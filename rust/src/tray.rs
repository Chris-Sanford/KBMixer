//! Notification-area icon: shown only while the window is hidden, like the .NET build.

use crate::win::*;

pub const WM_APP_TRAY: u32 = WM_APP + 20;
pub const CMD_OPEN: usize = 1;
pub const CMD_EXIT: usize = 2;

pub struct Tray {
    hwnd: HWND,
    icon: HICON,
    visible: bool,
}

impl Tray {
    pub fn new(hwnd: HWND, icon: HICON) -> Tray {
        Tray { hwnd, icon, visible: false }
    }

    fn data(&self) -> NOTIFYICONDATAW {
        let mut d: NOTIFYICONDATAW = unsafe { core::mem::zeroed() };
        d.cbSize = core::mem::size_of::<NOTIFYICONDATAW>() as u32;
        d.hWnd = self.hwnd;
        d.uID = 1;
        d
    }

    pub fn show(&mut self) {
        if self.visible {
            return;
        }
        let mut d = self.data();
        d.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        d.uCallbackMessage = WM_APP_TRAY;
        d.hIcon = self.icon;
        copy_to_wide_buf(&mut d.szTip, "KBMixer");
        unsafe {
            if Shell_NotifyIconW(NIM_ADD, &mut d) != 0 {
                d.uVersion = NOTIFYICON_VERSION_4;
                Shell_NotifyIconW(NIM_SETVERSION, &mut d);
                self.visible = true;
            }
        }
    }

    pub fn hide(&mut self) {
        if !self.visible {
            return;
        }
        let mut d = self.data();
        unsafe { Shell_NotifyIconW(NIM_DELETE, &mut d) };
        self.visible = false;
    }

    /// Explorer restarted: the icon is gone and has to be added again.
    pub fn recreate(&mut self) {
        if self.visible {
            self.visible = false;
            self.show();
        }
    }

    /// Right-click menu; returns the chosen command (0 if dismissed).
    pub fn context_menu(&self) -> usize {
        unsafe {
            let menu = CreatePopupMenu();
            let open = wide("Open KBMixer");
            let exit = wide("Exit");
            AppendMenuW(menu, MF_STRING, CMD_OPEN, open.as_ptr());
            AppendMenuW(menu, MF_SEPARATOR, 0, core::ptr::null());
            AppendMenuW(menu, MF_STRING, CMD_EXIT, exit.as_ptr());
            let mut pt = POINT::default();
            GetCursorPos(&mut pt);
            // Required so the menu closes when the user clicks elsewhere.
            SetForegroundWindow(self.hwnd);
            let cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, pt.x, pt.y, 0, self.hwnd, core::ptr::null());
            PostMessageW(self.hwnd, WM_NULL, 0, 0);
            DestroyMenu(menu);
            cmd as usize
        }
    }
}

impl Drop for Tray {
    fn drop(&mut self) {
        self.hide();
    }
}
