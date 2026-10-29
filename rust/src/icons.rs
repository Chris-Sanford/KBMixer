//! Per-executable icon cache (UI thread only). `None` is cached too, so missing icons are not retried every rebuild.

use crate::gfx::{Canvas, Image};
use crate::win::*;
use std::collections::HashMap;
use std::rc::Rc;

#[derive(Default)]
pub struct IconCache {
    by_path: HashMap<(String, i32), Option<Rc<Image>>>,
}

impl IconCache {
    pub fn clear(&mut self) {
        self.by_path.clear();
    }

    pub fn for_exe(&mut self, canvas: &mut Canvas, exe_path: &str, size: i32) -> Option<Rc<Image>> {
        let key = (exe_path.to_lowercase(), size);
        if let Some(v) = self.by_path.get(&key) {
            return v.clone();
        }
        let img = load_exe_icon(exe_path, size).map(|h| {
            let img = canvas.rasterize_icon(h, size);
            unsafe { DestroyIcon(h) };
            Rc::new(img)
        });
        self.by_path.insert(key, img.clone());
        img
    }
}

fn load_exe_icon(path: &str, size: i32) -> Option<HICON> {
    if !std::path::Path::new(path).exists() {
        return None;
    }
    let w = wide(path);
    let mut icon: HICON = 0;
    let mut id = 0u32;
    let n = unsafe { PrivateExtractIconsW(w.as_ptr(), 0, size, size, &mut icon, &mut id, 1, 0) };
    if n >= 1 && n != u32::MAX && icon != 0 {
        return Some(icon);
    }
    // No embedded icon: use the shell's associated icon (what ExtractAssociatedIcon does).
    let mut info: SHFILEINFOW = unsafe { core::mem::zeroed() };
    let ok = unsafe { SHGetFileInfoW(w.as_ptr(), 0, &mut info, core::mem::size_of::<SHFILEINFOW>() as u32, SHGFI_ICON | SHGFI_LARGEICON) };
    (ok != 0 && info.hIcon != 0).then_some(info.hIcon)
}
