//! Software canvas over a 32-bit premultiplied DIB: anti-aliased rounded shapes we rasterize ourselves, GDI text
//! composited so the alpha channel stays valid (which is what lets Mica show through on Windows 11).

use crate::win::*;
use core::ffi::c_void;
use std::collections::HashMap;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Color {
    pub r: u8,
    pub g: u8,
    pub b: u8,
    pub a: u8,
}

impl Color {
    pub const fn rgb(v: u32) -> Color {
        Color { r: (v >> 16) as u8, g: (v >> 8) as u8, b: v as u8, a: 255 }
    }
    pub const fn rgba(v: u32, a: u8) -> Color {
        Color { r: (v >> 16) as u8, g: (v >> 8) as u8, b: v as u8, a }
    }
    pub fn with_alpha(self, a: f32) -> Color {
        Color { a: (self.a as f32 * a.clamp(0.0, 1.0)).round() as u8, ..self }
    }
    pub fn colorref(self) -> u32 {
        self.r as u32 | (self.g as u32) << 8 | (self.b as u32) << 16
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Rect {
    pub x: i32,
    pub y: i32,
    pub w: i32,
    pub h: i32,
}

impl Rect {
    pub const fn new(x: i32, y: i32, w: i32, h: i32) -> Rect {
        Rect { x, y, w, h }
    }
    pub fn right(&self) -> i32 {
        self.x + self.w
    }
    pub fn bottom(&self) -> i32 {
        self.y + self.h
    }
    pub fn contains(&self, x: i32, y: i32) -> bool {
        x >= self.x && x < self.right() && y >= self.y && y < self.bottom()
    }
    pub fn inset(&self, dx: i32, dy: i32) -> Rect {
        Rect::new(self.x + dx, self.y + dy, (self.w - 2 * dx).max(0), (self.h - 2 * dy).max(0))
    }
    pub fn intersect(&self, o: &Rect) -> Rect {
        let x0 = self.x.max(o.x);
        let y0 = self.y.max(o.y);
        let x1 = self.right().min(o.right());
        let y1 = self.bottom().min(o.bottom());
        Rect::new(x0, y0, (x1 - x0).max(0), (y1 - y0).max(0))
    }
    pub fn is_empty(&self) -> bool {
        self.w <= 0 || self.h <= 0
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Hash, Debug)]
pub enum Font {
    Caption,
    Body,
    BodyStrong,
    Subtitle,
    Title,
    Icon(u8),
}

pub struct Theme {
    pub dark: bool,
    pub mica: bool,
    pub bg: Color,
    pub card: Color,
    pub card_stroke: Color,
    pub text: Color,
    pub text2: Color,
    pub text3: Color,
    pub text_disabled: Color,
    pub control: Color,
    pub control_hover: Color,
    pub control_pressed: Color,
    pub control_disabled: Color,
    pub control_stroke: Color,
    pub control_stroke_bottom: Color,
    pub subtle_hover: Color,
    pub subtle_pressed: Color,
    pub accent: Color,
    pub accent_hover: Color,
    pub accent_pressed: Color,
    pub on_accent: Color,
    pub highlight_row: Color,
    pub divider: Color,
    pub smoke: Color,
    pub dialog_bg: Color,
    pub dialog_footer: Color,
    pub flyout: Color,
    pub slider_track: Color,
    pub info_bg: Color,
    pub thumb_outer: Color,
    pub tooltip: Color,
    pub input_bg: Color,
    pub input_focus_bg: Color,
    pub caption_color: u32,
}

impl Theme {
    pub fn load(mica: bool) -> Theme {
        let dark = !apps_use_light_theme();
        let (accent_dark_mode, accent_light_mode) = accent_colors();
        if dark {
            let accent = accent_dark_mode;
            Theme {
                dark,
                mica,
                bg: Color::rgb(0x202020),
                card: Color::rgb(0x2B2B2B),
                card_stroke: Color::rgb(0x1D1D1D),
                text: Color::rgb(0xFFFFFF),
                text2: Color::rgb(0xCFCFCF),
                text3: Color::rgb(0x9E9E9E),
                text_disabled: Color::rgb(0x717171),
                control: Color::rgb(0x373737),
                control_hover: Color::rgb(0x3C3C3C),
                control_pressed: Color::rgb(0x323232),
                control_disabled: Color::rgb(0x313131),
                control_stroke: Color::rgb(0x434343),
                control_stroke_bottom: Color::rgb(0x3A3A3A),
                subtle_hover: Color::rgba(0xFFFFFF, 15),
                subtle_pressed: Color::rgba(0xFFFFFF, 10),
                accent_hover: accent.with_alpha(0.9),
                accent_pressed: accent.with_alpha(0.8),
                accent,
                on_accent: Color::rgb(0x000000),
                highlight_row: Color::rgb(0x2B3F55),
                divider: Color::rgb(0x3D3D3D),
                smoke: Color::rgba(0x000000, 0x4D),
                dialog_bg: Color::rgb(0x2B2B2B),
                dialog_footer: Color::rgb(0x202020),
                flyout: Color::rgb(0x2C2C2C),
                slider_track: Color::rgb(0x9A9A9A),
                info_bg: Color::rgb(0x2B2B2B),
                thumb_outer: Color::rgb(0x454545),
                tooltip: Color::rgb(0x2C2C2C),
                input_bg: Color::rgb(0x2D2D2D),
                input_focus_bg: Color::rgb(0x1F1F1F),
                caption_color: 0x202020,
            }
        } else {
            let accent = accent_light_mode;
            Theme {
                dark,
                mica,
                bg: Color::rgb(0xF3F3F3),
                card: Color::rgb(0xFBFBFB),
                card_stroke: Color::rgb(0xE5E5E5),
                text: Color::rgb(0x1B1B1B),
                text2: Color::rgb(0x5D5D5D),
                text3: Color::rgb(0x8B8B8B),
                text_disabled: Color::rgb(0xA0A0A0),
                control: Color::rgb(0xFEFEFE),
                control_hover: Color::rgb(0xF9F9F9),
                control_pressed: Color::rgb(0xF5F5F5),
                control_disabled: Color::rgb(0xF5F5F5),
                control_stroke: Color::rgb(0xE5E5E5),
                control_stroke_bottom: Color::rgb(0xCCCCCC),
                subtle_hover: Color::rgba(0x000000, 10),
                subtle_pressed: Color::rgba(0x000000, 6),
                accent_hover: accent.with_alpha(0.9),
                accent_pressed: accent.with_alpha(0.8),
                accent,
                on_accent: Color::rgb(0xFFFFFF),
                highlight_row: Color::rgb(0xD6E6F5),
                divider: Color::rgb(0xE5E5E5),
                smoke: Color::rgba(0x000000, 0x4D),
                dialog_bg: Color::rgb(0xFFFFFF),
                dialog_footer: Color::rgb(0xF3F3F3),
                flyout: Color::rgb(0xF9F9F9),
                slider_track: Color::rgb(0x8A8A8A),
                info_bg: Color::rgb(0xF6F6F6),
                thumb_outer: Color::rgb(0xFFFFFF),
                tooltip: Color::rgb(0xF9F9F9),
                input_bg: Color::rgb(0xFBFBFB),
                input_focus_bg: Color::rgb(0xFFFFFF),
                caption_color: 0xF3F3F3,
            }
        }
    }
}

fn apps_use_light_theme() -> bool {
    let sub = wide(r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
    let name = wide("AppsUseLightTheme");
    let mut v = 1u32;
    let mut len = 4u32;
    unsafe {
        RegGetValueW(HKEY_CURRENT_USER, sub.as_ptr(), name.as_ptr(), RRF_RT_REG_DWORD, core::ptr::null_mut(), &mut v as *mut u32 as *mut c_void, &mut len);
    }
    v != 0
}

/// (AccentFillColorDefault for dark mode = accent Light2, for light mode = accent Dark1) from the system palette.
fn accent_colors() -> (Color, Color) {
    let sub = wide(r"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
    let name = wide("AccentPalette");
    let mut buf = [0u8; 32];
    let mut len = 32u32;
    // RRF_RT_REG_BINARY = 0x8
    let ok = unsafe { RegGetValueW(HKEY_CURRENT_USER, sub.as_ptr(), name.as_ptr(), 0x8, core::ptr::null_mut(), buf.as_mut_ptr() as *mut c_void, &mut len) } == ERROR_SUCCESS;
    if ok && len >= 32 {
        let at = |i: usize| Color { r: buf[i * 4], g: buf[i * 4 + 1], b: buf[i * 4 + 2], a: 255 };
        (at(1), at(4))
    } else {
        (Color::rgb(0x60CDFF), Color::rgb(0x005FB8))
    }
}

// ──────────────────── Fonts ────────────────────

struct FontSet {
    text_face: String,
    display_face: String,
    icon_face: String,
    cache: HashMap<Font, HFONT>,
}

fn face_exists(dc: HDC, face: &str) -> bool {
    let f = make_font(face, 14, FW_NORMAL);
    unsafe {
        let old = SelectObject(dc, f);
        let mut buf = [0u16; 64];
        GetTextFaceW(dc, buf.len() as i32, buf.as_mut_ptr());
        SelectObject(dc, old);
        DeleteObject(f);
        from_wide(&buf).eq_ignore_ascii_case(face)
    }
}

fn make_font(face: &str, px: i32, weight: i32) -> HFONT {
    let mut lf = LOGFONTW {
        lfHeight: -px,
        lfWidth: 0,
        lfEscapement: 0,
        lfOrientation: 0,
        lfWeight: weight,
        lfItalic: 0,
        lfUnderline: 0,
        lfStrikeOut: 0,
        lfCharSet: DEFAULT_CHARSET,
        lfOutPrecision: OUT_TT_PRECIS,
        lfClipPrecision: CLIP_DEFAULT_PRECIS,
        lfQuality: CLEARTYPE_QUALITY,
        lfPitchAndFamily: 0,
        lfFaceName: [0; 32],
    };
    copy_to_wide_buf(&mut lf.lfFaceName, face);
    unsafe { CreateFontIndirectW(&lf) }
}

impl FontSet {
    fn new(dc: HDC) -> FontSet {
        let pick = |cands: &[&str]| cands.iter().find(|f| face_exists(dc, f)).unwrap_or(&cands[cands.len() - 1]).to_string();
        FontSet {
            text_face: pick(&["Segoe UI Variable Text", "Segoe UI"]),
            display_face: pick(&["Segoe UI Variable Display", "Segoe UI"]),
            icon_face: pick(&["Segoe Fluent Icons", "Segoe MDL2 Assets"]),
            cache: HashMap::new(),
        }
    }

    fn clear(&mut self) {
        for (_, f) in self.cache.drain() {
            unsafe { DeleteObject(f) };
        }
    }

    fn get(&mut self, f: Font, scale: f32) -> HFONT {
        if let Some(h) = self.cache.get(&f) {
            return *h;
        }
        let px = |v: f32| (v * scale).round() as i32;
        let h = match f {
            Font::Caption => make_font(&self.text_face, px(12.0), FW_NORMAL),
            Font::Body => make_font(&self.text_face, px(14.0), FW_NORMAL),
            Font::BodyStrong => make_font(&self.text_face, px(14.0), FW_SEMIBOLD),
            Font::Subtitle => make_font(&self.display_face, px(20.0), FW_SEMIBOLD),
            Font::Title => make_font(&self.display_face, px(28.0), FW_SEMIBOLD),
            Font::Icon(size) => make_font(&self.icon_face, px(size as f32), FW_NORMAL),
        };
        self.cache.insert(f, h);
        h
    }
}

// ──────────────────── Canvas ────────────────────

struct Dib {
    dc: HDC,
    bmp: HBITMAP,
    old: HGDIOBJ,
    px: *mut u32,
    w: i32,
    h: i32,
}

impl Dib {
    fn new(w: i32, h: i32) -> Dib {
        let w = w.max(1);
        let h = h.max(1);
        unsafe {
            let dc = CreateCompatibleDC(0);
            let bmi = BITMAPINFO {
                bmiHeader: BITMAPINFOHEADER {
                    biSize: core::mem::size_of::<BITMAPINFOHEADER>() as u32,
                    biWidth: w,
                    biHeight: -h, // top-down
                    biPlanes: 1,
                    biBitCount: 32,
                    biCompression: BI_RGB,
                    ..Default::default()
                },
                bmiColors: [0],
            };
            let mut bits = core::ptr::null_mut();
            let bmp = CreateDIBSection(dc, &bmi, DIB_RGB_COLORS, &mut bits, 0, 0);
            let old = SelectObject(dc, bmp);
            SetBkMode(dc, TRANSPARENT);
            Dib { dc, bmp, old, px: bits as *mut u32, w, h }
        }
    }

    fn pixels(&mut self) -> &mut [u32] {
        unsafe { core::slice::from_raw_parts_mut(self.px, (self.w * self.h) as usize) }
    }
}

impl Drop for Dib {
    fn drop(&mut self) {
        unsafe {
            SelectObject(self.dc, self.old);
            DeleteObject(self.bmp);
            DeleteDC(self.dc);
        }
    }
}

/// A premultiplied 32-bit image (used for app icons).
pub struct Image {
    pub w: i32,
    pub h: i32,
    pub px: Vec<u32>,
}

pub struct Canvas {
    back: Dib,
    scratch: Dib,
    fonts: FontSet,
    pub scale: f32,
    /// False during input passes: layout and hit-testing run, pixels are not touched.
    pub drawing: bool,
    clip: Rect,
    clip_stack: Vec<Rect>,
}

#[inline]
fn premul(c: Color, cov: f32) -> (u32, u32, u32, u32) {
    let a = c.a as f32 * cov;
    let f = a / 255.0;
    ((c.r as f32 * f) as u32, (c.g as f32 * f) as u32, (c.b as f32 * f) as u32, a as u32)
}

#[inline]
fn blend(dst: &mut u32, c: Color, cov: f32) {
    if cov <= 0.0 {
        return;
    }
    let (r, g, b, a) = premul(c, cov.min(1.0));
    if a >= 255 {
        *dst = 0xFF00_0000 | r << 16 | g << 8 | b;
        return;
    }
    let inv = 255 - a;
    let d = *dst;
    let mix = |s: u32, shift: u32| s + (((d >> shift) & 0xFF) * inv + 127) / 255;
    *dst = mix(a, 24) << 24 | mix(r, 16) << 16 | mix(g, 8) << 8 | mix(b, 0);
}

/// Signed distance from (px,py) to a rounded box with corner radius r.
#[inline]
fn sd_round_box(px: f32, py: f32, x: f32, y: f32, w: f32, h: f32, r: f32) -> f32 {
    let hw = w / 2.0;
    let hh = h / 2.0;
    let qx = (px - (x + hw)).abs() - (hw - r);
    let qy = (py - (y + hh)).abs() - (hh - r);
    let ox = qx.max(0.0);
    let oy = qy.max(0.0);
    (ox * ox + oy * oy).sqrt() + qx.max(qy).min(0.0) - r
}

impl Canvas {
    pub fn new() -> Canvas {
        let scratch = Dib::new(256, 64);
        let fonts = FontSet::new(scratch.dc);
        Canvas {
            back: Dib::new(1, 1),
            scratch,
            fonts,
            scale: 1.0,
            drawing: false,
            clip: Rect::new(0, 0, 1, 1),
            clip_stack: Vec::new(),
        }
    }

    pub fn set_scale(&mut self, scale: f32) {
        if (scale - self.scale).abs() > 0.001 {
            self.scale = scale;
            self.fonts.clear();
        }
    }

    pub fn resize(&mut self, w: i32, h: i32) {
        if w != self.back.w || h != self.back.h {
            self.back = Dib::new(w, h);
        }
        self.clip = Rect::new(0, 0, self.back.w, self.back.h);
    }

    pub fn width(&self) -> i32 {
        self.back.w
    }
    pub fn height(&self) -> i32 {
        self.back.h
    }

    /// Device pixels for a DIP value.
    #[inline]
    pub fn px(&self, dip: f32) -> i32 {
        (dip * self.scale).round() as i32
    }

    pub fn push_clip(&mut self, r: Rect) {
        self.clip_stack.push(self.clip);
        self.clip = self.clip.intersect(&r);
    }
    pub fn pop_clip(&mut self) {
        if let Some(c) = self.clip_stack.pop() {
            self.clip = c;
        }
    }
    pub fn clip(&self) -> Rect {
        self.clip
    }

    pub fn clear(&mut self, c: Color) {
        let (r, g, b, a) = premul(c, 1.0);
        let v = a << 24 | r << 16 | g << 8 | b;
        self.back.pixels().fill(v);
    }

    pub fn present(&mut self, dc: HDC) {
        unsafe {
            GdiFlush();
            BitBlt(dc, 0, 0, self.back.w, self.back.h, self.back.dc, 0, 0, SRCCOPY);
        }
    }

    pub fn fill_rect(&mut self, r: Rect, c: Color) {
        if !self.drawing || c.a == 0 {
            return;
        }
        let r = r.intersect(&self.clip);
        if r.is_empty() {
            return;
        }
        let w = self.back.w;
        let px = self.back.pixels();
        for y in r.y..r.bottom() {
            let row = &mut px[(y * w + r.x) as usize..(y * w + r.right()) as usize];
            for p in row {
                blend(p, c, 1.0);
            }
        }
    }

    /// Runs `cov(px,py)` for each pixel of `area` (clipped) and blends `c`.
    fn shade(&mut self, area: Rect, c: Color, cov: impl Fn(f32, f32) -> f32) {
        let area = area.intersect(&self.clip);
        if area.is_empty() {
            return;
        }
        let w = self.back.w;
        let px = self.back.pixels();
        for y in area.y..area.bottom() {
            let base = (y * w) as usize;
            for x in area.x..area.right() {
                let v = cov(x as f32 + 0.5, y as f32 + 0.5);
                if v > 0.0 {
                    blend(&mut px[base + x as usize], c, v);
                }
            }
        }
    }

    pub fn fill_round_rect(&mut self, r: Rect, radius: f32, c: Color) {
        if !self.drawing || c.a == 0 || r.is_empty() {
            return;
        }
        let rad = radius.min(r.w as f32 / 2.0).min(r.h as f32 / 2.0).max(0.0);
        let band = rad.ceil() as i32;
        let (x, y, w, h) = (r.x as f32, r.y as f32, r.w as f32, r.h as f32);
        let cov = move |px: f32, py: f32| (0.5 - sd_round_box(px, py, x, y, w, h, rad)).clamp(0.0, 1.0);
        if band == 0 {
            self.fill_rect(r, c);
            return;
        }
        // Corners need coverage; everything else is solid.
        self.shade(Rect::new(r.x, r.y, band, band), c, cov);
        self.shade(Rect::new(r.right() - band, r.y, band, band), c, cov);
        self.shade(Rect::new(r.x, r.bottom() - band, band, band), c, cov);
        self.shade(Rect::new(r.right() - band, r.bottom() - band, band, band), c, cov);
        self.fill_rect(Rect::new(r.x + band, r.y, r.w - 2 * band, band), c);
        self.fill_rect(Rect::new(r.x + band, r.bottom() - band, r.w - 2 * band, band), c);
        self.fill_rect(Rect::new(r.x, r.y + band, r.w, r.h - 2 * band), c);
    }

    /// Border of `thickness` inside `r`.
    pub fn stroke_round_rect(&mut self, r: Rect, radius: f32, thickness: f32, c: Color) {
        if !self.drawing || c.a == 0 || r.is_empty() {
            return;
        }
        let rad = radius.min(r.w as f32 / 2.0).min(r.h as f32 / 2.0).max(0.0);
        let (x, y, w, h) = (r.x as f32, r.y as f32, r.w as f32, r.h as f32);
        let t = thickness;
        let cov = move |px: f32, py: f32| {
            let d = sd_round_box(px, py, x, y, w, h, rad);
            ((0.5 - d).clamp(0.0, 1.0) - (0.5 - (d + t)).clamp(0.0, 1.0)).max(0.0)
        };
        let band = (rad.max(t)).ceil() as i32 + 1;
        self.shade(Rect::new(r.x, r.y, r.w, band), c, cov);
        self.shade(Rect::new(r.x, r.bottom() - band, r.w, band), c, cov);
        self.shade(Rect::new(r.x, r.y + band, band, r.h - 2 * band), c, cov);
        self.shade(Rect::new(r.right() - band, r.y + band, band, r.h - 2 * band), c, cov);
    }

    /// Two-tone control border: `bottom` on the last pixel row like Fluent's elevation stroke.
    pub fn control_border(&mut self, r: Rect, radius: f32, top: Color, bottom: Color) {
        self.stroke_round_rect(r, radius, 1.0, top);
        if bottom != top {
            let clip = Rect::new(r.x, r.bottom() - self.px(radius).max(2), r.w, self.px(radius).max(2));
            self.push_clip(clip);
            self.stroke_round_rect(r, radius, 1.0, bottom);
            self.pop_clip();
        }
    }

    pub fn fill_circle(&mut self, cx: f32, cy: f32, radius: f32, c: Color) {
        if !self.drawing || c.a == 0 {
            return;
        }
        let area = Rect::new((cx - radius - 1.0) as i32, (cy - radius - 1.0) as i32, (radius * 2.0 + 3.0) as i32, (radius * 2.0 + 3.0) as i32);
        self.shade(area, c, move |px, py| {
            let d = ((px - cx).powi(2) + (py - cy).powi(2)).sqrt() - radius;
            (0.5 - d).clamp(0.0, 1.0)
        });
    }

    pub fn stroke_circle(&mut self, cx: f32, cy: f32, radius: f32, t: f32, c: Color) {
        if !self.drawing || c.a == 0 {
            return;
        }
        let area = Rect::new((cx - radius - 1.0) as i32, (cy - radius - 1.0) as i32, (radius * 2.0 + 3.0) as i32, (radius * 2.0 + 3.0) as i32);
        self.shade(area, c, move |px, py| {
            let d = ((px - cx).powi(2) + (py - cy).powi(2)).sqrt() - radius;
            ((0.5 - d).clamp(0.0, 1.0) - (0.5 - (d + t)).clamp(0.0, 1.0)).max(0.0)
        });
    }

    // ───── Text ─────

    pub fn font(&mut self, f: Font) -> HFONT {
        self.fonts.get(f, self.scale)
    }

    pub fn measure(&mut self, text: &str, f: Font) -> (i32, i32) {
        let font = self.font(f);
        let w = text.encode_utf16().collect::<Vec<u16>>();
        let mut sz = SIZE::default();
        unsafe {
            SelectObject(self.scratch.dc, font);
            GetTextExtentPoint32W(self.scratch.dc, w.as_ptr(), w.len() as i32, &mut sz);
        }
        if text.is_empty() {
            sz.cx = 0;
        }
        (sz.cx, sz.cy)
    }

    /// Height of `text` wrapped at `width`.
    pub fn measure_wrapped(&mut self, text: &str, f: Font, width: i32) -> i32 {
        let font = self.font(f);
        let w: Vec<u16> = text.encode_utf16().collect();
        let mut r = RECT { left: 0, top: 0, right: width.max(1), bottom: 0 };
        unsafe {
            SelectObject(self.scratch.dc, font);
            DrawTextW(self.scratch.dc, w.as_ptr(), w.len() as i32, &mut r, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX);
        }
        r.bottom - r.top
    }

    /// Character offsets (in UTF-16 units) → x positions, for the text box caret and hit-testing.
    pub fn char_positions(&mut self, text: &[u16], f: Font) -> Vec<i32> {
        let font = self.font(f);
        let mut out = vec![0; text.len() + 1];
        if text.is_empty() {
            return out;
        }
        let mut dx = vec![0i32; text.len()];
        let mut sz = SIZE::default();
        let mut fit = 0;
        unsafe {
            SelectObject(self.scratch.dc, font);
            GetTextExtentExPointW(self.scratch.dc, text.as_ptr(), text.len() as i32, i32::MAX, &mut fit, dx.as_mut_ptr(), &mut sz);
        }
        out[1..].copy_from_slice(&dx);
        out
    }

    /// Draws text inside `r` with DrawText `flags`. Opaque destinations get exact ClearType rendering against the
    /// real background; translucent ones (Mica) get coverage-derived alpha so nothing punches holes.
    pub fn text(&mut self, r: Rect, text: &str, f: Font, c: Color, flags: u32) {
        if !self.drawing || text.is_empty() || c.a == 0 {
            return;
        }
        let vis = r.intersect(&self.clip);
        if vis.is_empty() {
            return;
        }
        let font = self.font(f);
        let w: Vec<u16> = text.encode_utf16().collect();
        if self.scratch.w < vis.w || self.scratch.h < vis.h {
            self.scratch = Dib::new(vis.w.max(self.scratch.w), vis.h.max(self.scratch.h));
        }
        let (bw, sw) = (self.back.w, self.scratch.w);

        // Is the destination fully opaque?
        let opaque = {
            let back = unsafe { core::slice::from_raw_parts(self.back.px, (self.back.w * self.back.h) as usize) };
            (vis.y..vis.bottom()).all(|y| back[(y * bw + vis.x) as usize..(y * bw + vis.right()) as usize].iter().all(|p| p >> 24 == 0xFF))
        };

        let mut rr = RECT { left: r.x - vis.x, top: r.y - vis.y, right: r.right() - vis.x, bottom: r.bottom() - vis.y };
        let flags = flags | DT_NOPREFIX;
        unsafe {
            let back = core::slice::from_raw_parts_mut(self.back.px, (self.back.w * self.back.h) as usize);
            let scr = core::slice::from_raw_parts_mut(self.scratch.px, (self.scratch.w * self.scratch.h) as usize);
            SelectObject(self.scratch.dc, font);
            if opaque && c.a == 255 {
                for y in 0..vis.h {
                    let s = (y * sw) as usize;
                    let d = ((vis.y + y) * bw + vis.x) as usize;
                    scr[s..s + vis.w as usize].copy_from_slice(&back[d..d + vis.w as usize]);
                }
                SetTextColor(self.scratch.dc, c.colorref());
                DrawTextW(self.scratch.dc, w.as_ptr(), w.len() as i32, &mut rr, flags);
                GdiFlush();
                for y in 0..vis.h {
                    let s = (y * sw) as usize;
                    let d = ((vis.y + y) * bw + vis.x) as usize;
                    for x in 0..vis.w as usize {
                        back[d + x] = scr[s + x] | 0xFF00_0000;
                    }
                }
            } else {
                for y in 0..vis.h {
                    let s = (y * sw) as usize;
                    scr[s..s + vis.w as usize].fill(0);
                }
                SetTextColor(self.scratch.dc, 0xFFFFFF);
                DrawTextW(self.scratch.dc, w.as_ptr(), w.len() as i32, &mut rr, flags);
                GdiFlush();
                for y in 0..vis.h {
                    let s = (y * sw) as usize;
                    let d = ((vis.y + y) * bw + vis.x) as usize;
                    for x in 0..vis.w as usize {
                        let p = scr[s + x];
                        let cov = (((p >> 16) & 0xFF) + ((p >> 8) & 0xFF) + (p & 0xFF)) as f32 / (3.0 * 255.0);
                        blend(&mut back[d + x], c, cov);
                    }
                }
            }
        }
    }

    /// Icon-font glyph centered in `r`.
    pub fn glyph(&mut self, r: Rect, glyph: char, size: u8, c: Color) {
        let mut buf = [0u8; 4];
        self.text(r, glyph.encode_utf8(&mut buf), Font::Icon(size), c, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    }

    pub fn image(&mut self, x: i32, y: i32, img: &Image, opacity: f32) {
        if !self.drawing {
            return;
        }
        let area = Rect::new(x, y, img.w, img.h).intersect(&self.clip);
        if area.is_empty() {
            return;
        }
        let bw = self.back.w;
        let back = self.back.pixels();
        for py in area.y..area.bottom() {
            for px in area.x..area.right() {
                let s = img.px[((py - y) * img.w + (px - x)) as usize];
                let sa = ((s >> 24) as f32 * opacity) as u32;
                if sa == 0 {
                    continue;
                }
                let f = |shift: u32| (((s >> shift) & 0xFF) as f32 * opacity) as u32;
                let d = &mut back[(py * bw + px) as usize];
                let inv = 255 - sa.min(255);
                let mix = |sv: u32, shift: u32| (sv + (((*d >> shift) & 0xFF) * inv + 127) / 255).min(255);
                *d = mix(sa, 24) << 24 | mix(f(16), 16) << 16 | mix(f(8), 8) << 8 | mix(f(0), 0);
            }
        }
    }

    /// Rasterizes an HICON at `size` into a premultiplied image by drawing it on black and on white.
    pub fn rasterize_icon(&mut self, icon: HICON, size: i32) -> Image {
        let mut on_black = Dib::new(size, size);
        let mut on_white = Dib::new(size, size);
        on_black.pixels().fill(0xFF00_0000);
        on_white.pixels().fill(0xFFFF_FFFF);
        unsafe {
            DrawIconEx(on_black.dc, 0, 0, icon, size, size, 0, 0, DI_NORMAL);
            DrawIconEx(on_white.dc, 0, 0, icon, size, size, 0, 0, DI_NORMAL);
            GdiFlush();
        }
        let b = on_black.pixels().to_vec();
        let wv = on_white.pixels().to_vec();
        let px = b
            .iter()
            .zip(wv.iter())
            .map(|(&kb, &kw)| {
                // alpha = 1 - (white - black); premultiplied colour = the on-black value.
                let ch = |p: u32, s: u32| (p >> s) & 0xFF;
                let diff = (ch(kw, 16) as i32 - ch(kb, 16) as i32 + ch(kw, 8) as i32 - ch(kb, 8) as i32 + ch(kw, 0) as i32 - ch(kb, 0) as i32) / 3;
                let a = (255 - diff).clamp(0, 255) as u32;
                let cl = |s: u32| ch(kb, s).min(a);
                a << 24 | cl(16) << 16 | cl(8) << 8 | cl(0)
            })
            .collect();
        Image { w: size, h: size, px }
    }
}

impl Drop for Canvas {
    fn drop(&mut self) {
        self.fonts.clear();
    }
}
