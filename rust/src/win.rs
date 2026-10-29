//! Hand-written Win32 FFI: just the types, constants and functions KBMixer uses. No crates.
#![allow(non_snake_case, non_camel_case_types, clippy::upper_case_acronyms, dead_code)]

use core::ffi::c_void;

pub type BOOL = i32;
pub type HRESULT = i32;
pub type HANDLE = isize;
pub type HWND = isize;
pub type HINSTANCE = isize;
pub type HICON = isize;
pub type HCURSOR = isize;
pub type HBRUSH = isize;
pub type HDC = isize;
pub type HGDIOBJ = isize;
pub type HBITMAP = isize;
pub type HFONT = isize;
pub type HMENU = isize;
pub type HKEY = isize;
pub type HMONITOR = isize;
pub type HRAWINPUT = isize;
pub type WPARAM = usize;
pub type LPARAM = isize;
pub type LRESULT = isize;
pub type PCWSTR = *const u16;
pub type PWSTR = *mut u16;
pub type WNDPROC = unsafe extern "system" fn(HWND, u32, WPARAM, LPARAM) -> LRESULT;

#[repr(C)]
#[derive(Clone, Copy, PartialEq, Eq, Default, Debug)]
pub struct GUID {
    pub data1: u32,
    pub data2: u16,
    pub data3: u16,
    pub data4: [u8; 8],
}

impl GUID {
    pub const fn from_u128(v: u128) -> GUID {
        GUID {
            data1: (v >> 96) as u32,
            data2: (v >> 80) as u16,
            data3: (v >> 64) as u16,
            data4: (v as u64).to_be_bytes(),
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Default, Debug, PartialEq, Eq)]
pub struct POINT {
    pub x: i32,
    pub y: i32,
}

#[repr(C)]
#[derive(Clone, Copy, Default, Debug, PartialEq, Eq)]
pub struct RECT {
    pub left: i32,
    pub top: i32,
    pub right: i32,
    pub bottom: i32,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct SIZE {
    pub cx: i32,
    pub cy: i32,
}

#[repr(C)]
pub struct WNDCLASSEXW {
    pub cbSize: u32,
    pub style: u32,
    pub lpfnWndProc: Option<WNDPROC>,
    pub cbClsExtra: i32,
    pub cbWndExtra: i32,
    pub hInstance: HINSTANCE,
    pub hIcon: HICON,
    pub hCursor: HCURSOR,
    pub hbrBackground: HBRUSH,
    pub lpszMenuName: PCWSTR,
    pub lpszClassName: PCWSTR,
    pub hIconSm: HICON,
}

#[repr(C)]
pub struct MSG {
    pub hwnd: HWND,
    pub message: u32,
    pub wParam: WPARAM,
    pub lParam: LPARAM,
    pub time: u32,
    pub pt: POINT,
    pub lPrivate: u32,
}

#[repr(C)]
pub struct PAINTSTRUCT {
    pub hdc: HDC,
    pub fErase: BOOL,
    pub rcPaint: RECT,
    pub fRestore: BOOL,
    pub fIncUpdate: BOOL,
    pub rgbReserved: [u8; 32],
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct BITMAPINFOHEADER {
    pub biSize: u32,
    pub biWidth: i32,
    pub biHeight: i32,
    pub biPlanes: u16,
    pub biBitCount: u16,
    pub biCompression: u32,
    pub biSizeImage: u32,
    pub biXPelsPerMeter: i32,
    pub biYPelsPerMeter: i32,
    pub biClrUsed: u32,
    pub biClrImportant: u32,
}

#[repr(C)]
pub struct BITMAPINFO {
    pub bmiHeader: BITMAPINFOHEADER,
    pub bmiColors: [u32; 1],
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct LOGFONTW {
    pub lfHeight: i32,
    pub lfWidth: i32,
    pub lfEscapement: i32,
    pub lfOrientation: i32,
    pub lfWeight: i32,
    pub lfItalic: u8,
    pub lfUnderline: u8,
    pub lfStrikeOut: u8,
    pub lfCharSet: u8,
    pub lfOutPrecision: u8,
    pub lfClipPrecision: u8,
    pub lfQuality: u8,
    pub lfPitchAndFamily: u8,
    pub lfFaceName: [u16; 32],
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct RAWINPUTDEVICE {
    pub usUsagePage: u16,
    pub usUsage: u16,
    pub dwFlags: u32,
    pub hwndTarget: HWND,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct RAWINPUTHEADER {
    pub dwType: u32,
    pub dwSize: u32,
    pub hDevice: HANDLE,
    pub wParam: WPARAM,
}

#[repr(C)]
pub struct NOTIFYICONDATAW {
    pub cbSize: u32,
    pub hWnd: HWND,
    pub uID: u32,
    pub uFlags: u32,
    pub uCallbackMessage: u32,
    pub hIcon: HICON,
    pub szTip: [u16; 128],
    pub dwState: u32,
    pub dwStateMask: u32,
    pub szInfo: [u16; 256],
    pub uVersion: u32,
    pub szInfoTitle: [u16; 64],
    pub dwInfoFlags: u32,
    pub guidItem: GUID,
    pub hBalloonIcon: HICON,
}

#[repr(C)]
pub struct TRACKMOUSEEVENT {
    pub cbSize: u32,
    pub dwFlags: u32,
    pub hwndTrack: HWND,
    pub dwHoverTime: u32,
}

#[repr(C)]
pub struct PROCESSENTRY32W {
    pub dwSize: u32,
    pub cntUsage: u32,
    pub th32ProcessID: u32,
    pub th32DefaultHeapID: usize,
    pub th32ModuleID: u32,
    pub cntThreads: u32,
    pub th32ParentProcessID: u32,
    pub pcPriClassBase: i32,
    pub dwFlags: u32,
    pub szExeFile: [u16; 260],
}

#[repr(C)]
pub struct MONITORINFO {
    pub cbSize: u32,
    pub rcMonitor: RECT,
    pub rcWork: RECT,
    pub dwFlags: u32,
}

#[repr(C)]
pub struct MARGINS {
    pub cxLeftWidth: i32,
    pub cxRightWidth: i32,
    pub cyTopHeight: i32,
    pub cyBottomHeight: i32,
}

#[repr(C)]
pub struct MINMAXINFO {
    pub ptReserved: POINT,
    pub ptMaxSize: POINT,
    pub ptMaxPosition: POINT,
    pub ptMinTrackSize: POINT,
    pub ptMaxTrackSize: POINT,
}

#[repr(C)]
pub struct VS_FIXEDFILEINFO {
    pub dwSignature: u32,
    pub dwStrucVersion: u32,
    pub dwFileVersionMS: u32,
    pub dwFileVersionLS: u32,
    pub dwProductVersionMS: u32,
    pub dwProductVersionLS: u32,
    pub dwFileFlagsMask: u32,
    pub dwFileFlags: u32,
    pub dwFileOS: u32,
    pub dwFileType: u32,
    pub dwFileSubtype: u32,
    pub dwFileDateMS: u32,
    pub dwFileDateLS: u32,
}

#[repr(C)]
pub struct SHFILEINFOW {
    pub hIcon: HICON,
    pub iIcon: i32,
    pub dwAttributes: u32,
    pub szDisplayName: [u16; 260],
    pub szTypeName: [u16; 80],
}

// ───── Messages ─────
pub const WM_CREATE: u32 = 0x0001;
pub const WM_DESTROY: u32 = 0x0002;
pub const WM_SIZE: u32 = 0x0005;
pub const WM_ACTIVATE: u32 = 0x0006;
pub const WM_SETFOCUS: u32 = 0x0007;
pub const WM_KILLFOCUS: u32 = 0x0008;
pub const WM_PAINT: u32 = 0x000F;
pub const WM_CLOSE: u32 = 0x0010;
pub const WM_QUIT: u32 = 0x0012;
pub const WM_ERASEBKGND: u32 = 0x0014;
pub const WM_SHOWWINDOW: u32 = 0x0018;
pub const WM_SETTINGCHANGE: u32 = 0x001A;
pub const WM_SETCURSOR: u32 = 0x0020;
pub const WM_GETMINMAXINFO: u32 = 0x0024;
pub const WM_NCCREATE: u32 = 0x0081;
pub const WM_INPUT: u32 = 0x00FF;
pub const WM_KEYDOWN: u32 = 0x0100;
pub const WM_KEYUP: u32 = 0x0101;
pub const WM_CHAR: u32 = 0x0102;
pub const WM_SYSKEYDOWN: u32 = 0x0104;
pub const WM_COMMAND: u32 = 0x0111;
pub const WM_SYSCOMMAND: u32 = 0x0112;
pub const WM_TIMER: u32 = 0x0113;
pub const WM_MOUSEMOVE: u32 = 0x0200;
pub const WM_LBUTTONDOWN: u32 = 0x0201;
pub const WM_LBUTTONUP: u32 = 0x0202;
pub const WM_LBUTTONDBLCLK: u32 = 0x0203;
pub const WM_RBUTTONUP: u32 = 0x0205;
pub const WM_MOUSEWHEEL: u32 = 0x020A;
pub const WM_CAPTURECHANGED: u32 = 0x0215;
pub const WM_MOUSELEAVE: u32 = 0x02A3;
pub const WM_DPICHANGED: u32 = 0x02E0;
pub const WM_THEMECHANGED: u32 = 0x031A;
pub const WM_DWMCOLORIZATIONCOLORCHANGED: u32 = 0x0320;
pub const WM_APP: u32 = 0x8000;
pub const WM_CONTEXTMENU: u32 = 0x007B;
pub const WM_NULL: u32 = 0x0000;

pub const SIZE_MINIMIZED: usize = 1;
pub const SC_MINIMIZE: usize = 0xF020;

// ───── Window styles ─────
pub const WS_OVERLAPPEDWINDOW: u32 = 0x00CF0000;
pub const WS_POPUP: u32 = 0x80000000;
pub const WS_CLIPCHILDREN: u32 = 0x02000000;
pub const WS_EX_TOOLWINDOW: u32 = 0x00000080;
pub const WS_EX_NOACTIVATE: u32 = 0x08000000;
pub const CS_HREDRAW: u32 = 0x0002;
pub const CS_VREDRAW: u32 = 0x0001;
pub const CS_DBLCLKS: u32 = 0x0008;
pub const CW_USEDEFAULT: i32 = 0x80000000u32 as i32;
pub const GWLP_USERDATA: i32 = -21;

pub const SW_HIDE: i32 = 0;
pub const SW_SHOWNORMAL: i32 = 1;
pub const SW_SHOW: i32 = 5;
pub const SW_RESTORE: i32 = 9;

pub const SWP_NOSIZE: u32 = 0x0001;
pub const SWP_NOMOVE: u32 = 0x0002;
pub const SWP_NOZORDER: u32 = 0x0004;
pub const SWP_NOACTIVATE: u32 = 0x0010;
pub const SWP_FRAMECHANGED: u32 = 0x0020;

pub const IDC_ARROW: usize = 32512;
pub const IDC_IBEAM: usize = 32513;
pub const IDC_HAND: usize = 32649;
pub const HTCLIENT: u16 = 1;

pub const IMAGE_ICON: u32 = 1;
pub const LR_DEFAULTCOLOR: u32 = 0;
pub const LR_SHARED: u32 = 0x8000;
pub const DI_NORMAL: u32 = 0x0003;

pub const TME_LEAVE: u32 = 0x00000002;
pub const MK_LBUTTON: usize = 0x0001;

pub const MONITOR_DEFAULTTONEAREST: u32 = 2;
pub const DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2: isize = -4;
pub const USER_DEFAULT_SCREEN_DPI: u32 = 96;

pub const VK_BACK: u32 = 0x08;
pub const VK_TAB: u32 = 0x09;
pub const VK_RETURN: u32 = 0x0D;
pub const VK_SHIFT: u32 = 0x10;
pub const VK_CONTROL: u32 = 0x11;
pub const VK_ESCAPE: u32 = 0x1B;
pub const VK_END: u32 = 0x23;
pub const VK_HOME: u32 = 0x24;
pub const VK_LEFT: u32 = 0x25;
pub const VK_UP: u32 = 0x26;
pub const VK_RIGHT: u32 = 0x27;
pub const VK_DOWN: u32 = 0x28;
pub const VK_DELETE: u32 = 0x2E;

// ───── Raw input ─────
pub const RID_INPUT: u32 = 0x10000003;
pub const RIM_TYPEMOUSE: u32 = 0;
pub const RIM_TYPEKEYBOARD: u32 = 1;
pub const RIDEV_REMOVE: u32 = 0x00000001;
pub const RIDEV_INPUTSINK: u32 = 0x00000100;
pub const RI_KEY_BREAK: u16 = 0x0001;
pub const RI_MOUSE_WHEEL: u16 = 0x0400;

// ───── GDI ─────
pub const BI_RGB: u32 = 0;
pub const DIB_RGB_COLORS: u32 = 0;
pub const SRCCOPY: u32 = 0x00CC0020;
pub const TRANSPARENT: i32 = 1;
pub const FW_NORMAL: i32 = 400;
pub const FW_SEMIBOLD: i32 = 600;
pub const DEFAULT_CHARSET: u8 = 1;
pub const OUT_TT_PRECIS: u8 = 4;
pub const CLIP_DEFAULT_PRECIS: u8 = 0;
pub const CLEARTYPE_QUALITY: u8 = 5;
pub const ANTIALIASED_QUALITY: u8 = 4;

pub const DT_LEFT: u32 = 0x0000;
pub const DT_CENTER: u32 = 0x0001;
pub const DT_RIGHT: u32 = 0x0002;
pub const DT_VCENTER: u32 = 0x0004;
pub const DT_WORDBREAK: u32 = 0x0010;
pub const DT_SINGLELINE: u32 = 0x0020;
pub const DT_NOCLIP: u32 = 0x0100;
pub const DT_CALCRECT: u32 = 0x0400;
pub const DT_NOPREFIX: u32 = 0x0800;
pub const DT_END_ELLIPSIS: u32 = 0x8000;

// ───── Menus / tray ─────
pub const MF_STRING: u32 = 0x0000;
pub const MF_SEPARATOR: u32 = 0x0800;
pub const TPM_RIGHTBUTTON: u32 = 0x0002;
pub const TPM_RETURNCMD: u32 = 0x0100;
pub const TPM_BOTTOMALIGN: u32 = 0x0020;
pub const NIM_ADD: u32 = 0;
pub const NIM_MODIFY: u32 = 1;
pub const NIM_DELETE: u32 = 2;
pub const NIM_SETVERSION: u32 = 4;
pub const NIF_MESSAGE: u32 = 0x1;
pub const NIF_ICON: u32 = 0x2;
pub const NIF_TIP: u32 = 0x4;
pub const NIF_SHOWTIP: u32 = 0x80;
pub const NOTIFYICON_VERSION_4: u32 = 4;
pub const NIN_SELECT: u32 = 0x0400;
pub const NIN_KEYSELECT: u32 = 0x0401;

// ───── Kernel / process ─────
pub const PROCESS_QUERY_LIMITED_INFORMATION: u32 = 0x1000;
pub const TH32CS_SNAPPROCESS: u32 = 0x00000002;
pub const INVALID_HANDLE_VALUE: HANDLE = -1;
pub const THREAD_PRIORITY_ABOVE_NORMAL: i32 = 1;
pub const COINIT_APARTMENTTHREADED: u32 = 0x2;
pub const COINIT_MULTITHREADED: u32 = 0x0;
pub const CLSCTX_ALL: u32 = 0x17;
pub const CF_UNICODETEXT: u32 = 13;
pub const GMEM_MOVEABLE: u32 = 0x0002;

// ───── Registry ─────
pub const HKEY_CURRENT_USER: HKEY = 0x80000001u32 as i32 as isize;
pub const KEY_READ: u32 = 0x20019;
pub const KEY_WRITE: u32 = 0x20006;
pub const REG_SZ: u32 = 1;
pub const REG_DWORD: u32 = 4;
pub const RRF_RT_REG_SZ: u32 = 0x00000002;
pub const RRF_RT_REG_DWORD: u32 = 0x00000010;
pub const ERROR_SUCCESS: i32 = 0;

// ───── DWM ─────
pub const DWMWA_USE_IMMERSIVE_DARK_MODE: u32 = 20;
pub const DWMWA_CAPTION_COLOR: u32 = 35;
pub const DWMWA_SYSTEMBACKDROP_TYPE: u32 = 38;
pub const DWMSBT_MAINWINDOW: u32 = 2;

pub const SHGFI_ICON: u32 = 0x100;
pub const SHGFI_LARGEICON: u32 = 0x0;

#[link(name = "kernel32")]
extern "system" {
    pub fn GetModuleHandleW(name: PCWSTR) -> HINSTANCE;
    pub fn OpenProcess(access: u32, inherit: BOOL, pid: u32) -> HANDLE;
    pub fn CloseHandle(h: HANDLE) -> BOOL;
    pub fn QueryFullProcessImageNameW(h: HANDLE, flags: u32, name: PWSTR, size: *mut u32) -> BOOL;
    pub fn CreateToolhelp32Snapshot(flags: u32, pid: u32) -> HANDLE;
    pub fn Process32FirstW(snap: HANDLE, entry: *mut PROCESSENTRY32W) -> BOOL;
    pub fn Process32NextW(snap: HANDLE, entry: *mut PROCESSENTRY32W) -> BOOL;
    pub fn GetTickCount64() -> u64;
    pub fn GetCurrentThread() -> HANDLE;
    pub fn SetThreadPriority(h: HANDLE, priority: i32) -> BOOL;
    pub fn GlobalAlloc(flags: u32, bytes: usize) -> HANDLE;
    pub fn GlobalLock(h: HANDLE) -> *mut c_void;
    pub fn GlobalUnlock(h: HANDLE) -> BOOL;
    pub fn GlobalFree(h: HANDLE) -> HANDLE;
    pub fn GetLastError() -> u32;
    pub fn OutputDebugStringW(s: PCWSTR);
    pub fn FormatMessageW(flags: u32, src: *const c_void, id: u32, lang: u32, buf: PWSTR, size: u32, args: *const c_void) -> u32;
    pub fn GetModuleHandleExW(flags: u32, name: PCWSTR, out: *mut HINSTANCE) -> BOOL;
}

#[link(name = "user32")]
extern "system" {
    pub fn RegisterClassExW(wc: *const WNDCLASSEXW) -> u16;
    pub fn CreateWindowExW(
        ex_style: u32, class: PCWSTR, title: PCWSTR, style: u32, x: i32, y: i32, w: i32, h: i32,
        parent: HWND, menu: HMENU, inst: HINSTANCE, param: *const c_void,
    ) -> HWND;
    pub fn DefWindowProcW(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT;
    pub fn DestroyWindow(hwnd: HWND) -> BOOL;
    pub fn ShowWindow(hwnd: HWND, cmd: i32) -> BOOL;
    pub fn GetMessageW(msg: *mut MSG, hwnd: HWND, min: u32, max: u32) -> BOOL;
    pub fn TranslateMessage(msg: *const MSG) -> BOOL;
    pub fn DispatchMessageW(msg: *const MSG) -> LRESULT;
    pub fn PostMessageW(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> BOOL;
    pub fn PostQuitMessage(code: i32);
    pub fn LoadCursorW(inst: HINSTANCE, name: usize) -> HCURSOR;
    pub fn SetCursor(c: HCURSOR) -> HCURSOR;
    pub fn LoadImageW(inst: HINSTANCE, name: usize, ty: u32, cx: i32, cy: i32, flags: u32) -> HANDLE;
    pub fn GetClientRect(hwnd: HWND, r: *mut RECT) -> BOOL;
    pub fn GetWindowRect(hwnd: HWND, r: *mut RECT) -> BOOL;
    pub fn SetWindowPos(hwnd: HWND, after: HWND, x: i32, y: i32, w: i32, h: i32, flags: u32) -> BOOL;
    pub fn InvalidateRect(hwnd: HWND, r: *const RECT, erase: BOOL) -> BOOL;
    pub fn BeginPaint(hwnd: HWND, ps: *mut PAINTSTRUCT) -> HDC;
    pub fn EndPaint(hwnd: HWND, ps: *const PAINTSTRUCT) -> BOOL;
    pub fn GetDC(hwnd: HWND) -> HDC;
    pub fn ReleaseDC(hwnd: HWND, dc: HDC) -> i32;
    pub fn SetCapture(hwnd: HWND) -> HWND;
    pub fn ReleaseCapture() -> BOOL;
    pub fn TrackMouseEvent(t: *mut TRACKMOUSEEVENT) -> BOOL;
    pub fn SetTimer(hwnd: HWND, id: usize, ms: u32, f: *const c_void) -> usize;
    pub fn KillTimer(hwnd: HWND, id: usize) -> BOOL;
    pub fn GetDpiForWindow(hwnd: HWND) -> u32;
    pub fn GetDpiForSystem() -> u32;
    pub fn SetProcessDpiAwarenessContext(ctx: isize) -> BOOL;
    pub fn AdjustWindowRectExForDpi(r: *mut RECT, style: u32, menu: BOOL, ex: u32, dpi: u32) -> BOOL;
    pub fn RegisterRawInputDevices(devs: *const RAWINPUTDEVICE, n: u32, size: u32) -> BOOL;
    pub fn GetRawInputData(h: HRAWINPUT, cmd: u32, data: *mut c_void, size: *mut u32, header: u32) -> u32;
    pub fn RegisterWindowMessageW(name: PCWSTR) -> u32;
    pub fn CreatePopupMenu() -> HMENU;
    pub fn AppendMenuW(m: HMENU, flags: u32, id: usize, text: PCWSTR) -> BOOL;
    pub fn TrackPopupMenu(m: HMENU, flags: u32, x: i32, y: i32, r: i32, hwnd: HWND, rect: *const RECT) -> BOOL;
    pub fn DestroyMenu(m: HMENU) -> BOOL;
    pub fn SetForegroundWindow(hwnd: HWND) -> BOOL;
    pub fn GetCursorPos(p: *mut POINT) -> BOOL;
    pub fn ScreenToClient(hwnd: HWND, p: *mut POINT) -> BOOL;
    pub fn IsWindowVisible(hwnd: HWND) -> BOOL;
    pub fn IsIconic(hwnd: HWND) -> BOOL;
    pub fn OpenClipboard(hwnd: HWND) -> BOOL;
    pub fn CloseClipboard() -> BOOL;
    pub fn GetClipboardData(fmt: u32) -> HANDLE;
    pub fn EmptyClipboard() -> BOOL;
    pub fn SetClipboardData(fmt: u32, h: HANDLE) -> HANDLE;
    pub fn DrawIconEx(dc: HDC, x: i32, y: i32, icon: HICON, cx: i32, cy: i32, step: u32, brush: HBRUSH, flags: u32) -> BOOL;
    pub fn DestroyIcon(icon: HICON) -> BOOL;
    pub fn PrivateExtractIconsW(file: PCWSTR, idx: i32, cx: i32, cy: i32, icons: *mut HICON, ids: *mut u32, n: u32, flags: u32) -> u32;
    pub fn SetWindowLongPtrW(hwnd: HWND, idx: i32, v: isize) -> isize;
    pub fn GetWindowLongPtrW(hwnd: HWND, idx: i32) -> isize;
    pub fn GetKeyState(vk: i32) -> i16;
    pub fn MonitorFromWindow(hwnd: HWND, flags: u32) -> HMONITOR;
    pub fn GetMonitorInfoW(m: HMONITOR, info: *mut MONITORINFO) -> BOOL;
    pub fn SetFocus(hwnd: HWND) -> HWND;
    pub fn GetDoubleClickTime() -> u32;
    pub fn GetCaretBlinkTime() -> u32;
    pub fn DrawTextW(dc: HDC, text: PCWSTR, len: i32, r: *mut RECT, fmt: u32) -> i32;
    pub fn FillRect(dc: HDC, r: *const RECT, br: HBRUSH) -> i32;
    pub fn SystemParametersInfoW(action: u32, param: u32, pv: *mut c_void, ini: u32) -> BOOL;
    pub fn MessageBeep(t: u32) -> BOOL;
    pub fn IsWindow(hwnd: HWND) -> BOOL;
}

#[link(name = "gdi32")]
extern "system" {
    pub fn CreateCompatibleDC(dc: HDC) -> HDC;
    pub fn CreateDIBSection(dc: HDC, bmi: *const BITMAPINFO, usage: u32, bits: *mut *mut c_void, section: HANDLE, offset: u32) -> HBITMAP;
    pub fn SelectObject(dc: HDC, obj: HGDIOBJ) -> HGDIOBJ;
    pub fn DeleteObject(obj: HGDIOBJ) -> BOOL;
    pub fn DeleteDC(dc: HDC) -> BOOL;
    pub fn BitBlt(dst: HDC, x: i32, y: i32, w: i32, h: i32, src: HDC, sx: i32, sy: i32, rop: u32) -> BOOL;
    pub fn CreateFontIndirectW(lf: *const LOGFONTW) -> HFONT;
    pub fn SetTextColor(dc: HDC, c: u32) -> u32;
    pub fn SetBkMode(dc: HDC, mode: i32) -> i32;
    pub fn GetTextExtentPoint32W(dc: HDC, s: PCWSTR, n: i32, size: *mut SIZE) -> BOOL;
    pub fn GetTextExtentExPointW(dc: HDC, s: PCWSTR, n: i32, max: i32, fit: *mut i32, dx: *mut i32, size: *mut SIZE) -> BOOL;
    pub fn GetTextFaceW(dc: HDC, n: i32, name: PWSTR) -> i32;
    pub fn GdiFlush() -> BOOL;
    pub fn GetStockObject(i: i32) -> HGDIOBJ;
}

#[link(name = "shell32")]
extern "system" {
    pub fn Shell_NotifyIconW(msg: u32, data: *mut NOTIFYICONDATAW) -> BOOL;
    pub fn ShellExecuteW(hwnd: HWND, op: PCWSTR, file: PCWSTR, params: PCWSTR, dir: PCWSTR, show: i32) -> HINSTANCE;
    pub fn SHGetFileInfoW(path: PCWSTR, attrs: u32, info: *mut SHFILEINFOW, size: u32, flags: u32) -> usize;
}

#[link(name = "ole32")]
extern "system" {
    pub fn CoInitializeEx(reserved: *const c_void, flags: u32) -> HRESULT;
    pub fn CoUninitialize();
    pub fn CoCreateInstance(clsid: *const GUID, outer: *mut c_void, ctx: u32, iid: *const GUID, out: *mut *mut c_void) -> HRESULT;
    pub fn CoTaskMemFree(p: *mut c_void);
    pub fn CoCreateGuid(g: *mut GUID) -> HRESULT;
    pub fn PropVariantClear(pv: *mut c_void) -> HRESULT;
}

#[link(name = "advapi32")]
extern "system" {
    pub fn RegOpenKeyExW(key: HKEY, sub: PCWSTR, opts: u32, sam: u32, out: *mut HKEY) -> i32;
    pub fn RegCloseKey(key: HKEY) -> i32;
    pub fn RegSetValueExW(key: HKEY, name: PCWSTR, reserved: u32, ty: u32, data: *const u8, len: u32) -> i32;
    pub fn RegDeleteValueW(key: HKEY, name: PCWSTR) -> i32;
    pub fn RegGetValueW(key: HKEY, sub: PCWSTR, name: PCWSTR, flags: u32, ty: *mut u32, data: *mut c_void, len: *mut u32) -> i32;
}

#[link(name = "kernel32")]
extern "system" {
    fn LoadLibraryW(name: PCWSTR) -> HINSTANCE;
    fn GetProcAddress(m: HINSTANCE, name: *const u8) -> *const c_void;
}

/// Binds a system DLL export on first use (dwmapi / version have no import libs in every toolchain).
macro_rules! dynamic_fn {
    ($dll:literal, $name:ident($($arg:ident: $ty:ty),*) -> $ret:ty, $fallback:expr) => {
        #[allow(clippy::missing_safety_doc)]
        pub unsafe fn $name($($arg: $ty),*) -> $ret {
            static PTR: std::sync::OnceLock<usize> = std::sync::OnceLock::new();
            let p = *PTR.get_or_init(|| {
                let dll = wide($dll);
                let m = LoadLibraryW(dll.as_ptr());
                if m == 0 {
                    return 0;
                }
                GetProcAddress(m, concat!(stringify!($name), "\0").as_ptr()) as usize
            });
            if p == 0 {
                return $fallback;
            }
            let f: unsafe extern "system" fn($($ty),*) -> $ret = core::mem::transmute(p);
            f($($arg),*)
        }
    };
}

dynamic_fn!("dwmapi.dll", DwmSetWindowAttribute(hwnd: HWND, attr: u32, data: *const c_void, size: u32) -> HRESULT, -1);
dynamic_fn!("dwmapi.dll", DwmExtendFrameIntoClientArea(hwnd: HWND, m: *const MARGINS) -> HRESULT, -1);
dynamic_fn!("version.dll", GetFileVersionInfoSizeW(file: PCWSTR, handle: *mut u32) -> u32, 0);
dynamic_fn!("version.dll", GetFileVersionInfoW(file: PCWSTR, handle: u32, len: u32, data: *mut c_void) -> BOOL, 0);
dynamic_fn!("version.dll", VerQueryValueW(block: *const c_void, sub: PCWSTR, out: *mut *mut c_void, len: *mut u32) -> BOOL, 0);

#[link(name = "winhttp")]
extern "system" {
    pub fn WinHttpOpen(agent: PCWSTR, access: u32, proxy: PCWSTR, bypass: PCWSTR, flags: u32) -> *mut c_void;
    pub fn WinHttpConnect(s: *mut c_void, server: PCWSTR, port: u16, reserved: u32) -> *mut c_void;
    pub fn WinHttpOpenRequest(c: *mut c_void, verb: PCWSTR, path: PCWSTR, ver: PCWSTR, referrer: PCWSTR, accept: *const PCWSTR, flags: u32) -> *mut c_void;
    pub fn WinHttpAddRequestHeaders(r: *mut c_void, headers: PCWSTR, len: u32, modifiers: u32) -> BOOL;
    pub fn WinHttpSendRequest(r: *mut c_void, headers: PCWSTR, hlen: u32, opt: *const c_void, olen: u32, total: u32, ctx: usize) -> BOOL;
    pub fn WinHttpReceiveResponse(r: *mut c_void, reserved: *mut c_void) -> BOOL;
    pub fn WinHttpQueryHeaders(r: *mut c_void, level: u32, name: PCWSTR, buf: *mut c_void, len: *mut u32, index: *mut u32) -> BOOL;
    pub fn WinHttpReadData(r: *mut c_void, buf: *mut c_void, len: u32, read: *mut u32) -> BOOL;
    pub fn WinHttpSetTimeouts(h: *mut c_void, resolve: i32, connect: i32, send: i32, recv: i32) -> BOOL;
    pub fn WinHttpCloseHandle(h: *mut c_void) -> BOOL;
}

// ───── Helpers ─────

/// NUL-terminated UTF-16 for passing to W APIs.
pub fn wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(Some(0)).collect()
}

/// Reads a NUL-terminated UTF-16 string.
///
/// # Safety
/// `p` must be null or point to a NUL-terminated UTF-16 string.
pub unsafe fn from_wide_ptr(p: *const u16) -> String {
    if p.is_null() {
        return String::new();
    }
    let mut n = 0;
    while *p.add(n) != 0 {
        n += 1;
    }
    String::from_utf16_lossy(core::slice::from_raw_parts(p, n))
}

pub fn from_wide(buf: &[u16]) -> String {
    let n = buf.iter().position(|&c| c == 0).unwrap_or(buf.len());
    String::from_utf16_lossy(&buf[..n])
}

pub fn copy_to_wide_buf(dst: &mut [u16], s: &str) {
    let mut i = 0;
    for c in s.encode_utf16() {
        if i + 1 >= dst.len() {
            break;
        }
        dst[i] = c;
        i += 1;
    }
    dst[i] = 0;
}

#[inline]
pub fn loword(l: isize) -> i32 {
    (l as u32 & 0xFFFF) as i16 as i32
}

#[inline]
pub fn hiword(l: isize) -> i32 {
    ((l as u32 >> 16) & 0xFFFF) as i16 as i32
}

pub fn tick_ms() -> u64 {
    unsafe { GetTickCount64() }
}

/// Debug output; also appended to the file named by KBMIXER_LOG when that is set.
pub fn debug_log(s: &str) {
    if cfg!(debug_assertions) {
        let w = wide(&format!("KBMixer: {s}\n"));
        unsafe { OutputDebugStringW(w.as_ptr()) };
    }
    static LOG: std::sync::OnceLock<Option<std::sync::Mutex<std::fs::File>>> = std::sync::OnceLock::new();
    let log = LOG.get_or_init(|| {
        let p = std::env::var_os("KBMIXER_LOG")?;
        std::fs::OpenOptions::new().create(true).append(true).open(p).ok().map(std::sync::Mutex::new)
    });
    if let Some(f) = log {
        use std::io::Write;
        let _ = writeln!(f.lock().unwrap_or_else(|e| e.into_inner()), "{} {s}", tick_ms());
    }
}

/// Opens a URL or file with its default handler.
pub fn shell_open(target: &str) {
    let op = wide("open");
    let t = wide(target);
    unsafe { ShellExecuteW(0, op.as_ptr(), t.as_ptr(), core::ptr::null(), core::ptr::null(), SW_SHOWNORMAL) };
}
