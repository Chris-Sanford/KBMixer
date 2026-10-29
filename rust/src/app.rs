//! Main window: profile editor, mixer strip, dialogs, tray behaviour. Port of MainWindow.xaml(.cs).

use crate::audio::*;
use crate::config::{self, Config, Reconcile};
use crate::gfx::{Canvas, Color, Font, Rect, Theme};
use crate::icons::IconCache;
use crate::input::{self, HotkeyProfile, InputShared};
use crate::keys;
use crate::startup;
use crate::text::{cmp_ic, eq_ic, is_blank};
use crate::tray::{self, Tray};
use crate::ui::{self, id, id_n, CursorKind, Ev, TextEdit, Ui, UiState};
use crate::update::{self, UpdateResult};
use crate::win::*;
use crate::worker::{self, Target, Worker};
use std::cell::RefCell;
use std::collections::VecDeque;
use std::sync::atomic::Ordering;
use std::sync::{Arc, Mutex};

const SLIDER_COOLDOWN_MS: u64 = 400;
const USER_INTERACTION_QUIET_MS: u64 = 2000;

const TIMER_DEFER_SNAPSHOT: usize = 1;
const TIMER_TOOLTIP: usize = 2;
const TIMER_CARET: usize = 3;
const TIMER_CHOOSE: usize = 4;

const WM_APP_UPDATE: u32 = WM_APP + 30;
const WM_APP_REPAINT: u32 = WM_APP + 31;
const WM_APP_TRAY_COMMAND: u32 = WM_APP + 32;

const GLYPH_HELP: char = '\u{E897}';
const GLYPH_SETTINGS: char = '\u{E713}';
const GLYPH_VOLUME: char = '\u{E995}';
const GLYPH_MUTED: char = '\u{E74F}';
const GLYPH_APP: char = '\u{E8D6}';
const GLYPH_CLOSE: char = '\u{E711}';
const GLYPH_INFO: char = '\u{E946}';

// ──────────────────── Model ────────────────────

struct Row {
    key: u64,
    is_master: bool,
    title: String,
    detail: Option<String>,
    exe_path: Option<String>,
    device_id: String,
    app: Option<AppInfo>,
    handles: Vec<SessionHandle>,
    target_unavailable: bool,
    is_hotkey_target: bool,
    percent: i32,
    muted: bool,
    last_interaction: u64,
}

impl Row {
    fn volume_enabled(&self) -> bool {
        !self.target_unavailable && (self.is_master || !self.handles.is_empty())
    }
    fn target(&self) -> Option<Target> {
        if self.is_master {
            Some(Target::Master(self.device_id.clone()))
        } else if !self.handles.is_empty() {
            Some(Target::Sessions(self.handles.clone()))
        } else {
            None
        }
    }
    fn mute_tooltip(&self) -> &'static str {
        if !self.volume_enabled() {
            "Mute unavailable"
        } else if self.is_master {
            if self.muted { "Unmute" } else { "Mute" }
        } else if self.muted {
            "Unmute all sessions"
        } else {
            "Mute all sessions"
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum DropKind {
    Profile,
    Device,
    Session,
}

struct Dropdown {
    kind: DropKind,
    items: Vec<String>,
    selected: Option<usize>,
    anchor: Rect,
}

struct PickItem {
    name: String,
    exe_path: Option<String>,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum PickMode {
    Select,
    Manual,
    DeviceMaster,
    NoTarget,
}

enum Dialog {
    Message { title: String, text: String },
    Help,
    CloseWarning { dont_remind: bool },
    Rename { edit: TextEdit, placeholder: String },
    AppSelect { mode: PickMode, items: Vec<PickItem>, selected: Option<usize>, edit: TextEdit },
    Settings { startup: bool },
    UpdateAvailable { latest: String, current: String, url: String },
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum DialogButton {
    Primary,
    Secondary,
    Close,
}

#[derive(Debug)]
enum Action {
    SelectProfile(usize),
    SelectDevice(usize),
    SelectSession(usize),
    OpenDropdown(DropKind, Rect),
    NewProfile,
    RenameProfile,
    DeleteProfile,
    Choose,
    AddHotkey,
    ResetHotkeys,
    ToggleSingleSession,
    RowMute(bool, usize),
    RowTarget(usize),
    Help,
    Settings,
    CloseInfoBar,
    Dialog(DialogButton),
    ToggleStartup,
    CheckUpdates,
}

struct Model {
    hwnd: HWND,
    worker: Arc<Worker>,
    input: Arc<InputShared>,
    tray: Tray,
    icons: IconCache,

    snapshot: Arc<Snapshot>,
    pending_snapshot: Option<Arc<Snapshot>>,
    ui_stale: bool,

    configs: Vec<Config>,
    current: usize,
    device_index: usize,
    session_entries: Vec<String>,

    rows_master: Vec<Row>,
    rows_apps: Vec<Row>,
    row_gen: u64,

    listening_hotkey: bool,
    dropdown: Option<Dropdown>,
    dialog: Option<Dialog>,
    dialog_queue: VecDeque<Dialog>,
    infobar: Option<String>,
    pending_rebind_notice: Option<String>,
    focus_request: Option<ui::Id>,

    visible: bool,
    close_confirmed: bool,
    choose_token: Option<u64>,
    next_token: u64,
    update_slot: Arc<Mutex<Option<(UpdateResult, bool)>>>,
    actions: Vec<Action>,
}

pub struct App {
    canvas: Canvas,
    theme: Theme,
    st: UiState,
    model: Model,
    mx: i32,
    my: i32,
    tracking: bool,
    taskbar_created: u32,
}

thread_local! {
    static APP: RefCell<Option<App>> = const { RefCell::new(None) };
}

fn data_flag_path() -> std::path::PathBuf {
    config::data_dir().join(".close-without-warning")
}

// ──────────────────── Startup ────────────────────

pub fn run(start_minimized: bool) {
    let worker = Arc::new(Worker::start());
    let first = worker.wait_first_snapshot(std::time::Duration::from_secs(10));

    unsafe {
        let inst = GetModuleHandleW(core::ptr::null());
        let class = wide("KBMixerWindow");
        let icon = LoadImageW(inst, 1, IMAGE_ICON, 0, 0, LR_DEFAULTCOLOR | 0x40 /* LR_DEFAULTSIZE */);
        let wc = WNDCLASSEXW {
            cbSize: core::mem::size_of::<WNDCLASSEXW>() as u32,
            style: CS_DBLCLKS,
            lpfnWndProc: Some(wndproc),
            cbClsExtra: 0,
            cbWndExtra: 0,
            hInstance: inst,
            hIcon: icon,
            hCursor: LoadCursorW(0, IDC_ARROW),
            hbrBackground: 0,
            lpszMenuName: core::ptr::null(),
            lpszClassName: class.as_ptr(),
            hIconSm: LoadImageW(inst, 1, IMAGE_ICON, 16, 16, LR_DEFAULTCOLOR),
        };
        RegisterClassExW(&wc);
        let title = wide("KBMixer");
        let hwnd = CreateWindowExW(
            0, class.as_ptr(), title.as_ptr(), WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
            CW_USEDEFAULT, CW_USEDEFAULT, 720, 820, 0, 0, inst, core::ptr::null(),
        );
        if hwnd == 0 {
            return;
        }
        let dpi = GetDpiForWindow(hwnd).max(96);
        // Same outer size as the WinUI build (720x820 at 100%).
        let w = (720 * dpi / 96) as i32;
        let h = (820 * dpi / 96) as i32;
        SetWindowPos(hwnd, 0, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

        let mica = std::env::var_os("KBMIXER_NO_MICA").is_none() && enable_mica(hwnd);
        let theme = Theme::load(mica);
        apply_title_bar(hwnd, &theme);

        let mut canvas = Canvas::new();
        canvas.set_scale(dpi as f32 / 96.0);

        worker.set_notify_window(hwnd);
        let input = input::start(worker.clone(), hwnd);
        let tray_icon = LoadImageW(inst, 1, IMAGE_ICON, (16 * dpi / 96) as i32, (16 * dpi / 96) as i32, LR_DEFAULTCOLOR);

        let loaded = config::load_all();
        let mut model = Model {
            hwnd,
            worker: worker.clone(),
            input,
            tray: Tray::new(hwnd, tray_icon),
            icons: IconCache::default(),
            snapshot: first.unwrap_or_default(),
            pending_snapshot: None,
            ui_stale: false,
            configs: loaded.configs,
            current: 0,
            device_index: 0,
            session_entries: Vec::new(),
            rows_master: Vec::new(),
            rows_apps: Vec::new(),
            row_gen: 0,
            listening_hotkey: false,
            dropdown: None,
            dialog: None,
            dialog_queue: VecDeque::new(),
            infobar: None,
            pending_rebind_notice: None,
            focus_request: None,
            visible: !start_minimized,
            close_confirmed: false,
            choose_token: None,
            next_token: 1,
            update_slot: Arc::new(Mutex::new(None)),
            actions: Vec::new(),
        };

        model.reconcile_config_devices();
        if model.configs.is_empty() {
            model.new_profile(true);
        }
        model.current = 0;
        model.load_config_to_form();
        model.sync_profiles();
        model.worker.set_polling(!start_minimized);
        model.show_rebind_notice_if_needed();
        if let Some(err) = loaded.error {
            model.show_dialog(Dialog::Message { title: "Configuration Load Error".into(), text: err });
        }

        let app = App {
            canvas,
            theme,
            st: UiState::default(),
            model,
            mx: -1,
            my: -1,
            tracking: false,
            taskbar_created: RegisterWindowMessageW(wide("TaskbarCreated").as_ptr()),
        };
        APP.with(|a| *a.borrow_mut() = Some(app));

        if start_minimized {
            with_app(|a| a.model.tray.show());
        } else {
            ShowWindow(hwnd, SW_SHOWNORMAL);
        }

        start_update_check(hwnd, with_app(|a| a.model.update_slot.clone()).unwrap(), true);

        let mut msg: MSG = core::mem::zeroed();
        while GetMessageW(&mut msg, 0, 0, 0) > 0 {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        // Drop the app (tray icon, worker handle) before the worker thread is joined.
        APP.with(|a| a.borrow_mut().take());
    }
}

fn enable_mica(hwnd: HWND) -> bool {
    let v = DWMSBT_MAINWINDOW;
    let ok = unsafe { DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, &v as *const u32 as *const _, 4) } >= 0;
    if ok {
        let m = MARGINS { cxLeftWidth: -1, cxRightWidth: -1, cyTopHeight: -1, cyBottomHeight: -1 };
        unsafe { DwmExtendFrameIntoClientArea(hwnd, &m) };
    }
    ok
}

fn apply_title_bar(hwnd: HWND, t: &Theme) {
    let dark: BOOL = t.dark as BOOL;
    unsafe {
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &dark as *const BOOL as *const _, 4);
        if !t.mica {
            let c = t.caption_color;
            let cref = (c >> 16) & 0xFF | (c & 0xFF00) | (c & 0xFF) << 16;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, &cref as *const u32 as *const _, 4);
        }
    }
}

fn start_update_check(hwnd: HWND, slot: Arc<Mutex<Option<(UpdateResult, bool)>>>, silent: bool) {
    std::thread::spawn(move || {
        let r = update::check();
        *slot.lock().unwrap_or_else(|e| e.into_inner()) = Some((r, silent));
        unsafe { PostMessageW(hwnd, WM_APP_UPDATE, 0, 0) };
    });
}

/// Runs `f` with the app unless it is already borrowed (re-entrant message during a modal loop or a synchronous
/// SendMessage from one of our own calls); then returns None and the caller falls back to default handling.
fn with_app<R>(f: impl FnOnce(&mut App) -> R) -> Option<R> {
    APP.with(|a| match a.try_borrow_mut() {
        Ok(mut g) => g.as_mut().map(f),
        Err(_) => None,
    })
}

// ──────────────────── Window procedure ────────────────────

unsafe extern "system" fn wndproc(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT {
    match msg {
        WM_PAINT => {
            let mut ps: PAINTSTRUCT = core::mem::zeroed();
            let dc = BeginPaint(hwnd, &mut ps);
            if with_app(|a| a.paint(dc)).is_none() {
                PostMessageW(hwnd, WM_APP_REPAINT, 0, 0);
            }
            EndPaint(hwnd, &ps);
            0
        }
        WM_ERASEBKGND => 1,
        WM_APP_REPAINT => {
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        WM_MOUSEMOVE => {
            with_app(|a| a.mouse_move(loword(l), hiword(l)));
            0
        }
        WM_MOUSELEAVE => {
            with_app(|a| {
                a.tracking = false;
                if a.st.active == 0 {
                    a.mouse_move(-1000, -1000);
                }
            });
            0
        }
        WM_LBUTTONDOWN | WM_LBUTTONDBLCLK => {
            SetCapture(hwnd);
            SetFocus(hwnd);
            with_app(|a| {
                a.mx = loword(l);
                a.my = hiword(l);
                a.input(Ev::MouseDown)
            });
            0
        }
        WM_LBUTTONUP => {
            ReleaseCapture();
            with_app(|a| {
                a.mx = loword(l);
                a.my = hiword(l);
                a.input(Ev::MouseUp)
            });
            0
        }
        WM_CAPTURECHANGED => {
            with_app(|a| {
                if a.st.active != 0 {
                    a.input(Ev::MouseUp);
                }
            });
            0
        }
        WM_MOUSEWHEEL => {
            let mut pt = POINT { x: loword(l), y: hiword(l) };
            ScreenToClient(hwnd, &mut pt);
            let delta = hiword(w as isize);
            with_app(|a| {
                a.mx = pt.x;
                a.my = pt.y;
                a.input(Ev::Wheel(delta))
            });
            0
        }
        WM_KEYDOWN | WM_SYSKEYDOWN => {
            let shift = GetKeyState(VK_SHIFT as i32) < 0;
            let ctrl = GetKeyState(VK_CONTROL as i32) < 0;
            let handled = with_app(|a| a.input(Ev::Key { vk: w as u32, shift, ctrl })).unwrap_or(false);
            if msg == WM_SYSKEYDOWN && !handled {
                return DefWindowProcW(hwnd, msg, w, l);
            }
            0
        }
        WM_CHAR => {
            with_app(|a| a.input(Ev::Char(w as u16)));
            0
        }
        WM_SETCURSOR => {
            if loword(l) as u16 == HTCLIENT {
                let kind = with_app(|a| a.st.cursor).unwrap_or_default();
                SetCursor(LoadCursorW(0, if kind == CursorKind::IBeam { IDC_IBEAM } else { IDC_ARROW }));
                return 1;
            }
            DefWindowProcW(hwnd, msg, w, l)
        }
        WM_TIMER => {
            with_app(|a| a.timer(w));
            0
        }
        WM_SIZE => {
            if w == SIZE_MINIMIZED {
                // Minimize goes to the notification area, like the WinUI build.
                PostMessageW(hwnd, WM_APP_TRAY_COMMAND, 100, 0);
            } else {
                InvalidateRect(hwnd, core::ptr::null(), 0);
            }
            0
        }
        WM_GETMINMAXINFO => {
            let mmi = &mut *(l as *mut MINMAXINFO);
            let dpi = GetDpiForWindow(hwnd).max(96) as i32;
            mmi.ptMinTrackSize = POINT { x: 520 * dpi / 96, y: 420 * dpi / 96 };
            0
        }
        WM_DPICHANGED => {
            let r = &*(l as *const RECT);
            let dpi = hiword(w as isize) as f32;
            with_app(|a| {
                a.canvas.set_scale(dpi / 96.0);
                a.model.icons.clear();
            });
            SetWindowPos(hwnd, 0, r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOZORDER | SWP_NOACTIVATE);
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        WM_SETTINGCHANGE | WM_THEMECHANGED | WM_DWMCOLORIZATIONCOLORCHANGED => {
            with_app(|a| {
                a.theme = Theme::load(a.theme.mica);
                apply_title_bar(hwnd, &a.theme);
            });
            InvalidateRect(hwnd, core::ptr::null(), 0);
            DefWindowProcW(hwnd, msg, w, l)
        }
        WM_CLOSE => {
            let destroy = with_app(|a| a.model.on_close_requested()).unwrap_or(true);
            if destroy {
                DestroyWindow(hwnd);
            } else {
                InvalidateRect(hwnd, core::ptr::null(), 0);
            }
            0
        }
        WM_DESTROY => {
            with_app(|a| a.model.tray.hide());
            PostQuitMessage(0);
            0
        }
        worker::WM_APP_SNAPSHOT => {
            with_app(|a| a.model.on_snapshot_message());
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        worker::WM_APP_POLL => {
            if with_app(|a| a.model.apply_poll()).unwrap_or(false) {
                InvalidateRect(hwnd, core::ptr::null(), 0);
            }
            0
        }
        worker::WM_APP_SCAN_DONE => {
            with_app(|a| a.model.on_scan_done());
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        input::WM_APP_HOTKEY_CAPTURED => {
            with_app(|a| a.model.on_hotkey_captured(w as i32));
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        WM_APP_UPDATE => {
            with_app(|a| a.model.on_update_result());
            InvalidateRect(hwnd, core::ptr::null(), 0);
            0
        }
        tray::WM_APP_TRAY => {
            // NOTIFYICON_VERSION_4: the event is in LOWORD(lParam).
            let ev = (l as u32) & 0xFFFF;
            if ev == WM_LBUTTONUP || ev == NIN_SELECT || ev == NIN_KEYSELECT {
                PostMessageW(hwnd, WM_APP_TRAY_COMMAND, tray::CMD_OPEN, 0);
            } else if ev == WM_CONTEXTMENU || ev == WM_RBUTTONUP {
                // TrackPopupMenu runs a modal loop: call it without holding the app borrow.
                let cmd = Tray::new(hwnd, 0).context_menu_detached();
                if cmd != 0 {
                    PostMessageW(hwnd, WM_APP_TRAY_COMMAND, cmd, 0);
                }
            }
            0
        }
        WM_APP_TRAY_COMMAND => {
            match w {
                tray::CMD_OPEN => {
                    with_app(|a| a.model.show_from_tray());
                }
                tray::CMD_EXIT => {
                    with_app(|a| a.model.close_confirmed = true);
                    PostMessageW(hwnd, WM_CLOSE, 0, 0);
                }
                100 => {
                    with_app(|a| a.model.hide_to_tray());
                }
                _ => {}
            }
            0
        }
        _ => {
            let tc = with_app(|a| a.taskbar_created).unwrap_or(0);
            if tc != 0 && msg == tc {
                with_app(|a| a.model.tray.recreate());
                return 0;
            }
            DefWindowProcW(hwnd, msg, w, l)
        }
    }
}

impl Tray {
    /// A menu-only helper that doesn't own the notification icon (dropping it must not remove the real one).
    fn context_menu_detached(self) -> usize {
        let cmd = self.context_menu();
        std::mem::forget(self);
        cmd
    }
}

// ──────────────────── App: input & painting ────────────────────

impl App {
    fn client_size(&self) -> (i32, i32) {
        let mut r = RECT::default();
        unsafe { GetClientRect(self.model.hwnd, &mut r) };
        (r.right - r.left, r.bottom - r.top)
    }

    fn paint(&mut self, dc: HDC) {
        let (w, h) = self.client_size();
        if w <= 0 || h <= 0 {
            return;
        }
        let started = std::time::Instant::now();
        self.canvas.resize(w, h);
        let bg = if self.theme.mica { Color::rgba(0, 0) } else { self.theme.bg };
        self.canvas.drawing = true;
        self.canvas.clear(bg);
        let repaint = self.pass(None);
        self.canvas.drawing = false;
        self.canvas.present(dc);
        debug_log(&format!("paint {}x{} {} us", w, h, started.elapsed().as_micros()));
        if repaint {
            unsafe { PostMessageW(self.model.hwnd, WM_APP_REPAINT, 0, 0) };
        }
    }

    /// Runs one frame without drawing. Returns whether a repaint is needed.
    fn pass(&mut self, ev: Option<Ev>) -> bool {
        let (w, h) = self.client_size();
        self.canvas.resize(w.max(1), h.max(1));
        if let Some(f) = self.model.focus_request.take() {
            self.st.focus = f;
            self.st.caret_visible = true;
        }
        let before_hot = self.st.hot;
        let (repaint, leftover) = {
            let mut ui = Ui::new(&mut self.canvas, &self.theme, &mut self.st, self.mx, self.my, ev);
            self.model.frame(&mut ui, w, h);
            ui.finish();
            (ui.repaint, ui.ev)
        };
        let mut need = repaint || !self.model.actions.is_empty();
        if let Some(ev) = leftover {
            need |= self.model.unhandled_event(ev);
        }
        self.model.run_actions();
        if self.st.hot != before_hot {
            unsafe { SetTimer(self.model.hwnd, TIMER_TOOLTIP, 650, core::ptr::null()) };
            need = true;
        }
        // Caret blink only while a text box has focus.
        let text_focus = matches!(self.model.dialog, Some(Dialog::Rename { .. }) | Some(Dialog::AppSelect { mode: PickMode::Manual, .. }));
        unsafe {
            if text_focus {
                SetTimer(self.model.hwnd, TIMER_CARET, GetCaretBlinkTime().clamp(200, 1200), core::ptr::null());
            } else {
                KillTimer(self.model.hwnd, TIMER_CARET);
                self.st.focus = 0;
            }
        }
        need
    }

    fn invalidate(&self) {
        unsafe { InvalidateRect(self.model.hwnd, core::ptr::null(), 0) };
    }

    fn mouse_move(&mut self, x: i32, y: i32) {
        if !self.tracking && x >= 0 {
            let mut t = TRACKMOUSEEVENT { cbSize: core::mem::size_of::<TRACKMOUSEEVENT>() as u32, dwFlags: TME_LEAVE, hwndTrack: self.model.hwnd, dwHoverTime: 0 };
            unsafe { TrackMouseEvent(&mut t) };
            self.tracking = true;
        }
        self.mx = x;
        self.my = y;
        let dragging = self.st.active != 0;
        let hot = self.st.hot;
        let repaint = self.pass(None);
        if dragging || repaint || hot != self.st.hot {
            self.invalidate();
        }
    }

    /// Returns true if the event was used.
    fn input(&mut self, ev: Ev) -> bool {
        self.pass(None); // bring `hot` up to date for the current mouse position
        debug_log(&format!("input {ev:?} at {},{} hot={:x} active={:x}", self.mx, self.my, self.st.hot, self.st.active));
        let used = self.pass(Some(ev));
        self.pass(None);
        self.invalidate();
        used || !matches!(ev, Ev::Key { .. })
    }

    fn timer(&mut self, id: usize) {
        match id {
            TIMER_TOOLTIP => unsafe {
                KillTimer(self.model.hwnd, TIMER_TOOLTIP);
                self.invalidate();
            },
            TIMER_CARET => {
                self.st.caret_visible = !self.st.caret_visible;
                self.invalidate();
            }
            TIMER_DEFER_SNAPSHOT => {
                self.model.try_apply_pending_snapshot();
                self.invalidate();
            }
            TIMER_CHOOSE => {
                unsafe { KillTimer(self.model.hwnd, TIMER_CHOOSE) };
                if self.model.choose_token.take().is_some() {
                    self.model.open_app_selection();
                }
                self.invalidate();
            }
            _ => {}
        }
    }
}

// ──────────────────── Model: audio snapshots ────────────────────

impl Model {
    fn cfg(&self) -> &Config {
        &self.configs[self.current]
    }
    fn cfg_mut(&mut self) -> &mut Config {
        &mut self.configs[self.current]
    }

    fn user_recently_interacted(&self) -> bool {
        let now = tick_ms();
        self.rows_master.iter().chain(self.rows_apps.iter()).any(|r| now.saturating_sub(r.last_interaction) < USER_INTERACTION_QUIET_MS)
    }

    fn on_snapshot_message(&mut self) {
        let Some(snap) = self.worker.snapshot() else { return };
        if snap.generation == self.snapshot.generation {
            return;
        }
        // Don't rebuild rows out from under a slider drag; apply once the user has paused.
        if self.user_recently_interacted() {
            self.pending_snapshot = Some(snap);
            unsafe { SetTimer(self.hwnd, TIMER_DEFER_SNAPSHOT, 300, core::ptr::null()) };
            return;
        }
        self.apply_snapshot(snap);
    }

    fn try_apply_pending_snapshot(&mut self) {
        if self.pending_snapshot.is_none() {
            unsafe { KillTimer(self.hwnd, TIMER_DEFER_SNAPSHOT) };
            return;
        }
        if self.user_recently_interacted() {
            return;
        }
        unsafe { KillTimer(self.hwnd, TIMER_DEFER_SNAPSHOT) };
        if let Some(s) = self.pending_snapshot.take() {
            self.apply_snapshot(s);
        }
    }

    fn apply_snapshot(&mut self, snap: Arc<Snapshot>) {
        self.pending_snapshot = None;
        self.snapshot = snap;
        // A device that was missing at startup may have come back under a new id.
        self.reconcile_config_devices();
        if self.pending_rebind_notice.is_some() {
            self.show_rebind_notice_if_needed();
        }
        self.sync_profiles();
        // While hidden in the tray only hotkeys matter; skip UI work until the window is shown.
        if self.visible {
            self.refresh_ui_from_audio();
        } else {
            self.ui_stale = true;
        }
    }

    fn refresh_ui_from_audio(&mut self) {
        self.ui_stale = false;
        self.populate_devices();
        self.refresh_session_picker();
        self.rebuild_rows();
        if let Some(d) = &self.dropdown {
            // Lists in an open dropdown may be stale now; close it rather than show wrong entries.
            if d.kind != DropKind::Profile {
                self.dropdown = None;
            }
        }
    }

    fn on_scan_done(&mut self) {
        let done = self.worker.take_completed_scans();
        if let Some(tok) = self.choose_token {
            if done.contains(&tok) {
                self.choose_token = None;
                unsafe { KillTimer(self.hwnd, TIMER_CHOOSE) };
                if let Some(s) = self.worker.snapshot() {
                    if s.generation != self.snapshot.generation {
                        self.apply_snapshot(s);
                    }
                }
                self.open_app_selection();
            }
        }
    }

    fn apply_poll(&mut self) -> bool {
        let Some(results) = self.worker.take_poll_results() else { return false };
        let now = tick_ms();
        let mut changed = false;
        for r in results {
            let Some(row) = self.rows_master.iter_mut().chain(self.rows_apps.iter_mut()).find(|row| row.key == r.key) else { continue };
            if let Some(v) = r.volume {
                if now.saturating_sub(row.last_interaction) > SLIDER_COOLDOWN_MS {
                    let p = (v.clamp(0.0, 1.0) * 100.0).round() as i32;
                    if p != row.percent {
                        row.percent = p;
                        changed = true;
                    }
                }
            }
            if let Some(m) = r.muted {
                if m != row.muted {
                    row.muted = m;
                    changed = true;
                }
            }
        }
        changed
    }

    fn update_poll_targets(&self) {
        let mut targets = Vec::new();
        for r in self.rows_master.iter().chain(self.rows_apps.iter()) {
            if let Some(t) = r.target() {
                targets.push((r.key, t));
            }
        }
        self.worker.set_poll_targets(targets);
    }

    fn sync_profiles(&self) {
        self.worker.set_profiles(self.configs.clone());
        self.input.set_profiles(
            self.configs.iter().map(|c| HotkeyProfile { config_id: c.config_id.clone(), hotkeys: c.hotkeys.clone() }).collect(),
        );
    }

    // ──────────────────── Rows ────────────────────

    fn rebuild_rows(&mut self) {
        self.rows_master.clear();
        self.rows_apps.clear();
        self.row_gen += 1;
        let gen = self.row_gen << 20;
        let snap = self.snapshot.clone();
        if snap.devices.is_empty() || self.device_index >= snap.devices.len() {
            self.update_poll_targets();
            return;
        }
        let device = &snap.devices[self.device_index];
        let cfg = self.cfg().clone();

        self.rows_master.push(Row {
            key: gen,
            is_master: true,
            title: "Volume".into(),
            detail: None,
            exe_path: None,
            device_id: device.id.clone(),
            app: None,
            handles: Vec::new(),
            target_unavailable: false,
            is_hotkey_target: cfg.control_device_master_volume,
            percent: (device.master_volume * 100.0).round() as i32,
            muted: device.master_muted,
            last_interaction: 0,
        });

        struct S<'a> {
            name: &'a str,
            pid: u32,
            app: &'a AppInfo,
            s: &'a SessionInfo,
        }
        let mut list: Vec<S> = Vec::new();
        for app in &device.apps {
            for s in &app.sessions {
                list.push(S { name: &app.app_friendly_name, pid: s.pid, app, s });
            }
        }
        list.sort_by(|a, b| b.s.is_system.cmp(&a.s.is_system).then_with(|| cmp_ic(a.name, b.name)).then(a.pid.cmp(&b.pid)));

        // Group by (name, pid) preserving order.
        let mut groups: Vec<(String, u32, Vec<&S>)> = Vec::new();
        for item in &list {
            match groups.iter_mut().find(|g| g.0 == item.name && g.1 == item.pid) {
                Some(g) => g.2.push(item),
                None => groups.push((item.name.to_string(), item.pid, vec![item])),
            }
        }
        let dup = |name: &str| groups.iter().filter(|g| eq_ic(&g.0, name)).count() > 1;

        let mut rows = Vec::new();
        for (i, (name, pid, members)) in groups.iter().enumerate() {
            let first = members[0];
            let detail = (dup(name) && *pid > 0).then(|| format!("PID {pid}"));
            let exe = if !first.s.is_system && first.s.pid != 0 { first.s.exe_path.clone() } else { None };
            rows.push(Row {
                key: gen | (i as u64 + 1),
                is_master: false,
                title: name.clone(),
                detail,
                exe_path: exe,
                device_id: device.id.clone(),
                app: Some(first.app.clone()),
                handles: members.iter().map(|m| m.s.handle).collect(),
                target_unavailable: false,
                is_hotkey_target: app_matches_config_on_device(first.app, &cfg),
                percent: (members[0].s.volume * 100.0).round() as i32,
                muted: members.iter().all(|m| m.s.muted),
                last_interaction: 0,
            });
        }

        // Keep the configured target visible even when it has no active session.
        if !cfg.control_device_master_volume && cfg.has_target() && !rows.iter().any(|r| r.is_hotkey_target) {
            let name = if is_blank(&cfg.app_friendly_name) { cfg.app_file_name.clone() } else { cfg.app_friendly_name.clone() };
            self.rows_apps.push(Row {
                key: gen | 0xFFFFF,
                is_master: false,
                title: name.clone(),
                detail: Some("Not currently active on this output.".into()),
                exe_path: self.icon_path_by_friendly_name(&cfg.app_friendly_name),
                device_id: device.id.clone(),
                app: Some(AppInfo { device_id: device.id.clone(), app_file_name: cfg.app_file_name.clone(), app_friendly_name: name, sessions: Vec::new() }),
                handles: Vec::new(),
                target_unavailable: true,
                is_hotkey_target: true,
                percent: 0,
                muted: false,
                last_interaction: 0,
            });
        }
        // The profile's target floats to the top; the rest keep System Sounds-first alphabetical order.
        let (targets, others): (Vec<Row>, Vec<Row>) = rows.into_iter().partition(|r| r.is_hotkey_target);
        self.rows_apps.extend(targets);
        self.rows_apps.extend(others);
        self.update_poll_targets();
    }

    fn recompute_highlights(&mut self) {
        let cfg = self.configs[self.current].clone();
        for r in &mut self.rows_master {
            r.is_hotkey_target = cfg.control_device_master_volume;
        }
        for r in &mut self.rows_apps {
            r.is_hotkey_target = r.target_unavailable || r.app.as_ref().is_some_and(|a| app_matches_config_on_device(a, &cfg));
        }
    }

    fn icon_path_by_friendly_name(&self, friendly: &str) -> Option<String> {
        let app = self.snapshot.all_apps().find(|a| eq_ic(&a.app_friendly_name, friendly))?;
        let s = app.sessions.first()?;
        if s.is_system || s.pid == 0 { None } else { s.exe_path.clone() }
    }

    // ──────────────────── Config / form ────────────────────

    fn device_friendly_name_for(&self, c: &Config) -> Option<String> {
        self.snapshot.devices.iter().find(|d| d.id == c.device_id).map(|d| d.friendly_name.clone())
    }

    fn display_name(&self, c: &Config) -> String {
        match c.custom_display_name.as_deref() {
            Some(n) if !is_blank(n) => n.trim().to_string(),
            _ => c.auto_display_name(self.device_friendly_name_for(c).as_deref()),
        }
    }

    fn load_config_to_form(&mut self) {
        self.populate_devices();
        self.refresh_session_picker();
        self.rebuild_rows();
        self.recompute_highlights();
    }

    fn populate_devices(&mut self) {
        let id = self.cfg().device_id.clone();
        self.device_index = self.snapshot.devices.iter().position(|d| d.id == id).unwrap_or(0);
    }

    fn refresh_session_picker(&mut self) {
        self.session_entries.clear();
        let cfg = self.cfg().clone();
        if cfg.control_device_master_volume || !cfg.has_target() {
            return;
        }
        let entries: Vec<String> = collect_matching_sessions(&self.snapshot, &cfg)
            .iter()
            .enumerate()
            .map(|(i, (s, dev))| format!("#{i} \u{2014} PID {} \u{2014} {dev}", s.pid))
            .collect();
        if entries.is_empty() {
            if cfg.control_single_session {
                self.cfg_mut().process_index = 0;
            }
            return;
        }
        let clamped = cfg.process_index.clamp(0, entries.len() as i32 - 1);
        if clamped != cfg.process_index {
            self.cfg_mut().process_index = clamped;
            self.cfg().save();
            self.sync_profiles();
        }
        self.session_entries = entries;
    }

    fn reconcile_config_devices(&mut self) -> bool {
        let live: Vec<DeviceIdentity> = self.snapshot.devices.iter().filter_map(|d| d.identity()).collect();
        if live.is_empty() || self.configs.is_empty() {
            return false;
        }
        let default_id = self.snapshot.default_device_id.clone();
        let fallback = || live.iter().find(|d| default_id.as_deref().is_some_and(|id| eq_ic(&d.id, id))).unwrap_or(&live[0]).clone();
        let mut rebound: Vec<(String, String)> = Vec::new();
        let mut any = false;
        for i in 0..self.configs.len() {
            match self.configs[i].reconcile_device(&live) {
                Reconcile::Backfilled | Reconcile::Rematched => {
                    self.configs[i].save();
                    any = true;
                }
                Reconcile::Orphaned => {
                    // Nothing to match on: bind to the default output and say so in the InfoBar.
                    let fb = fallback();
                    self.configs[i].set_device(&fb.id, &fb.friendly_name, fb.description.as_deref());
                    self.configs[i].save();
                    let c = self.configs[i].clone();
                    rebound.push((self.display_name(&c), fb.friendly_name.clone()));
                    any = true;
                }
                Reconcile::Unchanged => {}
            }
        }
        if !rebound.is_empty() {
            self.pending_rebind_notice = Some(build_rebind_notice(&rebound));
        }
        any
    }

    fn show_rebind_notice_if_needed(&mut self) {
        if let Some(n) = self.pending_rebind_notice.take() {
            self.infobar = Some(n);
        }
    }

    fn show_dialog(&mut self, d: Dialog) {
        if self.dialog.is_some() {
            self.dialog_queue.push_back(d);
            return;
        }
        self.focus_request = match &d {
            Dialog::Rename { .. } => Some(id("dlg-rename-edit")),
            Dialog::AppSelect { mode: PickMode::Manual, .. } => Some(id("dlg-app-edit")),
            _ => None,
        };
        self.dropdown = None;
        self.dialog = Some(d);
    }

    fn close_dialog(&mut self) {
        self.dialog = None;
        if let Some(next) = self.dialog_queue.pop_front() {
            self.show_dialog(next);
        }
    }

    fn message(&mut self, text: &str, title: &str) {
        self.show_dialog(Dialog::Message { title: title.into(), text: text.into() });
    }

    // ──────────────────── Tray / close ────────────────────

    fn hide_to_tray(&mut self) {
        unsafe { ShowWindow(self.hwnd, SW_HIDE) };
        self.tray.show();
        self.visible = false;
        self.dropdown = None;
        self.worker.set_polling(false);
    }

    fn show_from_tray(&mut self) {
        unsafe {
            if IsIconic(self.hwnd) != 0 {
                ShowWindow(self.hwnd, SW_RESTORE);
            } else {
                ShowWindow(self.hwnd, SW_SHOW);
            }
            SetForegroundWindow(self.hwnd);
        }
        self.tray.hide();
        let was_hidden = !self.visible;
        self.visible = true;
        self.worker.set_polling(true);
        if was_hidden {
            if self.pending_snapshot.is_some() {
                self.try_apply_pending_snapshot();
            } else if self.ui_stale {
                self.refresh_ui_from_audio();
            }
            self.worker.request_scan();
        }
        unsafe { InvalidateRect(self.hwnd, core::ptr::null(), 0) };
    }

    /// WM_CLOSE: returns true to destroy the window now.
    fn on_close_requested(&mut self) -> bool {
        if self.close_confirmed || data_flag_path().exists() {
            return true;
        }
        if !self.visible {
            self.show_from_tray();
        }
        if !matches!(self.dialog, Some(Dialog::CloseWarning { .. })) {
            self.show_dialog(Dialog::CloseWarning { dont_remind: false });
        }
        false
    }

    // ──────────────────── Event handlers ────────────────────

    fn on_hotkey_captured(&mut self, vk: i32) {
        if !self.listening_hotkey {
            return;
        }
        self.listening_hotkey = false;
        if self.cfg().hotkeys.contains(&vk) {
            self.message("This hotkey is already added.", "Duplicate hotkey");
            return;
        }
        self.cfg_mut().hotkeys.push(vk);
        self.cfg().save();
        self.sync_profiles();
    }

    fn on_update_result(&mut self) {
        let Some((r, silent)) = self.update_slot.lock().unwrap_or_else(|e| e.into_inner()).take() else { return };
        match r {
            UpdateResult::Available { latest, current, url } => self.show_dialog(Dialog::UpdateAvailable { latest, current, url }),
            UpdateResult::UpToDate { current } if !silent => {
                self.message(&format!("KBMixer {current} is the latest version."), "You're up to date")
            }
            UpdateResult::Failed(e) if !silent => self.message(&format!("Could not check for updates: {e}"), "Update check failed"),
            _ => {}
        }
    }

    /// Keys the widgets didn't use: Enter / Esc for dialogs and dropdowns.
    fn unhandled_event(&mut self, ev: Ev) -> bool {
        let Ev::Key { vk, .. } = ev else { return false };
        if vk == VK_ESCAPE && self.dropdown.is_some() {
            self.dropdown = None;
            return true;
        }
        if self.dialog.is_some() {
            match vk {
                VK_ESCAPE => {
                    self.actions.push(Action::Dialog(DialogButton::Close));
                    return true;
                }
                VK_RETURN => {
                    let b = if dialog_buttons(self.dialog.as_ref().unwrap()).iter().any(|(_, b, _)| *b == DialogButton::Primary) {
                        DialogButton::Primary
                    } else {
                        DialogButton::Close
                    };
                    self.actions.push(Action::Dialog(b));
                    return true;
                }
                _ => {}
            }
        }
        false
    }

    fn new_profile(&mut self, allow_without_device: bool) {
        let default_id = self.snapshot.default_device_id.clone();
        let dev = self
            .snapshot
            .devices
            .iter()
            .find(|d| default_id.as_deref().is_some_and(|id| eq_ic(&d.id, id)))
            .or(self.snapshot.devices.first())
            .and_then(|d| d.identity());
        if dev.is_none() && !allow_without_device {
            return;
        }
        let id = crate::com::guid_to_string(&crate::com::new_guid());
        let c = Config::new_empty(id, dev.as_ref());
        c.save();
        self.configs.push(c);
        self.current = self.configs.len() - 1;
        self.sync_profiles();
        self.load_config_to_form();
    }

    fn after_target_change(&mut self) {
        self.cfg().save();
        self.sync_profiles();
        self.refresh_session_picker();
        self.rebuild_rows();
        self.recompute_highlights();
    }

    fn open_app_selection(&mut self) {
        let cfg = self.cfg().clone();
        let mut items: Vec<PickItem> = Vec::new();
        for app in self.snapshot.all_apps() {
            if is_blank(&app.app_friendly_name) || items.iter().any(|i| eq_ic(&i.name, &app.app_friendly_name)) {
                continue;
            }
            let exe = app.sessions.first().filter(|s| !s.is_system && s.pid != 0).and_then(|s| s.exe_path.clone());
            items.push(PickItem { name: app.app_friendly_name.clone(), exe_path: exe });
        }
        items.sort_by(|a, b| cmp_ic(&a.name, &b.name));

        let (mode, selected, edit) = if cfg.control_device_master_volume {
            (PickMode::DeviceMaster, None, TextEdit::default())
        } else if is_blank(&cfg.app_friendly_name) {
            (PickMode::NoTarget, None, TextEdit::default())
        } else if let Some(i) = items.iter().position(|i| eq_ic(&i.name, &cfg.app_friendly_name)) {
            (PickMode::Select, Some(i), TextEdit::default())
        } else {
            (PickMode::Manual, None, TextEdit::new(&cfg.app_friendly_name))
        };
        self.show_dialog(Dialog::AppSelect { mode, items, selected, edit });
    }

    fn apply_app_selection(&mut self, mode: PickMode, result: Option<String>) {
        match mode {
            PickMode::NoTarget => {
                let c = self.cfg_mut();
                c.control_device_master_volume = false;
                c.app_file_name.clear();
                c.app_friendly_name.clear();
                c.hotkeys.clear();
                c.control_single_session = false;
                c.process_index = 0;
                self.after_target_change();
            }
            PickMode::DeviceMaster => {
                self.cfg_mut().control_device_master_volume = true;
                self.after_target_change();
            }
            PickMode::Select | PickMode::Manual => {
                let Some(result) = result else { return };
                let dev_id = self.cfg().device_id.clone();
                let snap = self.snapshot.clone();
                let on_dev = |a: &&AppInfo| eq_ic(&a.device_id, &dev_id);
                let by_friendly = |a: &&AppInfo| !is_blank(&a.app_friendly_name) && eq_ic(&a.app_friendly_name, &result);
                let by_file = |a: &&AppInfo| !is_blank(&a.app_file_name) && eq_ic(&a.app_file_name, &result);
                let m = snap
                    .all_apps()
                    .filter(on_dev)
                    .find(by_friendly)
                    .or_else(|| snap.all_apps().find(by_friendly))
                    .or_else(|| snap.all_apps().filter(on_dev).find(by_file))
                    .or_else(|| snap.all_apps().find(by_file));
                let (file, friendly) = match m {
                    Some(a) => (a.app_file_name.clone(), a.app_friendly_name.clone()),
                    None => (result.clone(), result.clone()),
                };
                let c = self.cfg_mut();
                c.control_device_master_volume = false;
                c.app_file_name = file;
                c.app_friendly_name = friendly;
                c.process_index = 0;
                self.after_target_change();
            }
        }
    }

    fn run_actions(&mut self) {
        let actions = std::mem::take(&mut self.actions);
        for a in actions {
            self.run_action(a);
        }
    }

    fn run_action(&mut self, a: Action) {
        debug_log(&format!("action {a:?}"));
        match a {
            Action::OpenDropdown(kind, anchor) => {
                let (items, selected) = match kind {
                    DropKind::Profile => (self.configs.iter().map(|c| self.display_name(c)).collect(), Some(self.current)),
                    DropKind::Device => (self.snapshot.devices.iter().map(|d| d.friendly_name.clone()).collect(), Some(self.device_index)),
                    DropKind::Session => {
                        // Opening the picker refreshes it and asks for a rescan, like DropDownOpened.
                        self.refresh_session_picker();
                        self.worker.request_scan();
                        (self.session_entries.clone(), Some(self.cfg().process_index.max(0) as usize))
                    }
                };
                if !items.is_empty() {
                    self.dropdown = Some(Dropdown { kind, items, selected, anchor });
                }
            }
            Action::SelectProfile(i) => {
                self.dropdown = None;
                if i < self.configs.len() && i != self.current {
                    self.current = i;
                    self.listening_hotkey = false;
                    self.input.capture_next_key.store(false, Ordering::Release);
                    self.load_config_to_form();
                }
            }
            Action::SelectDevice(i) => {
                self.dropdown = None;
                let Some(d) = self.snapshot.devices.get(i).cloned() else { return };
                self.device_index = i;
                self.cfg_mut().set_device(&d.id, &d.friendly_name, d.description.as_deref());
                self.cfg().save();
                self.sync_profiles();
                self.refresh_session_picker();
                self.rebuild_rows();
                self.recompute_highlights();
            }
            Action::SelectSession(i) => {
                self.dropdown = None;
                let n = collect_matching_sessions(&self.snapshot, self.cfg()).len();
                if n == 0 {
                    return;
                }
                self.cfg_mut().process_index = i.min(n - 1) as i32;
                self.cfg().save();
                self.sync_profiles();
            }
            Action::NewProfile => self.new_profile(false),
            Action::RenameProfile => {
                let auto = self.cfg().auto_display_name(self.device_friendly_name_for(self.cfg()).as_deref());
                let edit = TextEdit::new(self.cfg().custom_display_name.as_deref().unwrap_or(""));
                self.show_dialog(Dialog::Rename { edit, placeholder: auto });
            }
            Action::DeleteProfile => {
                if self.configs.len() == 1 {
                    self.message("Cannot delete the only remaining configuration.", "KBMixer");
                    return;
                }
                let removed = self.configs.remove(self.current);
                removed.delete();
                self.current = self.current.min(self.configs.len() - 1);
                self.sync_profiles();
                self.load_config_to_form();
            }
            Action::Choose => {
                // Rescan right now (up to 3 s) so the picker lists apps that just started playing.
                let tok = self.next_token;
                self.next_token += 1;
                self.choose_token = Some(tok);
                self.worker.scan_now(tok);
                unsafe { SetTimer(self.hwnd, TIMER_CHOOSE, 3000, core::ptr::null()) };
            }
            Action::AddHotkey => {
                self.listening_hotkey = true;
                self.input.capture_next_key.store(true, Ordering::Release);
            }
            Action::ResetHotkeys => {
                self.cfg_mut().hotkeys.clear();
                self.cfg().save();
                self.sync_profiles();
            }
            Action::ToggleSingleSession => {
                let v = !self.cfg().control_single_session;
                self.cfg_mut().control_single_session = v;
                self.refresh_session_picker();
                self.cfg().save();
                self.sync_profiles();
            }
            Action::RowMute(master, i) => {
                let rows = if master { &self.rows_master } else { &self.rows_apps };
                if let Some(t) = rows.get(i).and_then(|r| r.target()) {
                    // The worker toggles and then polls, which brings the new state back to the row.
                    self.worker.queue_toggle_mute(t);
                }
            }
            Action::RowTarget(i) => {
                let Some(app) = self.rows_apps.get(i).and_then(|r| r.app.clone()) else { return };
                let c = self.cfg_mut();
                c.control_device_master_volume = false;
                c.app_file_name = app.app_file_name;
                c.app_friendly_name = app.app_friendly_name;
                c.process_index = 0;
                self.after_target_change();
            }
            Action::Help => self.show_dialog(Dialog::Help),
            Action::Settings => {
                let startup = startup::is_registered_for_current_exe();
                self.show_dialog(Dialog::Settings { startup });
            }
            Action::CloseInfoBar => self.infobar = None,
            Action::ToggleStartup => {
                let Some(Dialog::Settings { startup }) = &mut self.dialog else { return };
                let want = !*startup;
                match startup::set_registered(want) {
                    Ok(()) => *startup = want,
                    Err(e) => self.dialog_queue.push_back(Dialog::Message {
                        title: "KBMixer".into(),
                        text: format!("Could not update the Windows startup setting.\n\n{e}"),
                    }),
                }
            }
            Action::CheckUpdates => {
                // Only one dialog at a time: close Settings before reporting.
                self.close_dialog();
                start_update_check(self.hwnd, self.update_slot.clone(), false);
            }
            Action::Dialog(b) => self.dialog_result(b),
        }
    }

    fn dialog_result(&mut self, b: DialogButton) {
        let Some(d) = self.dialog.take() else { return };
        match d {
            Dialog::CloseWarning { dont_remind } => {
                if dont_remind {
                    let _ = std::fs::create_dir_all(config::data_dir());
                    let _ = std::fs::write(data_flag_path(), "");
                }
                match b {
                    DialogButton::Primary => self.hide_to_tray(),
                    DialogButton::Secondary => {
                        self.close_confirmed = true;
                        unsafe { PostMessageW(self.hwnd, WM_CLOSE, 0, 0) };
                    }
                    DialogButton::Close => {}
                }
            }
            Dialog::Rename { edit, .. } if b == DialogButton::Primary => {
                let trimmed = edit.value().trim().to_string();
                let auto = self.cfg().auto_display_name(self.device_friendly_name_for(self.cfg()).as_deref());
                self.cfg_mut().custom_display_name = if trimmed.is_empty() || eq_ic(&trimmed, &auto) { None } else { Some(trimmed) };
                self.cfg().save();
            }
            Dialog::AppSelect { mode, items, selected, edit } if b == DialogButton::Primary => {
                let result = match mode {
                    PickMode::Select => selected.and_then(|i| items.get(i)).map(|i| i.name.clone()),
                    PickMode::Manual => Some(edit.value().trim().to_string()).filter(|s| !s.is_empty()),
                    _ => None,
                };
                self.close_dialog();
                self.apply_app_selection(mode, result);
                return;
            }
            Dialog::UpdateAvailable { url, .. } if b == DialogButton::Primary => shell_open(&url),
            _ => {}
        }
        self.close_dialog();
    }
}

fn build_rebind_notice(rebound: &[(String, String)]) -> String {
    let mut devices: Vec<&str> = Vec::new();
    for (_, d) in rebound {
        if !devices.contains(&d.as_str()) {
            devices.push(d);
        }
    }
    let mut sb = String::new();
    for dev in devices {
        let mut names: Vec<&str> = Vec::new();
        for (p, d) in rebound {
            if d == dev && !names.contains(&p.as_str()) {
                names.push(p);
            }
        }
        let list = match names.len() {
            1 => names[0].to_string(),
            2 => format!("{} and {}", names[0], names[1]),
            n => format!("{}, and {}", names[..n - 1].join(", "), names[n - 1]),
        };
        let verb = if names.len() == 1 { "was" } else { "were" };
        sb.push_str(&format!("{list} {verb} re-bound to {dev} because the original output device no longer exists. "));
    }
    sb.push_str("If that's wrong, select the profile and pick a different Output device.");
    sb
}

fn dialog_title(d: &Dialog) -> &'static str {
    match d {
        Dialog::Message { .. } => "",
        Dialog::Help => "How KBMixer works",
        Dialog::CloseWarning { .. } => "Close KBMixer?",
        Dialog::Rename { .. } => "Rename profile",
        Dialog::AppSelect { .. } => "Select a Volume Target",
        Dialog::Settings { .. } => "Settings",
        Dialog::UpdateAvailable { .. } => "Update available",
    }
}

/// (label, role, is accent)
fn dialog_buttons(d: &Dialog) -> Vec<(&'static str, DialogButton, bool)> {
    use DialogButton::*;
    match d {
        Dialog::Message { .. } => vec![("OK", Close, false)],
        Dialog::Help => vec![("Got it", Close, false)],
        Dialog::CloseWarning { .. } => vec![("Minimize to tray", Primary, true), ("Close anyway", Secondary, false), ("Cancel", Close, false)],
        Dialog::Rename { .. } => vec![("Save", Primary, true), ("Cancel", Close, false)],
        Dialog::AppSelect { .. } => vec![("OK", Primary, true), ("Cancel", Close, false)],
        Dialog::Settings { .. } => vec![("Close", Close, false)],
        Dialog::UpdateAvailable { .. } => vec![("Download", Primary, true), ("Later", Close, false)],
    }
}

// ──────────────────── Frame (layout + drawing) ────────────────────

impl Model {
    fn frame(&mut self, ui: &mut Ui, w: i32, h: i32) {
        let modal = self.dialog.is_some() || self.dropdown.is_some();
        ui.input_enabled = !modal;
        self.draw_main(ui, w, h);
        if self.dropdown.is_some() {
            ui.input_enabled = self.dialog.is_none();
            self.draw_dropdown(ui, w, h);
        }
        if self.dialog.is_some() {
            ui.input_enabled = true;
            self.draw_dialog(ui, w, h);
        }
        ui.draw_tooltip();
    }

    fn draw_main(&mut self, ui: &mut Ui, w: i32, h: i32) {
        let t = ui.t;
        let view = Rect::new(0, 0, w, h);
        let scroll_id = id("main-scroll");
        let scroll = ui.begin_scroll(scroll_id, view);

        let panel_w = w.min(ui.px(900.0));
        let pad = ui.px(36.0);
        let x = (w - panel_w) / 2 + pad;
        let cw = panel_w - 2 * pad;
        let mut y = ui.px(16.0) - scroll;

        // Title row + help / settings.
        let title_h = ui.px(40.0);
        ui.label(Rect::new(x, y, cw - ui.px(80.0), title_h), "KBMixer", Font::Title, t.text);
        let b = ui.px(32.0);
        let settings_r = Rect::new(x + cw - b, y + (title_h - b) / 2, b, b);
        let help_r = Rect::new(settings_r.x - b - ui.px(4.0), settings_r.y, b, b);
        if ui.icon_button(id("help"), help_r, GLYPH_HELP, 16, true, "How KBMixer works") {
            self.actions.push(Action::Help);
        }
        if ui.icon_button(id("settings"), settings_r, GLYPH_SETTINGS, 16, true, "Settings") {
            self.actions.push(Action::Settings);
        }
        y += title_h + ui.px(4.0);
        let sub = "Control per-app and system-wide volume levels using your mouse scroll wheel and custom hotkeys from anywhere.";
        let sh = ui.c.measure_wrapped(sub, Font::Caption, cw);
        ui.wrapped(Rect::new(x, y, cw, sh), sub, Font::Caption, t.text2);
        y += sh + ui.px(20.0);

        if let Some(msg) = self.infobar.clone() {
            y += self.draw_infobar(ui, Rect::new(x, y, cw, 0), &msg) + ui.px(14.0);
        }

        y += self.draw_profile_card(ui, x, y, cw) + ui.px(14.0);
        y += self.draw_mixer_card(ui, x, y, cw);
        y += ui.px(36.0);

        ui.end_scroll(scroll_id, view, y + scroll);
    }

    fn draw_infobar(&mut self, ui: &mut Ui, r: Rect, msg: &str) -> i32 {
        let t = ui.t;
        let title = "Profiles re-bound to a different output device";
        let pad = ui.px(16.0);
        let icon = ui.px(20.0);
        let close = ui.px(32.0);
        let tx = r.x + pad + icon + ui.px(12.0);
        let tw = r.right() - close - ui.px(8.0) - tx;
        let (_, th) = ui.c.measure(title, Font::BodyStrong);
        let mh = ui.c.measure_wrapped(msg, Font::Body, tw);
        let h = (ui.px(14.0) * 2 + th + ui.px(4.0) + mh).max(ui.px(48.0));
        let box_r = Rect::new(r.x, r.y, r.w, h);
        let rad = ui.px(4.0) as f32;
        ui.c.fill_round_rect(box_r, rad, t.info_bg);
        ui.c.stroke_round_rect(box_r, rad, 1.0, t.card_stroke);
        let cy = r.y + ui.px(14.0) + th / 2;
        ui.c.fill_circle((r.x + pad + icon / 2) as f32, cy as f32, icon as f32 / 2.0 - 2.0, t.accent);
        ui.c.glyph(Rect::new(r.x + pad, cy - icon / 2, icon, icon), GLYPH_INFO, 14, t.on_accent);
        ui.label(Rect::new(tx, r.y + ui.px(14.0), tw, th), title, Font::BodyStrong, t.text);
        ui.wrapped(Rect::new(tx, r.y + ui.px(14.0) + th + ui.px(4.0), tw, mh), msg, Font::Body, t.text);
        let cr = Rect::new(r.right() - close - ui.px(8.0), r.y + ui.px(8.0), close, close);
        if ui.icon_button(id("infobar-close"), cr, GLYPH_CLOSE, 12, true, "Close") {
            self.actions.push(Action::CloseInfoBar);
        }
        h
    }

    fn card(ui: &mut Ui, r: Rect) {
        let t = ui.t;
        let rad = ui.px(8.0) as f32;
        ui.c.fill_round_rect(r, rad, t.card);
        ui.c.stroke_round_rect(r, rad, 1.0, t.card_stroke);
    }

    /// Draws the card background behind content of a known height (computed with a dry run first).
    fn with_card(&mut self, ui: &mut Ui, x: i32, y: i32, w: i32, pad: (f32, f32, f32), body: impl Fn(&mut Self, &mut Ui, i32, i32, i32) -> i32) -> i32 {
        let px = ui.px(pad.0);
        let pt = ui.px(pad.1);
        let pb = ui.px(pad.2);
        let was = ui.c.drawing;
        let input = ui.input_enabled;
        let ev = ui.ev;
        ui.c.drawing = false;
        ui.input_enabled = false;
        ui.ev = None;
        let measured = body(self, ui, x + px, y + pt, w - 2 * px);
        ui.c.drawing = was;
        ui.input_enabled = input;
        ui.ev = ev;
        let h = pt + measured + pb;
        Self::card(ui, Rect::new(x, y, w, h));
        body(self, ui, x + px, y + pt, w - 2 * px);
        h
    }

    fn draw_profile_card(&mut self, ui: &mut Ui, x: i32, y: i32, w: i32) -> i32 {
        self.with_card(ui, x, y, w, (20.0, 18.0, 18.0), |m, ui, x, y, w| m.profile_body(ui, x, y, w))
    }

    fn profile_body(&mut self, ui: &mut Ui, x: i32, y0: i32, w: i32) -> i32 {
        let t = ui.t;
        let mut y = y0;
        let (_, hh) = ui.c.measure("Profile", Font::BodyStrong);
        ui.label(Rect::new(x, y, w, hh), "Profile", Font::BodyStrong, t.text);
        y += hh + ui.px(2.0);
        let cap = "Start at the top and work down: each profile stores its output, target, and controls.";
        let ch = ui.c.measure_wrapped(cap, Font::Caption, w);
        ui.wrapped(Rect::new(x, y, w, ch), cap, Font::Caption, t.text3);
        y += ch + ui.px(16.0);

        let cfg = self.cfg().clone();
        let row_h = ui.px(32.0);
        let gap = ui.px(12.0);
        let col0 = ui.px(110.0);
        let btn_w = |ui: &mut Ui, s: &str| ui.c.measure(s, Font::Body).0.max(0) + ui.px(28.0);
        let bw = |ui: &mut Ui, s: &str, min: f32| btn_w(ui, s).max(ui.px(min));
        let new_w = bw(ui, "New", 72.0);
        let ren_w = bw(ui, "Rename", 82.0);
        let del_w = bw(ui, "Delete", 72.0);
        let choose_w = bw(ui, "Choose\u{2026}", 72.0);
        let add_label = if self.listening_hotkey { "Listening\u{2026}" } else { "Add" };
        let add_w = bw(ui, add_label, 72.0);
        let reset_w = bw(ui, "Reset", 72.0);
        let s8 = ui.px(8.0);
        let col2 = (new_w + ren_w + del_w + 2 * s8).max(choose_w).max(add_w + reset_w + s8);
        let col1 = (w - col0 - col2 - 2 * gap).max(ui.px(80.0));
        let x1 = x + col0 + gap;
        let x2 = x1 + col1 + gap;
        let label = |ui: &mut Ui, y: i32, s: &str, opacity: f32| {
            ui.label(Rect::new(x, y, col0, row_h), s, Font::Body, t.text2.with_alpha(opacity));
        };

        // Row 0: profile
        label(ui, y, "Profile", 1.0);
        let combo_r = Rect::new(x1, y, col1, row_h);
        let open = self.dropdown.as_ref().is_some_and(|d| d.kind == DropKind::Profile);
        let name = self.display_name(&cfg);
        if ui.combo(id("combo-profile"), combo_r, &name, true, open) {
            self.actions.push(Action::OpenDropdown(DropKind::Profile, combo_r));
        }
        let mut bx = x2;
        if ui.button(id("btn-new"), Rect::new(bx, y, new_w, row_h), "New", true) {
            self.actions.push(Action::NewProfile);
        }
        bx += new_w + s8;
        if ui.button(id("btn-rename"), Rect::new(bx, y, ren_w, row_h), "Rename", true) {
            self.actions.push(Action::RenameProfile);
        }
        bx += ren_w + s8;
        if ui.button(id("btn-delete"), Rect::new(bx, y, del_w, row_h), "Delete", true) {
            self.actions.push(Action::DeleteProfile);
        }
        y += row_h + gap;

        // Row 1: output device
        label(ui, y, "Output device", 1.0);
        let dev_r = Rect::new(x1, y, col1 + gap + col2, row_h);
        let dev_name = self.snapshot.devices.get(self.device_index).map(|d| d.friendly_name.clone()).unwrap_or_default();
        let open = self.dropdown.as_ref().is_some_and(|d| d.kind == DropKind::Device);
        if ui.combo(id("combo-device"), dev_r, &dev_name, !self.snapshot.devices.is_empty(), open) {
            self.actions.push(Action::OpenDropdown(DropKind::Device, dev_r));
        }
        y += row_h + gap;

        // Row 2: target
        label(ui, y, "Target", 1.0);
        let icon_s = ui.px(20.0);
        let ir = Rect::new(x1, y + (row_h - icon_s) / 2, icon_s, icon_s);
        let (target_text, show_master_icon, icon_path) = if cfg.control_device_master_volume {
            ("Device master volume".to_string(), true, None)
        } else if !cfg.has_target() {
            ("Choose an app or device master volume".to_string(), false, None)
        } else {
            let n = if is_blank(&cfg.app_friendly_name) { cfg.app_file_name.clone() } else { cfg.app_friendly_name.clone() };
            (n, false, self.icon_path_by_friendly_name(&cfg.app_friendly_name))
        };
        if show_master_icon {
            ui.c.glyph(ir, GLYPH_VOLUME, 16, t.text);
        } else if let Some(p) = icon_path {
            if ui.c.drawing {
                if let Some(img) = self.icons.for_exe(ui.c, &p, icon_s) {
                    ui.image(ir, &img, 1.0);
                }
            }
        }
        let tx = x1 + icon_s + s8;
        ui.label(Rect::new(tx, y, x1 + col1 - tx, row_h), &target_text, Font::Body, t.text);
        if ui.button(id("btn-choose"), Rect::new(x2, y, choose_w, row_h), "Choose\u{2026}", true) {
            self.actions.push(Action::Choose);
        }
        y += row_h + gap;

        // Row 3: hotkeys
        label(ui, y, "Hotkeys", 1.0);
        ui.readonly_box(Rect::new(x1, y, col1, row_h), &keys::join(&cfg.hotkeys));
        let has_target = cfg.has_target();
        if ui.button(id("btn-add"), Rect::new(x2, y, add_w, row_h), add_label, has_target && !self.listening_hotkey) {
            self.actions.push(Action::AddHotkey);
        }
        if ui.button(id("btn-reset"), Rect::new(x2 + add_w + s8, y, reset_w, row_h), "Reset", has_target || !cfg.hotkeys.is_empty()) {
            self.actions.push(Action::ResetHotkeys);
        }
        y += row_h + gap;

        // Row 4: session
        let app_mode = has_target && !cfg.control_device_master_volume;
        label(ui, y, "Session", if app_mode { 1.0 } else { 0.45 });
        let cb_r = Rect::new(x1, y, col1 + gap + col2, row_h);
        if ui.checkbox(id("cb-single"), cb_r, "Control a specific session when the app has multiple mixer entries", cfg.control_single_session, app_mode) {
            self.actions.push(Action::ToggleSingleSession);
        }
        y += row_h + gap;

        // Row 5: session picker
        let sp_r = Rect::new(x, y, w, row_h);
        let enabled = app_mode && cfg.control_single_session && !self.session_entries.is_empty();
        let text = if app_mode {
            self.session_entries.get(cfg.process_index.max(0) as usize).cloned().unwrap_or_default()
        } else {
            String::new()
        };
        let open = self.dropdown.as_ref().is_some_and(|d| d.kind == DropKind::Session);
        if ui.combo(id("combo-session"), sp_r, &text, enabled, open) {
            self.actions.push(Action::OpenDropdown(DropKind::Session, sp_r));
        }
        y += row_h;
        y - y0
    }

    fn draw_mixer_card(&mut self, ui: &mut Ui, x: i32, y: i32, w: i32) -> i32 {
        self.with_card(ui, x, y, w, (20.0, 18.0, 14.0), |m, ui, x, y, w| m.mixer_body(ui, x, y, w))
    }

    fn mixer_body(&mut self, ui: &mut Ui, x: i32, y0: i32, w: i32) -> i32 {
        let t = ui.t;
        let mut y = y0;
        let (_, hh) = ui.c.measure("Mixer", Font::BodyStrong);
        ui.label(Rect::new(x, y, w, hh), "Mixer", Font::BodyStrong, t.text);
        y += hh + ui.px(10.0);
        for i in 0..self.rows_master.len() {
            y += self.draw_row(ui, true, i, x, y, w);
        }
        y += ui.px(14.0);
        ui.label(Rect::new(x, y, w, hh), "Apps", Font::BodyStrong, t.text);
        y += hh + ui.px(8.0);
        for i in 0..self.rows_apps.len() {
            y += self.draw_row(ui, false, i, x, y, w);
        }
        y - y0
    }

    fn draw_row(&mut self, ui: &mut Ui, master: bool, i: usize, x: i32, y: i32, w: i32) -> i32 {
        let t = ui.t;
        let row = if master { &self.rows_master[i] } else { &self.rows_apps[i] };
        let has_detail = row.detail.is_some();
        let h = if has_detail { ui.px(52.0) } else { ui.px(44.0) };
        let r = Rect::new(x, y, w, h);
        // Skip work for rows scrolled out of view.
        if r.bottom() < 0 || r.y > ui.c.height() {
            return h;
        }
        if row.is_hotkey_target {
            ui.c.fill_round_rect(r, ui.px(4.0) as f32, t.highlight_row);
            ui.c.fill_round_rect(Rect::new(x, y, ui.px(4.0), h), ui.px(2.0) as f32, t.accent);
        }
        let key = row.key;
        let pad = ui.px(8.0);
        let sp = ui.px(8.0);
        let cols = [28.0, 36.0, 140.0, 24.0, 40.0].map(|v| ui.px(v));
        let mut cx = [0i32; 6];
        cx[0] = x + pad;
        for c in 1..6 {
            cx[c] = cx[c - 1] + cols[c - 1] + sp;
        }
        let inner_y = y + pad;
        let inner_h = h - 2 * pad;
        let slider_w = (x + w - pad - ui.px(8.0) - cx[5]).max(ui.px(160.0));
        let vol_enabled = row.volume_enabled();
        let opacity = if row.muted { 0.38 } else { 1.0 };
        let glyph = if row.muted { GLYPH_MUTED } else { GLYPH_VOLUME };
        let tip = row.mute_tooltip();
        let title = row.title.clone();
        let detail = row.detail.clone();
        let exe = row.exe_path.clone();
        let percent = row.percent;
        let is_target = row.is_hotkey_target;

        if master {
            let mr = Rect::new(cx[1], y + (h - ui.px(28.0)) / 2, ui.px(28.0), ui.px(28.0));
            if ui.icon_button(id_n("mute", key), mr, glyph, 18, vol_enabled, tip) {
                self.actions.push(Action::RowMute(true, i));
            }
            ui.label(Rect::new(cx[2], inner_y, cols[2], inner_h), &title, Font::BodyStrong, t.text);
        } else {
            let rr = Rect::new(cx[0], inner_y, cols[0], inner_h);
            let s20 = ui.px(20.0);
            let radio_r = Rect::new(rr.x + (rr.w - s20) / 2, rr.y, s20, rr.h);
            if ui.radio(id_n("radio", key), radio_r, "", is_target, true) {
                self.actions.push(Action::RowTarget(i));
            }
            ui.set_tooltip(id_n("radio", key), radio_r, "Use this app as the profile target");
            let icon_s = ui.px(28.0);
            let ir = Rect::new(cx[1] + (cols[1] - icon_s) / 2, y + (h - icon_s) / 2, icon_s, icon_s);
            let mut drew = false;
            if let Some(p) = &exe {
                if ui.c.drawing {
                    if let Some(img) = self.icons.for_exe(ui.c, p, icon_s) {
                        ui.image(ir, &img, 1.0);
                        drew = true;
                    }
                } else {
                    drew = true;
                }
            }
            if !drew {
                ui.c.glyph(ir, GLYPH_APP, 14, t.text3);
            }
            let (_, th) = ui.c.measure("Ag", Font::BodyStrong);
            if let Some(d) = &detail {
                let (_, dh) = ui.c.measure("Ag", Font::Caption);
                let ty = y + (h - th - dh) / 2;
                ui.label(Rect::new(cx[2], ty, cols[2], th), &title, Font::BodyStrong, t.text);
                ui.label(Rect::new(cx[2], ty + th, cols[2], dh), d, Font::Caption, t.text3);
            } else {
                ui.label(Rect::new(cx[2], inner_y, cols[2], inner_h), &title, Font::BodyStrong, t.text);
            }
            let mr = Rect::new(cx[3], y + (h - ui.px(28.0)) / 2, cols[3], ui.px(28.0));
            if ui.icon_button(id_n("mute", key), mr, glyph, 16, vol_enabled, tip) {
                self.actions.push(Action::RowMute(false, i));
            }
        }

        let pr = Rect::new(cx[4], inner_y, cols[4], inner_h);
        ui.c.text(pr, &percent.to_string(), Font::Body, t.text.with_alpha(opacity), DT_RIGHT | DT_VCENTER | DT_SINGLELINE);

        let sr = Rect::new(cx[5], inner_y, slider_w, inner_h);
        let slider_enabled = master || vol_enabled;
        if let Some(v) = ui.slider(id_n("slider", key), sr, percent, slider_enabled, opacity) {
            let row = if master { &mut self.rows_master[i] } else { &mut self.rows_apps[i] };
            row.percent = v;
            row.last_interaction = tick_ms();
            if let Some(target) = row.target() {
                // Coalesced on the worker: a fast drag only ever applies its latest position.
                self.worker.queue_set_volume(row.key, target, v as f32 / 100.0);
            }
        }
        h
    }

    fn draw_dropdown(&mut self, ui: &mut Ui, w: i32, h: i32) {
        let t = ui.t;
        let Some(dd) = &self.dropdown else { return };
        let item_h = ui.px(36.0);
        let pad = ui.px(4.0);
        let margin = ui.px(8.0);
        let want = dd.items.len() as i32 * item_h + 2 * pad;
        let below = h - dd.anchor.bottom() - margin;
        let above = dd.anchor.y - margin;
        let (y, ph) = if want <= below || below >= above {
            (dd.anchor.bottom() + ui.px(2.0), want.min(below.max(item_h + 2 * pad)))
        } else {
            let ph = want.min(above);
            (dd.anchor.y - ph - ui.px(2.0), ph)
        };
        let x = dd.anchor.x.clamp(0, (w - dd.anchor.w).max(0));
        let r = Rect::new(x, y, dd.anchor.w, ph);
        let rad = ui.px(8.0) as f32;
        ui.c.fill_round_rect(Rect::new(r.x, r.y + ui.px(2.0), r.w, r.h), rad, Color::rgba(0, 0x30));
        ui.c.fill_round_rect(r, rad, t.flyout);
        ui.c.stroke_round_rect(r, rad, 1.0, t.card_stroke);

        let view = r.inset(0, pad);
        let sid = id("dropdown-scroll");
        let scroll = ui.begin_scroll(sid, view);
        let kind = dd.kind;
        let selected = dd.selected;
        let items = dd.items.clone();
        let mut picked = None;
        for (i, item) in items.iter().enumerate() {
            let ir = Rect::new(r.x + pad, view.y + i as i32 * item_h - scroll, r.w - 2 * pad, item_h);
            if ir.bottom() < view.y || ir.y > view.bottom() {
                continue;
            }
            let it = ui.interact(id_n("dd-item", i as u64), ir, true);
            let rad = ui.px(4.0) as f32;
            let sel = selected == Some(i);
            if it.pressed {
                ui.c.fill_round_rect(ir, rad, t.subtle_pressed);
            } else if it.hovered || sel {
                ui.c.fill_round_rect(ir, rad, t.subtle_hover);
            }
            if sel {
                let ph = ui.px(16.0);
                ui.c.fill_round_rect(Rect::new(ir.x, ir.y + (ir.h - ph) / 2, ui.px(3.0), ph), ui.px(1.5) as f32, t.accent);
            }
            ui.label(Rect::new(ir.x + ui.px(12.0), ir.y, ir.w - ui.px(20.0), ir.h), item, Font::Body, t.text);
            if it.clicked {
                picked = Some(i);
            }
        }
        ui.end_scroll(sid, view, items.len() as i32 * item_h);

        if let Some(i) = picked {
            self.actions.push(match kind {
                DropKind::Profile => Action::SelectProfile(i),
                DropKind::Device => Action::SelectDevice(i),
                DropKind::Session => Action::SelectSession(i),
            });
            self.dropdown = None;
            ui.st.scroll.remove(&sid);
        } else if matches!(ui.ev, Some(Ev::MouseDown)) && !r.contains(ui.mx, ui.my) {
            // Light dismiss.
            ui.take_event();
            self.dropdown = None;
            ui.st.scroll.remove(&sid);
        } else if matches!(ui.ev, Some(Ev::Wheel(_))) && !r.contains(ui.mx, ui.my) {
            ui.take_event();
        }
    }

    fn draw_dialog(&mut self, ui: &mut Ui, w: i32, h: i32) {
        let t = ui.t;
        // Smoke layer over the whole window.
        ui.c.fill_rect(Rect::new(0, 0, w, h), t.smoke);
        let Some(d) = &self.dialog else { return };
        let title = match d {
            Dialog::Message { title, .. } => title.clone(),
            other => dialog_title(other).to_string(),
        };
        let buttons = dialog_buttons(d);

        let dw = ui.px(548.0).min(w - ui.px(32.0)).max(ui.px(200.0));
        let pad = ui.px(24.0);
        let cw = dw - 2 * pad;
        let title_h = ui.c.measure_wrapped(&title, Font::Subtitle, cw);

        // Measure content.
        let (was, input, ev) = (ui.c.drawing, ui.input_enabled, ui.ev);
        ui.c.drawing = false;
        ui.input_enabled = false;
        ui.ev = None;
        let content_h = self.dialog_content(ui, 0, 0, cw);
        ui.c.drawing = was;
        ui.input_enabled = input;
        ui.ev = ev;

        let footer_h = ui.px(80.0);
        let max_content = (h - ui.px(32.0) - footer_h - 2 * pad - title_h - ui.px(12.0)).max(ui.px(60.0));
        let view_h = content_h.min(max_content);
        let dh = pad + title_h + ui.px(12.0) + view_h + pad + footer_h;
        let dx = (w - dw) / 2;
        let dy = ((h - dh) / 2).max(ui.px(16.0));
        let card = Rect::new(dx, dy, dw, dh);
        let rad = ui.px(8.0) as f32;

        ui.c.fill_round_rect(card, rad, t.dialog_bg);
        let footer = Rect::new(dx, card.bottom() - footer_h, dw, footer_h);
        ui.c.push_clip(footer);
        ui.c.fill_round_rect(card, rad, t.dialog_footer);
        ui.c.pop_clip();
        ui.c.fill_rect(Rect::new(dx, footer.y, dw, 1), t.card_stroke);
        ui.c.stroke_round_rect(card, rad, 1.0, t.card_stroke);

        let mut y = dy + pad;
        if !title.is_empty() {
            ui.wrapped(Rect::new(dx + pad, y, cw, title_h), &title, Font::Subtitle, t.text);
        }
        y += title_h + ui.px(12.0);

        let view = Rect::new(dx + pad, y, cw + ui.px(12.0), view_h);
        let sid = id("dialog-scroll");
        let scroll = ui.begin_scroll(sid, view);
        self.dialog_content(ui, dx + pad, y - scroll, cw);
        ui.end_scroll(sid, view, content_h);

        // Footer buttons share the width evenly; a lone button takes the right half like ContentDialog.
        let bh = ui.px(32.0);
        let by = footer.y + (footer_h - bh) / 2;
        let gap = ui.px(8.0);
        let n = buttons.len().max(2) as i32;
        let bw = (cw - gap * (n - 1)) / n;
        let start = if buttons.len() == 1 { dx + pad + bw + gap } else { dx + pad };
        for (i, (label, role, accent)) in buttons.iter().enumerate() {
            let br = Rect::new(start + i as i32 * (bw + gap), by, bw, bh);
            let bid = id_n("dlg-btn", i as u64);
            let clicked = if *accent { ui.accent_button(bid, br, label, true) } else { ui.button(bid, br, label, true) };
            if clicked {
                self.actions.push(Action::Dialog(*role));
            }
        }

        // Clicks on the smoke don't dismiss a ContentDialog, but must not reach the page either.
        if matches!(ui.ev, Some(Ev::MouseDown) | Some(Ev::MouseUp)) {
            ui.take_event();
        }
        if matches!(ui.ev, Some(Ev::Wheel(_))) {
            ui.take_event();
        }
    }

    /// Lays out (and draws, when the canvas is drawing) the dialog body; returns its height.
    fn dialog_content(&mut self, ui: &mut Ui, x: i32, y0: i32, w: i32) -> i32 {
        let t = ui.t;
        let mut y = y0;
        let row_h = ui.px(32.0);
        let s8 = ui.px(8.0);
        let mut acts = Vec::new();
        match self.dialog.as_mut() {
            None => {}
            Some(Dialog::Message { text, .. }) => {
                let h = ui.c.measure_wrapped(text, Font::Body, w);
                ui.wrapped(Rect::new(x, y, w, h), text, Font::Body, t.text);
                y += h;
            }
            Some(Dialog::UpdateAvailable { latest, current, .. }) => {
                let s = format!("KBMixer {latest} is available (you have {current}).");
                let h = ui.c.measure_wrapped(&s, Font::Body, w);
                ui.wrapped(Rect::new(x, y, w, h), &s, Font::Body, t.text);
                y += h;
            }
            Some(Dialog::Help) => {
                let steps = [
                    "Pick a profile, device, and hotkeys (click Add, then press keys).",
                    "Choose desired app to control or device master volume.",
                    "Hold your hotkeys and scroll the mouse wheel to change volume anywhere.",
                    "Keep KBMixer running; minimize to the notification area.",
                ];
                for (i, s) in steps.iter().enumerate() {
                    let line = format!("{}. {s}", i + 1);
                    let h = ui.c.measure_wrapped(&line, Font::Body, w);
                    ui.wrapped(Rect::new(x, y, w, h), &line, Font::Body, t.text2);
                    y += h + ui.px(10.0);
                }
                y -= ui.px(10.0);
            }
            Some(Dialog::CloseWarning { dont_remind }) => {
                let s = "KBMixer needs to be running in the background for your hotkeys and volume control to work. Would you like to minimize to the system tray instead?";
                let h = ui.c.measure_wrapped(s, Font::Body, w);
                ui.wrapped(Rect::new(x, y, w, h), s, Font::Body, t.text);
                y += h + ui.px(12.0);
                if ui.checkbox(id("dlg-close-cb"), Rect::new(x, y, w, row_h), "Don't remind me again", *dont_remind, true) {
                    *dont_remind = !*dont_remind;
                }
                y += row_h;
            }
            Some(Dialog::Rename { edit, placeholder }) => {
                let s = "Give this profile a custom name. Leave it blank to use the automatic name.";
                let h = ui.c.measure_wrapped(s, Font::Body, w);
                ui.wrapped(Rect::new(x, y, w, h), s, Font::Body, t.text);
                y += h + s8;
                if ui.text_box(id("dlg-rename-edit"), Rect::new(x, y, w, row_h), edit, placeholder) {
                    acts.push(Action::Dialog(DialogButton::Primary));
                }
                y += row_h;
            }
            Some(Dialog::Settings { startup }) => {
                let s = "Open KBMixer when Windows starts (minimized to the notification area)";
                let lw = w - ui.px(28.0);
                let lh = ui.c.measure_wrapped(s, Font::Body, lw).max(row_h);
                let cbr = Rect::new(x, y, w, lh);
                if ui.checkbox(id("dlg-startup"), Rect::new(x, y, ui.px(20.0), lh), "", *startup, true) {
                    acts.push(Action::ToggleStartup);
                }
                let it = ui.interact(id("dlg-startup-label"), Rect::new(x + ui.px(28.0), y, lw, lh), true);
                if it.clicked {
                    acts.push(Action::ToggleStartup);
                }
                let th = ui.c.measure_wrapped(s, Font::Body, lw);
                ui.wrapped(Rect::new(x + ui.px(28.0), y + (lh - th) / 2, lw, th), s, Font::Body, t.text);
                let _ = cbr;
                y += lh + ui.px(16.0);
                let label = "Check for updates";
                let bw = ui.c.measure(label, Font::Body).0 + ui.px(28.0);
                if ui.button(id("dlg-check-updates"), Rect::new(x, y, bw, row_h), label, true) {
                    acts.push(Action::CheckUpdates);
                }
                let v = format!("KBMixer {}", update::version_string(update::current_version()));
                ui.label(Rect::new(x + bw + ui.px(12.0), y, w - bw - ui.px(12.0), row_h), &v, Font::Body, t.text2);
                y += row_h;
            }
            Some(Dialog::AppSelect { mode, items, selected, edit }) => {
                let radios = [
                    (PickMode::NoTarget, "Leave this profile unconfigured for now"),
                    (PickMode::Select, "Choose from apps currently playing audio on this PC"),
                    (PickMode::Manual, "Enter an executable name manually (e.g. chrome.exe)"),
                    (PickMode::DeviceMaster, "Control device master volume (all apps on this output)"),
                ];
                for (i, (m, label)) in radios.iter().enumerate() {
                    if ui.radio(id_n("dlg-mode", i as u64), Rect::new(x, y, w, row_h), label, *mode == *m, true) && *mode != *m {
                        *mode = *m;
                        if *m == PickMode::Manual {
                            self.focus_request = Some(id("dlg-app-edit"));
                        }
                    }
                    y += row_h + s8;
                }
                y += ui.px(4.0) - s8;
                ui.c.fill_rect(Rect::new(x, y + ui.px(4.0), w, 1), t.divider);
                y += ui.px(9.0) + s8;
                match mode {
                    PickMode::Select => {
                        let item_h = ui.px(40.0);
                        let list_h = (items.len() as i32 * item_h).min(ui.px(280.0)).max(item_h);
                        let view = Rect::new(x, y, w, list_h);
                        let sid = id("dlg-app-list");
                        let scroll = ui.begin_scroll(sid, view);
                        for (i, item) in items.iter().enumerate() {
                            let ir = Rect::new(x, y + i as i32 * item_h - scroll, w - ui.px(10.0), item_h);
                            if ir.bottom() < view.y || ir.y > view.bottom() {
                                continue;
                            }
                            let it = ui.interact(id_n("dlg-app", i as u64), ir, true);
                            let rad = ui.px(4.0) as f32;
                            let sel = *selected == Some(i);
                            if it.pressed {
                                ui.c.fill_round_rect(ir, rad, t.subtle_pressed);
                            } else if it.hovered || sel {
                                ui.c.fill_round_rect(ir, rad, t.subtle_hover);
                            }
                            if sel {
                                let ph = ui.px(16.0);
                                ui.c.fill_round_rect(Rect::new(ir.x, ir.y + (ir.h - ph) / 2, ui.px(3.0), ph), ui.px(1.5) as f32, t.accent);
                            }
                            let s24 = ui.px(24.0);
                            let icr = Rect::new(ir.x + ui.px(12.0), ir.y + (ir.h - s24) / 2, s24, s24);
                            let mut drew = false;
                            if let (Some(p), true) = (&item.exe_path, ui.c.drawing) {
                                if let Some(img) = self.icons.for_exe(ui.c, p, s24) {
                                    ui.image(icr, &img, 1.0);
                                    drew = true;
                                }
                            }
                            if !drew {
                                ui.c.glyph(icr, GLYPH_APP, 14, t.text3);
                            }
                            let lx = icr.right() + ui.px(10.0);
                            ui.label(Rect::new(lx, ir.y, ir.right() - lx, ir.h), &item.name, Font::Body, t.text);
                            if it.clicked {
                                *selected = Some(i);
                            }
                        }
                        ui.end_scroll(sid, view, items.len() as i32 * item_h);
                        y += list_h;
                    }
                    PickMode::Manual => {
                        if ui.text_box(id("dlg-app-edit"), Rect::new(x, y, w, row_h), edit, "e.g. chrome.exe") {
                            acts.push(Action::Dialog(DialogButton::Primary));
                        }
                        y += row_h;
                    }
                    _ => {
                        y -= ui.px(9.0) + s8;
                    }
                }
            }
        }
        self.actions.extend(acts);
        y - y0
    }
}
