//! Plain-data audio model shared between the worker and the UI, plus the session grouping / matching rules.
//! Nothing here touches COM, so it is unit-testable and safe to use on any thread.

use crate::config::Config;
use crate::text::{contains_ic, eq_ic, file_name, file_stem};

pub const VOLUME_INCREMENT: f32 = 0.05;
pub const SYSTEM_SOUNDS_ID: &str = "%b#";

/// Opaque id the worker hands out for a live session; the UI refers to sessions only through these.
pub type SessionHandle = u32;

#[derive(Clone, Debug, Default)]
pub struct SessionInfo {
    pub handle: SessionHandle,
    pub pid: u32,
    pub is_system: bool,
    pub instance_id: String,
    pub display_name: String,
    pub has_identity: bool,
    pub app_file_name: String,
    pub app_friendly_name: String,
    pub exe_path: Option<String>,
    pub volume: f32,
    pub muted: bool,
}

#[derive(Clone, Debug, Default)]
pub struct AppInfo {
    pub device_id: String,
    pub app_friendly_name: String,
    pub app_file_name: String,
    pub sessions: Vec<SessionInfo>,
}

#[derive(Clone, Debug, Default)]
pub struct DeviceInfo {
    pub id: String,
    pub friendly_name: String,
    pub description: Option<String>,
    pub master_volume: f32,
    pub master_muted: bool,
    pub apps: Vec<AppInfo>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DeviceIdentity {
    pub id: String,
    pub friendly_name: String,
    pub description: Option<String>,
}

impl DeviceInfo {
    pub fn identity(&self) -> Option<DeviceIdentity> {
        if self.id.is_empty() {
            return None;
        }
        Some(DeviceIdentity { id: self.id.clone(), friendly_name: self.friendly_name.clone(), description: self.description.clone() })
    }
}

#[derive(Clone, Debug, Default)]
pub struct Snapshot {
    pub devices: Vec<DeviceInfo>,
    pub default_device_id: Option<String>,
    /// Bumped by the worker on every published change.
    pub generation: u64,
}

impl Snapshot {
    pub fn all_apps(&self) -> impl Iterator<Item = &AppInfo> {
        self.devices.iter().flat_map(|d| d.apps.iter())
    }
}

/// Applies queued wheel notches in order, clamping after each one exactly like one-at-a-time adjustment.
pub fn apply_volume_steps(current: f32, steps: &[bool]) -> f32 {
    let mut v = current;
    for &up in steps {
        v = if up { (v + VOLUME_INCREMENT).min(1.0) } else { (v - VOLUME_INCREMENT).max(0.0) };
    }
    v
}

/// Strips a trailing `(Instance N)` (case-insensitive, any surrounding whitespace), like the original regex
/// `\s*\(\s*Instance\s+\d+\s*\)\s*$`.
fn strip_instance_suffix(s: &str) -> &str {
    let t = s.trim_end();
    let Some(rest) = t.strip_suffix(')') else { return s };
    let rest = rest.trim_end();
    let digits_end = rest.len();
    let digits_start = rest.trim_end_matches(|c: char| c.is_ascii_digit()).len();
    if digits_start == digits_end {
        return s;
    }
    let before_digits = &rest[..digits_start];
    let trimmed = before_digits.trim_end();
    if trimmed.len() == before_digits.len() {
        return s; // need at least one whitespace between "Instance" and the number
    }
    if trimmed.len() < 8
        || !trimmed.is_char_boundary(trimmed.len() - 8)
        || !trimmed[trimmed.len() - 8..].eq_ignore_ascii_case("instance")
    {
        return s;
    }
    let before_word = trimmed[..trimmed.len() - 8].trim_end();
    let Some(before_paren) = before_word.strip_suffix('(') else { return s };
    before_paren.trim_end()
}

/// Stable key for grouping sessions on a device. Strips trailing ` (Instance N)` from display-derived names so
/// Windows' duplicate labels (e.g. Discord) merge with `Discord.exe` from the process path.
pub fn grouping_key(name: &str) -> String {
    let t = name.trim();
    if t.is_empty() {
        return String::new();
    }
    if t == SYSTEM_SOUNDS_ID {
        return t.to_lowercase();
    }
    let mut t = strip_instance_suffix(t).trim().to_string();
    if !ends_with_exe(&t) {
        t.push_str(".exe");
    }
    t.to_lowercase()
}

pub fn ends_with_exe(s: &str) -> bool {
    s.len() >= 4 && s.is_char_boundary(s.len() - 4) && s[s.len() - 4..].eq_ignore_ascii_case(".exe")
}

/// Whether a WASAPI session belongs to the app described by a profile. Executable grouping first, then
/// display/friendly substring matching so profiles that only store "Discord" still collect every related session.
pub fn session_matches_config(
    session_exe: &str,
    session_display: &str,
    session_friendly: &str,
    config_file: &str,
    config_friendly: &str,
) -> bool {
    if grouping_key(session_exe) == grouping_key(config_file) {
        return true;
    }

    let mut disp = session_display.trim();
    if disp.is_empty() {
        disp = session_friendly.trim();
    }

    let needle = config_friendly.trim();
    if needle.chars().count() >= 2 && contains_ic(disp, needle) {
        return true;
    }

    let cfg_base = file_stem(config_file.trim());
    if cfg_base.chars().count() >= 3 && contains_ic(disp, cfg_base) {
        return true;
    }
    false
}

pub fn session_belongs_to_config(s: &SessionInfo, c: &Config) -> bool {
    s.has_identity
        && session_matches_config(&s.app_file_name, s.display_name.trim(), &s.app_friendly_name, &c.app_file_name, &c.app_friendly_name)
}

/// True when any session in `app` is the hotkey target described by `config`.
pub fn app_matches_config_on_device(app: &AppInfo, c: &Config) -> bool {
    if c.control_device_master_volume || !c.has_target() {
        return false;
    }
    if !eq_ic(&app.device_id, &c.device_id) {
        return false;
    }
    app.sessions.iter().any(|s| session_belongs_to_config(s, c))
}

/// Process id, then session instance id: the order the session picker shows.
pub fn compare_sessions(a: &SessionInfo, b: &SessionInfo) -> std::cmp::Ordering {
    a.pid.cmp(&b.pid).then_with(|| a.instance_id.cmp(&b.instance_id))
}

/// Every session on every device matching `config`, sorted, with the owning device's name.
pub fn collect_matching_sessions<'a>(snapshot: &'a Snapshot, c: &Config) -> Vec<(&'a SessionInfo, &'a str)> {
    let mut out = Vec::new();
    if c.control_device_master_volume || !c.has_target() {
        return out;
    }
    for dev in &snapshot.devices {
        for app in &dev.apps {
            for s in &app.sessions {
                if session_belongs_to_config(s, c) && !out.iter().any(|(o, _): &(&SessionInfo, &str)| o.handle == s.handle) {
                    out.push((s, dev.friendly_name.as_str()));
                }
            }
        }
    }
    out.sort_by(|a, b| compare_sessions(a.0, b.0));
    out
}

/// Groups a device's sessions into one app per executable (merging "Discord" / "Discord.exe" / casing variants).
pub fn group_sessions(device_id: &str, sessions: Vec<SessionInfo>) -> Vec<AppInfo> {
    let mut apps: Vec<AppInfo> = Vec::new();
    for s in sessions {
        if !s.has_identity {
            continue;
        }
        let key = grouping_key(&s.app_file_name);
        if let Some(existing) = apps.iter_mut().find(|a| grouping_key(&a.app_file_name) == key) {
            if !ends_with_exe(&existing.app_file_name) && ends_with_exe(&s.app_file_name) {
                existing.app_file_name = s.app_file_name.clone();
            }
            existing.sessions.push(s);
        } else {
            apps.push(AppInfo {
                device_id: device_id.to_string(),
                app_friendly_name: s.app_friendly_name.clone(),
                app_file_name: s.app_file_name.clone(),
                sessions: vec![s],
            });
        }
    }
    apps
}

/// Which sessions exist on which devices; the worker republishes only when this changes.
pub fn fingerprint(devices: &[DeviceInfo]) -> String {
    let mut parts = Vec::new();
    for d in devices {
        parts.push(format!("{}|{}", d.id, d.friendly_name));
        for a in &d.apps {
            for s in &a.sessions {
                parts.push(format!("{}|{}|{}|{}", d.id, s.pid, s.instance_id, s.display_name));
            }
        }
    }
    parts.sort();
    parts.join("\n")
}

/// Session display names like "@%SystemRoot%\System32\AudioSrv.Dll,-202" are unresolved resource refs.
pub fn looks_like_resource_reference(display: &str) -> bool {
    display.starts_with('@') && display.contains(",-")
}

/// Pulls the .exe segment from a session instance id (format varies by OS build and packaging).
pub fn exe_from_instance_id(instance_id: &str) -> Option<String> {
    let lower = instance_id.to_ascii_lowercase();
    let exe_idx = lower.rfind(".exe")?;
    let end = exe_idx + 4;
    let head = &instance_id[..exe_idx];
    let start = head
        .rfind('\\')
        .or_else(|| head.rfind('/'))
        .or_else(|| head.rfind('|'))
        .map(|i| i + 1)
        .unwrap_or(0);
    if start >= end {
        return None;
    }
    let s = &instance_id[start..end];
    (!s.is_empty()).then(|| s.to_string())
}

/// What the OS told us about a session's process.
pub struct ProcessImage {
    pub exe_path: Option<String>,
    pub process_name: Option<String>,
}

/// Fills the identity fields of `info` from the session's own data plus its process image.
pub fn resolve_identity(info: &mut SessionInfo, image: Option<&ProcessImage>, mut file_description: impl FnMut(&str) -> Option<String>) {
    if info.is_system {
        info.app_file_name = SYSTEM_SOUNDS_ID.to_string();
        info.app_friendly_name = "System Sounds".to_string();
        info.has_identity = true;
        return;
    }

    if info.pid != 0 {
        if let Some(image) = image {
            let app_file = match &image.exe_path {
                Some(p) if !p.is_empty() => file_name(p).to_string(),
                _ => {
                    let n = image.process_name.clone().unwrap_or_default();
                    if n.is_empty() || ends_with_exe(&n) { n } else { n + ".exe" }
                }
            };
            if app_file.is_empty() {
                return;
            }
            let display = info.display_name.trim();
            info.app_friendly_name = if !display.is_empty() && !looks_like_resource_reference(display) {
                display.to_string()
            } else {
                image
                    .exe_path
                    .as_deref()
                    .and_then(&mut file_description)
                    .unwrap_or_else(|| file_stem(&app_file).to_string())
            };
            info.app_file_name = app_file;
            info.exe_path = image.exe_path.clone();
            info.has_identity = true;
            return;
        }
    }

    let display = info.display_name.trim().to_string();
    if let Some(exe) = exe_from_instance_id(&info.instance_id) {
        info.app_friendly_name = if display.is_empty() { file_stem(&exe).to_string() } else { display };
        info.app_file_name = exe;
        info.has_identity = true;
        return;
    }
    if !display.is_empty() {
        info.app_file_name = display.clone();
        info.app_friendly_name = display;
        info.has_identity = true;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn grouping_key_matches_executable_group() {
        let cases = [
            ("Discord.exe", "discord.exe", true),
            ("Discord.exe", "Discord (Instance 2)", true),
            ("Discord (Instance 1)", "Discord (Instance 2)", true),
            ("Discord.exe", "chrome.exe", false),
            ("%b#", "%b#", true),
            ("Foo (instance   12 )", "foo.exe", true),
            ("Foo (Instances 1)", "foo.exe", false),
        ];
        for (a, b, same) in cases {
            assert_eq!(grouping_key(a) == grouping_key(b), same, "{a} vs {b}");
        }
    }

    #[test]
    fn session_matches_exe_display_or_friendly() {
        let cases = [
            ("Discord.exe", "Discord (Instance 2)", "", "Discord.exe", "", true),
            ("Discord.exe", "Discord (Instance 2)", "", "Discord", "", true),
            ("SomeOther.exe", "Discord (Instance 2)", "", "Discord.exe", "Discord", true),
            ("z.exe", "Google Chrome", "", "Discord.exe", "", false),
            ("Discord.exe", "", "", "Discord.exe", "", true),
        ];
        for (exe, disp, fr, cf, cfr, expect) in cases {
            assert_eq!(session_matches_config(exe, disp, fr, cf, cfr), expect, "{exe} {disp} {cf}");
        }
    }

    #[test]
    fn volume_steps_clamp_each_notch() {
        assert_eq!(apply_volume_steps(0.98, &[true, false]), 0.95);
        assert!((apply_volume_steps(0.5, &[true, true]) - 0.6).abs() < 1e-6);
        assert_eq!(apply_volume_steps(0.02, &[false, true]), 0.05);
    }

    #[test]
    fn exe_from_instance_id_parses_segment() {
        let id = r"{0.0.0.00000000}.{abc}|\Device\HarddiskVolume3\Program Files\Discord\Discord.exe%b{00000000-0000-0000-0000-000000000000}|1%b1234";
        assert_eq!(exe_from_instance_id(id).as_deref(), Some("Discord.exe"));
        assert_eq!(exe_from_instance_id("nothing here"), None);
    }

    #[test]
    fn groups_merge_variants() {
        let mk = |file: &str, h| SessionInfo { handle: h, has_identity: true, app_file_name: file.into(), app_friendly_name: "Discord".into(), ..Default::default() };
        let apps = group_sessions("d", vec![mk("Discord (Instance 1)", 1), mk("Discord.exe", 2), mk("chrome.exe", 3)]);
        assert_eq!(apps.len(), 2);
        assert_eq!(apps[0].app_file_name, "Discord.exe");
        assert_eq!(apps[0].sessions.len(), 2);
    }
}
