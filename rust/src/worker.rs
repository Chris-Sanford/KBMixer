//! Owns every Core Audio call on one dedicated MTA thread.
//!
//! Calls into the Windows audio service are cross-process and can stall while a game is hammering it, so neither the
//! UI thread nor the raw-input thread ever makes them. Writes are coalesced: a slider drag applies only its latest
//! value and queued wheel notches are applied in one read-modify-write per target. Devices (and their session-created
//! registrations) live as long as the endpoint does, so nothing leaks across scans.

use crate::audio::{self, *};
use crate::com::*;
use crate::config::Config;
use crate::text::eq_ic;
use crate::win::*;
use std::collections::HashMap;
use std::sync::{Arc, Condvar, Mutex, Weak};
use std::thread::JoinHandle;
use std::time::Duration;

pub const WM_APP_SNAPSHOT: u32 = WM_APP + 1;
pub const WM_APP_POLL: u32 = WM_APP + 2;
pub const WM_APP_SCAN_DONE: u32 = WM_APP + 3;

const POLL_INTERVAL_MS: u64 = 120;
const SCAN_INTERVAL_MS: u64 = 3000;
const MIN_SCAN_GAP_MS: u64 = 750;
const DEVICE_NAME_REFRESH_MS: u64 = 60_000;

#[derive(Clone, Debug, PartialEq)]
pub enum Target {
    /// A device's master (endpoint) volume.
    Master(String),
    /// One or more sessions; reads use the first, writes apply to all.
    Sessions(Vec<SessionHandle>),
}

#[derive(Clone, Copy, Debug)]
pub struct PollResult {
    pub key: u64,
    pub volume: Option<f32>,
    pub muted: Option<bool>,
}

#[derive(Default)]
struct State {
    wake: bool,
    stopping: bool,
    scan_requested: bool,
    scan_waiters: Vec<u64>,
    adjusts: Vec<(String, Vec<bool>)>,
    sets: Vec<(u64, Target, f32)>,
    mutes: Vec<Target>,
    poll_targets: Vec<(u64, Target)>,
    poll_enabled: bool,
    poll_now: bool,
    profiles: Vec<Config>,

    // Outputs.
    snapshot: Option<Arc<Snapshot>>,
    first_scan_done: bool,
    poll_results: Option<Vec<PollResult>>,
    scans_completed: Vec<u64>,
}

struct Shared {
    state: Mutex<State>,
    cv: Condvar,
    hwnd: std::sync::atomic::AtomicIsize,
}

impl Shared {
    fn with<R>(&self, f: impl FnOnce(&mut State) -> R) -> R {
        let mut s = self.state.lock().unwrap_or_else(|e| e.into_inner());
        let r = f(&mut s);
        s.wake = true;
        drop(s);
        self.cv.notify_all();
        r
    }

    fn post(&self, msg: u32) {
        let hwnd = self.hwnd.load(std::sync::atomic::Ordering::Acquire);
        if hwnd != 0 {
            unsafe { PostMessageW(hwnd, msg, 0, 0) };
        }
    }
}

pub struct Worker {
    shared: Arc<Shared>,
    thread: Option<JoinHandle<()>>,
}

impl Worker {
    pub fn start() -> Worker {
        let shared = Arc::new(Shared {
            state: Mutex::new(State { scan_requested: true, ..Default::default() }),
            cv: Condvar::new(),
            hwnd: Default::default(),
        });
        let s2 = shared.clone();
        let thread = std::thread::Builder::new()
            .name("KBMixer audio".into())
            .spawn(move || {
                // A full-screen game can saturate every core; the worker does little work, so a small bump keeps
                // hotkey volume changes responsive without competing meaningfully with the game.
                unsafe { SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL) };
                let _com = ComInit::mta();
                Engine::new(Arc::downgrade(&s2)).run(&s2);
            })
            .expect("spawn audio worker");
        Worker { shared, thread: Some(thread) }
    }

    pub fn set_notify_window(&self, hwnd: HWND) {
        self.shared.hwnd.store(hwnd, std::sync::atomic::Ordering::Release);
    }

    pub fn wait_first_snapshot(&self, timeout: Duration) -> Option<Arc<Snapshot>> {
        let guard = self.shared.state.lock().unwrap_or_else(|e| e.into_inner());
        let (guard, _) = self
            .shared
            .cv
            .wait_timeout_while(guard, timeout, |s| !s.first_scan_done)
            .unwrap_or_else(|e| e.into_inner());
        guard.snapshot.clone()
    }

    pub fn snapshot(&self) -> Option<Arc<Snapshot>> {
        self.shared.state.lock().unwrap_or_else(|e| e.into_inner()).snapshot.clone()
    }

    pub fn take_poll_results(&self) -> Option<Vec<PollResult>> {
        self.shared.state.lock().unwrap_or_else(|e| e.into_inner()).poll_results.take()
    }

    pub fn take_completed_scans(&self) -> Vec<u64> {
        std::mem::take(&mut self.shared.state.lock().unwrap_or_else(|e| e.into_inner()).scans_completed)
    }

    /// Profiles used to resolve hotkey adjustments (target app / device per profile id).
    pub fn set_profiles(&self, profiles: Vec<Config>) {
        self.shared.with(|s| s.profiles = profiles);
    }

    /// Queue `steps` wheel notches (true = up) for the profile with `config_id`.
    pub fn queue_adjust(&self, config_id: &str, steps: &[bool]) {
        self.shared.with(|s| match s.adjusts.iter_mut().find(|(id, _)| id == config_id) {
            Some((_, v)) => v.extend_from_slice(steps),
            None => s.adjusts.push((config_id.to_string(), steps.to_vec())),
        });
    }

    /// Absolute volume; a newer request for the same `key` replaces an unapplied one.
    pub fn queue_set_volume(&self, key: u64, target: Target, scalar: f32) {
        let scalar = scalar.clamp(0.0, 1.0);
        self.shared.with(|s| match s.sets.iter_mut().find(|(k, _, _)| *k == key) {
            Some(e) => *e = (key, target, scalar),
            None => s.sets.push((key, target, scalar)),
        });
    }

    /// Sessions: mute all if any is unmuted, otherwise unmute all.
    pub fn queue_toggle_mute(&self, target: Target) {
        self.shared.with(|s| s.mutes.push(target));
    }

    /// Cheap to call often: scans are rate-limited and only publish on change.
    pub fn request_scan(&self) {
        self.shared.with(|s| s.scan_requested = true);
    }

    /// Rescan now; `token` comes back through WM_APP_SCAN_DONE / take_completed_scans.
    pub fn scan_now(&self, token: u64) {
        self.shared.with(|s| {
            s.scan_waiters.push(token);
            s.scan_requested = true;
        });
    }

    pub fn set_poll_targets(&self, targets: Vec<(u64, Target)>) {
        self.shared.with(|s| {
            s.poll_targets = targets;
            s.poll_now = true;
        });
    }

    /// Mixer polling only matters while the window is on screen.
    pub fn set_polling(&self, enabled: bool) {
        self.shared.with(|s| {
            s.poll_enabled = enabled;
            s.poll_now = enabled;
        });
    }
}

impl Drop for Worker {
    fn drop(&mut self) {
        self.shared.with(|s| s.stopping = true);
        if let Some(t) = self.thread.take() {
            let _ = t.join();
        }
    }
}

// ──────────────────── Worker thread ────────────────────

struct DeviceEntry {
    id: String,
    device: Device,
    endpoint: Option<EndpointVolume>,
    manager: Option<SessionManager>,
    registered: bool,
    names: Option<(String, Option<String>, u64)>,
}

struct LiveSession {
    volume: Option<SimpleVolume>,
    // Kept so the session object (and its volume interface) stays alive between scans.
    _control: SessionControl,
}

struct Engine {
    enumerator: Option<DeviceEnumerator>,
    devices: Vec<DeviceEntry>,
    sessions: HashMap<SessionHandle, LiveSession>,
    session_keys: HashMap<String, SessionHandle>,
    next_handle: SessionHandle,
    images: HashMap<String, Option<ProcessImageOwned>>,
    descriptions: HashMap<String, Option<String>>,
    notifier: SessionNotifier,
    last_fingerprint: Option<String>,
    last_scan: u64,
    generation: u64,
    current: Option<Arc<Snapshot>>,
}

#[derive(Clone)]
struct ProcessImageOwned {
    exe_path: Option<String>,
    process_name: Option<String>,
}

impl Engine {
    fn new(shared: Weak<Shared>) -> Engine {
        let notifier = SessionNotifier::new(Box::new(move || {
            if let Some(s) = shared.upgrade() {
                s.with(|st| st.scan_requested = true);
            }
        }));
        Engine {
            enumerator: DeviceEnumerator::create().ok(),
            devices: Vec::new(),
            sessions: HashMap::new(),
            session_keys: HashMap::new(),
            next_handle: 1,
            images: HashMap::new(),
            descriptions: HashMap::new(),
            notifier,
            last_fingerprint: None,
            last_scan: 0,
            generation: 0,
            current: None,
        }
    }

    fn run(mut self, shared: &Shared) {
        let mut next_poll = 0u64;
        let mut first = true;
        loop {
            // Take the queued work under the lock, then do COM calls without it.
            let (adjusts, sets, mutes, profiles, scan_req, waiters, poll_enabled, poll_now, stopping) = {
                let mut s = shared.state.lock().unwrap_or_else(|e| e.into_inner());
                s.wake = false;
                let adjusts = std::mem::take(&mut s.adjusts);
                let profiles = if adjusts.is_empty() { Vec::new() } else { s.profiles.clone() };
                (
                    adjusts,
                    std::mem::take(&mut s.sets),
                    std::mem::take(&mut s.mutes),
                    profiles,
                    s.scan_requested,
                    !s.scan_waiters.is_empty(),
                    s.poll_enabled,
                    s.poll_now,
                    s.stopping,
                )
            };
            if stopping {
                break;
            }

            let wrote = !adjusts.is_empty() || !sets.is_empty() || !mutes.is_empty();
            let mut failed = false;
            for (id, steps) in &adjusts {
                if let Some(c) = profiles.iter().find(|c| &c.config_id == id) {
                    failed |= !self.apply_adjust(c, steps);
                }
            }
            for (_, target, v) in &sets {
                failed |= !self.apply_set(target, *v);
            }
            for target in &mutes {
                self.toggle_mute(target);
            }
            let mut scan_requested = scan_req;
            if failed {
                // A failing session has usually expired (app closed); rescan so hotkeys find its replacement.
                scan_requested = true;
                shared.with(|s| s.scan_requested = true);
            }

            let now = tick_ms();
            let since = now.saturating_sub(self.last_scan);
            if first || since >= SCAN_INTERVAL_MS || waiters || (scan_requested && since >= MIN_SCAN_GAP_MS) {
                first = false;
                shared.state.lock().unwrap_or_else(|e| e.into_inner()).scan_requested = false;
                let t0 = tick_ms();
                let published = self.scan();
                debug_log(&format!("scan {} ms (published={})", tick_ms() - t0, published.is_some()));
                self.last_scan = tick_ms();
                let done = {
                    let mut s = shared.state.lock().unwrap_or_else(|e| e.into_inner());
                    if let Some(snap) = &published {
                        s.snapshot = Some(snap.clone());
                    }
                    s.first_scan_done = true;
                    let w = std::mem::take(&mut s.scan_waiters);
                    s.scans_completed.extend_from_slice(&w);
                    !w.is_empty()
                };
                shared.cv.notify_all();
                if published.is_some() {
                    shared.post(WM_APP_SNAPSHOT);
                }
                if done {
                    shared.post(WM_APP_SCAN_DONE);
                }
            }

            let now = tick_ms();
            if poll_enabled && (wrote || poll_now || now >= next_poll) {
                let targets = {
                    let mut s = shared.state.lock().unwrap_or_else(|e| e.into_inner());
                    s.poll_now = false;
                    s.poll_targets.clone()
                };
                if !targets.is_empty() {
                    let results = self.poll(&targets);
                    shared.state.lock().unwrap_or_else(|e| e.into_inner()).poll_results = Some(results);
                    shared.post(WM_APP_POLL);
                }
                next_poll = tick_ms() + POLL_INTERVAL_MS;
            }

            let now = tick_ms();
            let s = shared.state.lock().unwrap_or_else(|e| e.into_inner());
            let mut wait = (self.last_scan + SCAN_INTERVAL_MS).saturating_sub(now);
            if s.scan_requested {
                wait = wait.min((self.last_scan + MIN_SCAN_GAP_MS).saturating_sub(now));
            }
            if s.poll_enabled {
                wait = wait.min(next_poll.saturating_sub(now));
            }
            let wait = wait.clamp(1, SCAN_INTERVAL_MS);
            let _ = shared
                .cv
                .wait_timeout_while(s, Duration::from_millis(wait), |s| !s.wake)
                .unwrap_or_else(|e| e.into_inner());
        }

        for d in &self.devices {
            if d.registered {
                if let Some(m) = &d.manager {
                    m.unregister(&self.notifier);
                }
            }
        }
    }

    fn device(&self, id: &str) -> Option<&DeviceEntry> {
        self.devices.iter().find(|d| eq_ic(&d.id, id))
    }

    fn apply_adjust(&mut self, c: &Config, steps: &[bool]) -> bool {
        if c.control_device_master_volume {
            let Some(ep) = self.device(&c.device_id).and_then(|d| d.endpoint.clone()) else { return true };
            return match ep.scalar() {
                Ok(cur) => {
                    let next = apply_volume_steps(cur, steps);
                    next == cur || ep.set_scalar(next).is_ok()
                }
                Err(_) => false,
            };
        }

        let Some(snap) = self.current.clone() else { return false };
        let handles: Vec<SessionHandle> = collect_matching_sessions(&snap, c).iter().map(|(s, _)| s.handle).collect();
        if handles.is_empty() {
            // The target may have just started playing; pick it up for the next notch.
            return false;
        }
        let pick: Vec<SessionHandle> = match usize::try_from(c.process_index) {
            Ok(i) if c.control_single_session && i < handles.len() => vec![handles[i]],
            _ => handles,
        };
        let mut ok = true;
        for h in pick {
            let Some(vol) = self.sessions.get(&h).and_then(|s| s.volume.as_ref()) else {
                ok = false;
                continue;
            };
            match vol.volume() {
                Ok(cur) => {
                    let next = apply_volume_steps(cur, steps);
                    if next != cur && vol.set_volume(next).is_err() {
                        ok = false;
                    }
                }
                Err(_) => ok = false,
            }
        }
        ok
    }

    fn apply_set(&self, target: &Target, v: f32) -> bool {
        match target {
            Target::Master(id) => self.device(id).and_then(|d| d.endpoint.as_ref()).is_some_and(|ep| ep.set_scalar(v).is_ok()),
            Target::Sessions(hs) => {
                let mut ok = true;
                for h in hs {
                    match self.sessions.get(h).and_then(|s| s.volume.as_ref()) {
                        Some(vol) => ok &= vol.set_volume(v).is_ok(),
                        None => ok = false,
                    }
                }
                ok
            }
        }
    }

    fn toggle_mute(&self, target: &Target) {
        debug_log(&format!("toggle_mute {target:?}"));
        match target {
            Target::Master(id) => {
                if let Some(ep) = self.device(id).and_then(|d| d.endpoint.as_ref()) {
                    if let Ok(m) = ep.mute() {
                        let _ = ep.set_mute(!m);
                    }
                }
            }
            Target::Sessions(hs) => {
                let vols: Vec<&SimpleVolume> = hs.iter().filter_map(|h| self.sessions.get(h)?.volume.as_ref()).collect();
                let mut any_unmuted = false;
                for v in &vols {
                    // An unreadable session aborts the toggle rather than leave the group inconsistent.
                    match v.mute() {
                        Ok(false) => any_unmuted = true,
                        Ok(true) => {}
                        Err(_) => return,
                    }
                }
                for v in vols {
                    let _ = v.set_mute(any_unmuted);
                }
            }
        }
    }

    fn poll(&self, targets: &[(u64, Target)]) -> Vec<PollResult> {
        targets
            .iter()
            .map(|(key, t)| {
                let (mut volume, mut muted) = (None, None);
                match t {
                    Target::Master(id) => {
                        if let Some(ep) = self.device(id).and_then(|d| d.endpoint.as_ref()) {
                            volume = ep.scalar().ok();
                            muted = ep.mute().ok();
                        }
                    }
                    Target::Sessions(hs) => {
                        let vol = |h: &SessionHandle| self.sessions.get(h).and_then(|s| s.volume.as_ref());
                        volume = hs.first().and_then(vol).and_then(|v| v.volume().ok());
                        let mut all = true;
                        let mut read_all = !hs.is_empty();
                        for h in hs {
                            match vol(h).map(|v| v.mute()) {
                                Some(Ok(m)) => all &= m,
                                _ => {
                                    // Keep the last known state when a session disappears mid-refresh.
                                    read_all = false;
                                    break;
                                }
                            }
                        }
                        if read_all {
                            muted = Some(all);
                        }
                    }
                }
                PollResult { key: *key, volume, muted }
            })
            .collect()
    }

    /// Re-reads devices and sessions; returns a snapshot only when something changed.
    fn scan(&mut self) -> Option<Arc<Snapshot>> {
        let enumerator = self.enumerator.clone()?;
        let live = enumerator.active_render_devices().unwrap_or_default();

        let mut seen_ids: Vec<String> = Vec::new();
        for dev in live {
            let Ok(id) = dev.id() else { continue };
            if seen_ids.iter().any(|s| eq_ic(s, &id)) {
                continue;
            }
            seen_ids.push(id.clone());
            if self.device(&id).is_none() {
                self.devices.push(DeviceEntry {
                    endpoint: dev.endpoint_volume().ok(),
                    manager: dev.session_manager().ok(),
                    device: dev,
                    id,
                    registered: false,
                    names: None,
                });
            }
        }

        // Endpoints that went away: unregister notifications and release.
        let notifier = &self.notifier;
        self.devices.retain(|d| {
            let keep = seen_ids.iter().any(|s| eq_ic(s, &d.id));
            if !keep && d.registered {
                if let Some(m) = &d.manager {
                    m.unregister(notifier);
                }
            }
            keep
        });

        let now = tick_ms();
        let mut rename_check_done = false;
        let mut live_keys: Vec<String> = Vec::new();
        let mut new_sessions: HashMap<SessionHandle, LiveSession> = HashMap::new();
        let mut infos: Vec<DeviceInfo> = Vec::new();

        let mut devices = std::mem::take(&mut self.devices);
        for d in devices.iter_mut() {
            // Device names: read once, then re-read at most one stale device per scan to catch renames.
            let stale = d.names.as_ref().is_some_and(|n| now.saturating_sub(n.2) >= DEVICE_NAME_REFRESH_MS);
            if d.names.is_none() || (stale && !rename_check_done) {
                rename_check_done |= d.names.is_some();
                let (f, desc) = d.device.names();
                d.names = Some((f, desc, now));
            }
            let (friendly, description, _) = d.names.clone().unwrap_or_default();

            let mut info = DeviceInfo { id: d.id.clone(), friendly_name: friendly, description, ..Default::default() };
            if let Some(ep) = &d.endpoint {
                info.master_volume = ep.scalar().unwrap_or(0.0);
                info.master_muted = ep.mute().unwrap_or(false);
            }

            let mut sessions = Vec::new();
            if let Some(mgr) = &d.manager {
                let controls = mgr.sessions().unwrap_or_default();
                // RegisterSessionNotification requires one GetSessionEnumerator call first (done just above).
                if !d.registered {
                    d.registered = mgr.register(&self.notifier).is_ok();
                }
                for ctl in controls {
                    let instance_id = ctl.instance_id();
                    let pid = ctl.process_id();
                    let mut key = format!("{}|{}|{}", d.id.to_lowercase(), instance_id, pid);
                    // Instance ids are unique per session; guard against duplicates anyway.
                    while live_keys.contains(&key) {
                        key.push('#');
                    }
                    live_keys.push(key.clone());
                    let handle = *self.session_keys.entry(key).or_insert_with(|| {
                        let h = self.next_handle;
                        self.next_handle += 1;
                        h
                    });

                    let vol = ctl.simple_volume();
                    let mut s = SessionInfo {
                        handle,
                        pid,
                        is_system: ctl.is_system_sounds(),
                        instance_id,
                        display_name: ctl.display_name(),
                        ..Default::default()
                    };
                    if let Some(v) = &vol {
                        s.volume = v.volume().unwrap_or(0.0);
                        s.muted = v.mute().unwrap_or(false);
                    }
                    let image = if s.pid != 0 && !s.is_system { self.process_image(s.pid, &s.instance_id) } else { None };
                    let descs = &mut self.descriptions;
                    audio::resolve_identity(
                        &mut s,
                        image.as_ref().map(|i| ProcessImage { exe_path: i.exe_path.clone(), process_name: i.process_name.clone() }).as_ref(),
                        |p| cached_file_description(descs, p),
                    );
                    new_sessions.insert(handle, LiveSession { volume: vol, _control: ctl });
                    sessions.push(s);
                }
            }
            info.apps = group_sessions(&d.id, sessions);
            infos.push(info);
        }
        self.devices = devices;

        // Forget sessions that no longer exist (the old COM objects are released here).
        self.sessions = new_sessions;
        self.session_keys.retain(|k, _| live_keys.contains(k));
        let live_image_keys: Vec<String> = infos
            .iter()
            .flat_map(|d| d.apps.iter().flat_map(|a| a.sessions.iter()))
            .map(|s| image_key(s.pid, &s.instance_id))
            .collect();
        self.images.retain(|k, _| live_image_keys.contains(k));

        let fp = fingerprint(&infos);
        let default_id = enumerator.default_render_device().ok().and_then(|d| d.id().ok());
        let changed = self.current.is_none()
            || self.last_fingerprint.as_deref() != Some(fp.as_str())
            || self.current.as_ref().is_some_and(|c| c.default_device_id != default_id);

        if changed {
            self.last_fingerprint = Some(fp);
            self.generation += 1;
            let snap = Arc::new(Snapshot { devices: infos, default_device_id: default_id, generation: self.generation });
            self.current = Some(snap.clone());
            Some(snap)
        } else {
            // Same sessions; keep handles valid but refresh the stored levels for the next row rebuild.
            if let Some(cur) = &self.current {
                let mut updated = (**cur).clone();
                updated.devices = infos;
                self.current = Some(Arc::new(updated));
            }
            None
        }
    }

    /// Keyed by session instance id (it embeds the PID), so a recycled PID never reuses a stale entry.
    fn process_image(&mut self, pid: u32, instance_id: &str) -> Option<ProcessImageOwned> {
        let key = image_key(pid, instance_id);
        if let Some(c) = self.images.get(&key) {
            return c.clone();
        }
        let img = query_process_image(pid);
        self.images.insert(key, img.clone());
        img
    }
}

fn image_key(pid: u32, instance_id: &str) -> String {
    if instance_id.is_empty() { format!("pid:{pid}") } else { instance_id.to_string() }
}

/// Uses PROCESS_QUERY_LIMITED_INFORMATION, which works for elevated and anti-cheat protected games and never walks
/// the target's module list. Falls back to the process snapshot for the bare image name.
fn query_process_image(pid: u32) -> Option<ProcessImageOwned> {
    unsafe {
        let h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
        if h != 0 {
            let mut buf = [0u16; 1024];
            let mut size = buf.len() as u32;
            let ok = QueryFullProcessImageNameW(h, 0, buf.as_mut_ptr(), &mut size) != 0 && size > 0;
            CloseHandle(h);
            if ok {
                return Some(ProcessImageOwned {
                    exe_path: Some(String::from_utf16_lossy(&buf[..size as usize])),
                    process_name: None,
                });
            }
        }
        let snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snap == INVALID_HANDLE_VALUE {
            return None;
        }
        let mut e: PROCESSENTRY32W = core::mem::zeroed();
        e.dwSize = core::mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut found = None;
        if Process32FirstW(snap, &mut e) != 0 {
            loop {
                if e.th32ProcessID == pid {
                    found = Some(ProcessImageOwned { exe_path: None, process_name: Some(from_wide(&e.szExeFile)) });
                    break;
                }
                if Process32NextW(snap, &mut e) == 0 {
                    break;
                }
            }
        }
        CloseHandle(snap);
        found
    }
}

fn cached_file_description(cache: &mut HashMap<String, Option<String>>, path: &str) -> Option<String> {
    let key = path.to_lowercase();
    if let Some(v) = cache.get(&key) {
        return v.clone();
    }
    let v = file_description(path);
    cache.insert(key, v.clone());
    v
}

/// Friendly product name from the executable's version resource ("Google Chrome" for chrome.exe).
pub fn file_description(path: &str) -> Option<String> {
    unsafe {
        let wpath = wide(path);
        let mut dummy = 0;
        let size = GetFileVersionInfoSizeW(wpath.as_ptr(), &mut dummy);
        if size == 0 {
            return None;
        }
        let mut data = vec![0u8; size as usize];
        if GetFileVersionInfoW(wpath.as_ptr(), 0, size, data.as_mut_ptr() as *mut _) == 0 {
            return None;
        }
        let block = data.as_ptr() as *const core::ffi::c_void;

        let mut langs: Vec<(u16, u16)> = Vec::new();
        let mut p = core::ptr::null_mut();
        let mut len = 0u32;
        let sub = wide("\\VarFileInfo\\Translation");
        if VerQueryValueW(block, sub.as_ptr(), &mut p, &mut len) != 0 && !p.is_null() {
            let pairs = core::slice::from_raw_parts(p as *const u16, (len / 2) as usize);
            for c in pairs.chunks_exact(2) {
                langs.push((c[0], c[1]));
            }
        }
        langs.extend([(0x0409, 0x04B0), (0x0409, 0x04E4), (0x0409, 0x0000)]);

        let query = |name: &str| -> Option<String> {
            for (lang, cp) in &langs {
                let sub = wide(&format!("\\StringFileInfo\\{lang:04x}{cp:04x}\\{name}"));
                let mut p = core::ptr::null_mut();
                let mut len = 0u32;
                if VerQueryValueW(block, sub.as_ptr(), &mut p, &mut len) != 0 && !p.is_null() && len > 0 {
                    let s = from_wide(core::slice::from_raw_parts(p as *const u16, len as usize));
                    return Some(s.trim().to_string());
                }
            }
            None
        };
        query("FileDescription")
            .filter(|s| !s.is_empty())
            .or_else(|| query("ProductName"))
            .filter(|s| !s.is_empty())
    }
}

/// `--list-audio-sessions`: writes every render session on every active device to %TEMP% and returns the path.
pub fn write_session_diagnostic_file() -> std::path::PathBuf {
    let _com = ComInit::mta();
    let mut out = String::new();
    if let Ok(en) = DeviceEnumerator::create() {
        for (d, dev) in en.active_render_devices().unwrap_or_default().iter().enumerate() {
            let (name, _) = dev.names();
            out.push_str(&format!("=== [{d}] {name}\n    ID: {}\n", dev.id().unwrap_or_default()));
            let mut descs = HashMap::new();
            if let Ok(mgr) = dev.session_manager() {
                for (i, ctl) in mgr.sessions().unwrap_or_default().iter().enumerate() {
                    let mut s = SessionInfo {
                        pid: ctl.process_id(),
                        is_system: ctl.is_system_sounds(),
                        instance_id: ctl.instance_id(),
                        display_name: ctl.display_name(),
                        ..Default::default()
                    };
                    let img = if s.pid != 0 && !s.is_system { query_process_image(s.pid) } else { None };
                    audio::resolve_identity(
                        &mut s,
                        img.map(|i| ProcessImage { exe_path: i.exe_path, process_name: i.process_name }).as_ref(),
                        |p| cached_file_description(&mut descs, p),
                    );
                    out.push_str(&format!("  [{i}] PID={} Display=\"{}\"\n", s.pid, s.display_name));
                    out.push_str(&format!(
                        "        exeId={}  groupingKey={}  identityFriendly={}\n",
                        s.app_file_name,
                        grouping_key(&s.app_file_name),
                        s.app_friendly_name
                    ));
                }
            }
            out.push('\n');
        }
    }
    let path = std::env::temp_dir().join("KBMixer-audio-sessions.txt");
    let _ = std::fs::write(&path, out);
    path
}
