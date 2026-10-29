//! Global hotkeys + mouse wheel via raw input, on a dedicated thread with its own hidden window.
//!
//! This runs for every key press system-wide, so it does no allocation-heavy or audio work: it tracks which
//! configured hotkeys are held and queues wheel notches to the audio worker. Mouse raw input is registered only
//! while a hotkey is held, because a gaming mouse can report thousands of times per second.

use crate::win::*;
use crate::worker::Worker;
use std::sync::atomic::{AtomicBool, AtomicIsize, Ordering};
use std::sync::{Arc, Mutex};

pub const WM_APP_HOTKEY_CAPTURED: u32 = WM_APP + 10;
const WHEEL_DELTA_PER_NOTCH: i32 = 120;

#[derive(Default, Clone)]
pub struct HotkeyProfile {
    pub config_id: String,
    pub hotkeys: Vec<i32>,
}

pub struct InputShared {
    profiles: Mutex<Vec<HotkeyProfile>>,
    /// Next key-down is delivered to the UI (WM_APP_HOTKEY_CAPTURED) instead of being treated as a hotkey.
    pub capture_next_key: AtomicBool,
    ui_hwnd: AtomicIsize,
    worker: Arc<Worker>,
}

impl InputShared {
    pub fn set_profiles(&self, p: Vec<HotkeyProfile>) {
        *self.profiles.lock().unwrap_or_else(|e| e.into_inner()) = p;
    }
}

struct ThreadState {
    shared: Arc<InputShared>,
    held: Vec<i32>,
    mouse_registered: bool,
    wheel_remainder: i32,
}

pub fn start(worker: Arc<Worker>, ui_hwnd: HWND) -> Arc<InputShared> {
    let shared = Arc::new(InputShared {
        profiles: Mutex::new(Vec::new()),
        capture_next_key: AtomicBool::new(false),
        ui_hwnd: AtomicIsize::new(ui_hwnd),
        worker,
    });
    let s2 = shared.clone();
    std::thread::Builder::new()
        .name("KBMixer input".into())
        .spawn(move || unsafe { run(s2) })
        .expect("spawn input thread");
    shared
}

unsafe fn run(shared: Arc<InputShared>) {
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
    let inst = GetModuleHandleW(core::ptr::null());
    let class = wide("KBMixerRawInput");
    let wc = WNDCLASSEXW {
        cbSize: core::mem::size_of::<WNDCLASSEXW>() as u32,
        style: 0,
        lpfnWndProc: Some(wndproc),
        cbClsExtra: 0,
        cbWndExtra: 0,
        hInstance: inst,
        hIcon: 0,
        hCursor: 0,
        hbrBackground: 0,
        lpszMenuName: core::ptr::null(),
        lpszClassName: class.as_ptr(),
        hIconSm: 0,
    };
    RegisterClassExW(&wc);
    // A hidden (never shown) top-level popup: receives raw input with RIDEV_INPUTSINK.
    let hwnd = CreateWindowExW(WS_EX_TOOLWINDOW, class.as_ptr(), class.as_ptr(), WS_POPUP, 0, 0, 0, 0, 0, 0, inst, core::ptr::null());
    if hwnd == 0 {
        return;
    }
    let state = Box::new(ThreadState { shared, held: Vec::new(), mouse_registered: false, wheel_remainder: 0 });
    SetWindowLongPtrW(hwnd, GWLP_USERDATA, Box::into_raw(state) as isize);

    let kb = RAWINPUTDEVICE { usUsagePage: 0x01, usUsage: 0x06, dwFlags: RIDEV_INPUTSINK, hwndTarget: hwnd };
    RegisterRawInputDevices(&kb, 1, core::mem::size_of::<RAWINPUTDEVICE>() as u32);

    let mut msg: MSG = core::mem::zeroed();
    while GetMessageW(&mut msg, 0, 0, 0) > 0 {
        DispatchMessageW(&msg);
    }
}

unsafe extern "system" fn wndproc(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT {
    if msg == WM_INPUT {
        let p = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut ThreadState;
        if !p.is_null() {
            handle_raw_input(&mut *p, hwnd, l);
        }
        // Raw input sent while in the background must still go to DefWindowProc for cleanup.
    }
    DefWindowProcW(hwnd, msg, w, l)
}

const HEADER: usize = core::mem::size_of::<RAWINPUTHEADER>();

unsafe fn handle_raw_input(st: &mut ThreadState, hwnd: HWND, l: LPARAM) {
    let mut buf = [0u8; 64];
    let mut size = buf.len() as u32;
    let read = GetRawInputData(l, RID_INPUT, buf.as_mut_ptr() as *mut _, &mut size, HEADER as u32);
    if read == u32::MAX || (read as usize) < HEADER {
        return;
    }
    let ty = u32::from_le_bytes([buf[0], buf[1], buf[2], buf[3]]);
    let body = &buf[HEADER..read as usize];
    let u16_at = |o: usize| u16::from_le_bytes([body[o], body[o + 1]]);

    if ty == RIM_TYPEKEYBOARD && body.len() >= 8 {
        // RAWKEYBOARD: MakeCode, Flags, Reserved, VKey. Test the bit: extended keys report "Up | E0" on release.
        let key_up = u16_at(2) & RI_KEY_BREAK != 0;
        let vk = u16_at(6) as i32;
        if !key_up && st.shared.capture_next_key.swap(false, Ordering::AcqRel) {
            PostMessageW(st.shared.ui_hwnd.load(Ordering::Acquire), WM_APP_HOTKEY_CAPTURED, vk as usize, 0);
            return;
        }
        update_held(st, hwnd, vk, key_up);
    } else if ty == RIM_TYPEMOUSE && body.len() >= 8 {
        // RAWMOUSE: usFlags, (padding), usButtonFlags, usButtonData
        if u16_at(4) & RI_MOUSE_WHEEL == 0 {
            return;
        }
        on_wheel(st, u16_at(6) as i16 as i32);
    }
}

fn update_held(st: &mut ThreadState, hwnd: HWND, vk: i32, key_up: bool) {
    let listened = {
        let profiles = st.shared.profiles.lock().unwrap_or_else(|e| e.into_inner());
        profiles.iter().any(|p| p.hotkeys.contains(&vk))
    };
    if listened && !key_up && !st.held.contains(&vk) {
        // No rescan here: hotkeys are often game keys (Shift to sprint, Ctrl to crouch) pressed constantly.
        st.held.push(vk);
    } else if key_up && st.held.contains(&vk) {
        st.held.retain(|&k| k != vk);
    } else {
        return;
    }
    update_mouse_registration(st, hwnd);
}

fn update_mouse_registration(st: &mut ThreadState, hwnd: HWND) {
    let want = !st.held.is_empty();
    if want == st.mouse_registered {
        return;
    }
    let dev = RAWINPUTDEVICE {
        usUsagePage: 0x01,
        usUsage: 0x02,
        dwFlags: if want { RIDEV_INPUTSINK } else { RIDEV_REMOVE },
        hwndTarget: if want { hwnd } else { 0 },
    };
    if unsafe { RegisterRawInputDevices(&dev, 1, core::mem::size_of::<RAWINPUTDEVICE>() as u32) } != 0 {
        st.mouse_registered = want;
        st.wheel_remainder = 0;
    }
}

fn on_wheel(st: &mut ThreadState, delta: i32) {
    if st.held.is_empty() {
        return;
    }
    // One volume step per standard notch (120). High-resolution wheels report smaller deltas and some drivers batch
    // several notches into one report, so accumulate instead of matching ±120.
    st.wheel_remainder += delta;
    let notches = st.wheel_remainder / WHEEL_DELTA_PER_NOTCH;
    if notches == 0 {
        return;
    }
    st.wheel_remainder -= notches * WHEEL_DELTA_PER_NOTCH;
    let steps = vec![notches > 0; notches.unsigned_abs() as usize];

    let profiles = st.shared.profiles.lock().unwrap_or_else(|e| e.into_inner());
    for p in profiles.iter() {
        // Same rule as before: the held keys, in press order, must equal the profile's hotkeys exactly.
        if !p.hotkeys.is_empty() && p.hotkeys == st.held {
            st.shared.worker.queue_adjust(&p.config_id, &steps);
        }
    }
}
