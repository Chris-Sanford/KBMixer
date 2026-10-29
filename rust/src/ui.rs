//! Tiny immediate-mode widget toolkit drawn with `gfx::Canvas` in the WinUI 3 (Fluent) style.
//!
//! The same frame code runs for input passes (no pixels touched) and paint passes, so layout and hit-testing can
//! never disagree. Widgets react only to the event of the pass they run in.

use crate::gfx::{Canvas, Color, Font, Image, Rect, Theme};
use crate::win::*;
use std::collections::HashMap;

pub type Id = u64;

pub fn id(s: &str) -> Id {
    id_n(s, 0)
}

pub fn id_n(s: &str, n: u64) -> Id {
    let mut h: u64 = 0xcbf29ce484222325;
    for b in s.bytes().chain(n.to_le_bytes()) {
        h ^= b as u64;
        h = h.wrapping_mul(0x100000001b3);
    }
    h | 1
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Ev {
    MouseDown,
    MouseUp,
    Wheel(i32),
    Key { vk: u32, shift: bool, ctrl: bool },
    Char(u16),
}

#[derive(Clone, Copy, PartialEq, Eq, Default)]
pub enum CursorKind {
    #[default]
    Arrow,
    IBeam,
}

/// Single-line text editing state.
#[derive(Default, Clone)]
pub struct TextEdit {
    pub text: Vec<u16>,
    pub caret: usize,
    pub anchor: usize,
    pub scroll_x: i32,
}

impl TextEdit {
    pub fn new(s: &str) -> TextEdit {
        let text: Vec<u16> = s.encode_utf16().collect();
        let n = text.len();
        TextEdit { text, caret: n, anchor: 0, scroll_x: 0 }
    }
    pub fn value(&self) -> String {
        String::from_utf16_lossy(&self.text)
    }
    fn sel(&self) -> (usize, usize) {
        (self.caret.min(self.anchor), self.caret.max(self.anchor))
    }
    fn delete_sel(&mut self) -> bool {
        let (a, b) = self.sel();
        if a == b {
            return false;
        }
        self.text.drain(a..b);
        self.caret = a;
        self.anchor = a;
        true
    }
    fn insert(&mut self, s: &[u16]) {
        self.delete_sel();
        let clean: Vec<u16> = s.iter().copied().filter(|&c| c >= 0x20 && c != 0x7F).collect();
        for (i, c) in clean.iter().enumerate() {
            self.text.insert(self.caret + i, *c);
        }
        self.caret += clean.len();
        self.anchor = self.caret;
    }
    fn prev(&self, i: usize) -> usize {
        let mut j = i.saturating_sub(1);
        if j > 0 && (0xDC00..0xE000).contains(&self.text[j]) {
            j -= 1;
        }
        j
    }
    fn next(&self, i: usize) -> usize {
        let mut j = (i + 1).min(self.text.len());
        if j < self.text.len() && (0xDC00..0xE000).contains(&self.text[j]) {
            j += 1;
        }
        j
    }
}

#[derive(Default)]
pub struct UiState {
    pub hot: Id,
    pub active: Id,
    pub focus: Id,
    pub hot_since: u64,
    pub scroll: HashMap<Id, i32>,
    pub cursor: CursorKind,
    pub caret_visible: bool,
    /// Id of the scrollbar thumb being dragged and the grab offset.
    drag_scroll: Option<(Id, i32)>,
}

pub struct Interact {
    pub hovered: bool,
    pub pressed: bool,
    pub clicked: bool,
}

struct ScrollRegion {
    id: Id,
    rect: Rect,
    max: i32,
}

pub struct Ui<'a> {
    pub c: &'a mut Canvas,
    pub t: &'a Theme,
    pub st: &'a mut UiState,
    pub mx: i32,
    pub my: i32,
    pub ev: Option<Ev>,
    /// While a modal layer is open the base layer gets no input.
    pub input_enabled: bool,
    next_hot: Id,
    next_cursor: CursorKind,
    scroll_regions: Vec<ScrollRegion>,
    pub tooltip: Option<(Rect, String)>,
    pub repaint: bool,
    /// Set when a click landed on nothing (used by popups to close themselves).
    pub consumed: bool,
}

impl<'a> Ui<'a> {
    pub fn new(c: &'a mut Canvas, t: &'a Theme, st: &'a mut UiState, mx: i32, my: i32, ev: Option<Ev>) -> Ui<'a> {
        Ui {
            c,
            t,
            st,
            mx,
            my,
            ev,
            input_enabled: true,
            next_hot: 0,
            next_cursor: CursorKind::Arrow,
            scroll_regions: Vec::new(),
            tooltip: None,
            repaint: false,
            consumed: false,
        }
    }

    #[inline]
    pub fn px(&self, dip: f32) -> i32 {
        self.c.px(dip)
    }

    pub fn mouse_in(&self, r: Rect) -> bool {
        r.contains(self.mx, self.my) && self.c.clip().contains(self.mx, self.my)
    }

    pub fn take_event(&mut self) -> Option<Ev> {
        let e = self.ev.take();
        if e.is_some() {
            self.consumed = true;
            self.repaint = true;
        }
        e
    }

    pub fn interact(&mut self, id: Id, r: Rect, enabled: bool) -> Interact {
        let over = enabled && self.input_enabled && self.mouse_in(r);
        if over {
            self.next_hot = id;
        }
        let hovered = over && self.st.hot == id;
        let mut clicked = false;
        match self.ev {
            Some(Ev::MouseDown) if hovered => {
                self.st.active = id;
                self.take_event();
            }
            Some(Ev::MouseUp) if self.st.active == id => {
                self.st.active = 0;
                clicked = over;
                self.take_event();
            }
            _ => {}
        }
        Interact { hovered, pressed: self.st.active == id && over, clicked }
    }

    pub fn set_tooltip(&mut self, id: Id, r: Rect, text: &str) {
        if self.st.hot == id && self.st.active == 0 && tick_ms().saturating_sub(self.st.hot_since) >= 600 {
            self.tooltip = Some((r, text.to_string()));
        }
    }

    /// Call at the end of a pass. Returns true if the hot widget changed.
    pub fn finish(&mut self) -> bool {
        // Wheel goes to the innermost (last registered) scroll region under the mouse.
        if let Some(Ev::Wheel(delta)) = self.ev {
            if let Some(reg) = self.scroll_regions.iter().rev().find(|r| r.rect.contains(self.mx, self.my) && r.max > 0) {
                let step = self.c.px(48.0) * delta / 120;
                let cur = self.st.scroll.get(&reg.id).copied().unwrap_or(0);
                self.st.scroll.insert(reg.id, (cur - step).clamp(0, reg.max));
                self.repaint = true;
                self.ev = None;
            }
        }
        if let Some(Ev::MouseUp) = self.ev {
            self.st.drag_scroll = None;
            if self.st.active != 0 {
                self.st.active = 0;
                self.repaint = true;
            }
        }
        self.st.cursor = self.next_cursor;
        let changed = self.next_hot != self.st.hot;
        if self.ev.is_none() || matches!(self.ev, Some(Ev::MouseDown)) {
            if changed {
                self.st.hot = self.next_hot;
                self.st.hot_since = tick_ms();
            }
        }
        changed
    }

    // ──────────────────── Scrolling ────────────────────

    /// Starts a vertically scrolling region; returns the current offset. Pair with `end_scroll`.
    pub fn begin_scroll(&mut self, id: Id, view: Rect) -> i32 {
        self.c.push_clip(view);
        self.st.scroll.get(&id).copied().unwrap_or(0)
    }

    /// `content_h` is the full height that was laid out. Draws the scrollbar and clamps the offset.
    pub fn end_scroll(&mut self, id: Id, view: Rect, content_h: i32) {
        self.c.pop_clip();
        let max = (content_h - view.h).max(0);
        let cur = self.st.scroll.get(&id).copied().unwrap_or(0);
        if cur > max {
            self.st.scroll.insert(id, max);
            self.repaint = true;
        }
        if self.input_enabled {
            self.scroll_regions.push(ScrollRegion { id, rect: view, max });
        }
        if max == 0 {
            return;
        }
        let track = Rect::new(view.right() - self.px(8.0), view.y + self.px(2.0), self.px(6.0), view.h - self.px(4.0));
        let thumb_h = ((track.h as i64 * view.h as i64 / content_h as i64) as i32).max(self.px(24.0));
        let range = (track.h - thumb_h).max(1);
        let thumb_y = track.y + (range as i64 * cur.min(max) as i64 / max as i64) as i32;
        let thumb_id = id ^ 0x5c011;
        let thumb = Rect::new(track.x, thumb_y, track.w, thumb_h);
        let it = self.interact(thumb_id, Rect::new(track.x - self.px(4.0), thumb.y, track.w + self.px(6.0), thumb.h), true);
        if it.pressed && self.st.drag_scroll.is_none() {
            self.st.drag_scroll = Some((thumb_id, self.my - thumb_y));
        }
        if let Some((tid, grab)) = self.st.drag_scroll {
            if tid == thumb_id && self.st.active == thumb_id {
                let ny = (self.my - grab - track.y).clamp(0, range);
                let v = (ny as i64 * max as i64 / range as i64) as i32;
                if v != cur {
                    self.st.scroll.insert(id, v);
                    self.repaint = true;
                }
            }
        }
        let wide = it.hovered || self.st.active == thumb_id || self.mouse_in(view) && self.mx > view.right() - self.px(16.0);
        let t = self.t;
        let w = if wide { self.px(6.0) } else { self.px(2.0) };
        let r = Rect::new(track.right() - w - self.px(1.0), thumb.y, w, thumb.h);
        self.c.fill_round_rect(r, w as f32 / 2.0, t.text3.with_alpha(if wide { 0.8 } else { 0.6 }));
    }

    // ──────────────────── Widgets ────────────────────

    pub fn label(&mut self, r: Rect, s: &str, f: Font, c: Color) {
        self.c.text(r, s, f, c, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
    }

    pub fn wrapped(&mut self, r: Rect, s: &str, f: Font, c: Color) {
        self.c.text(r, s, f, c, DT_LEFT | DT_WORDBREAK);
    }

    fn control_bg(&mut self, r: Rect, enabled: bool, it: &Interact) {
        let t = self.t;
        let bg = if !enabled {
            t.control_disabled
        } else if it.pressed {
            t.control_pressed
        } else if it.hovered {
            t.control_hover
        } else {
            t.control
        };
        let rad = self.px(4.0) as f32;
        self.c.fill_round_rect(r, rad, bg);
        let bottom = if enabled && !it.pressed { t.control_stroke_bottom } else { t.control_stroke };
        self.c.control_border(r, rad, t.control_stroke, bottom);
    }

    pub fn button(&mut self, id: Id, r: Rect, label: &str, enabled: bool) -> bool {
        let it = self.interact(id, r, enabled);
        self.control_bg(r, enabled, &it);
        let t = self.t;
        let fg = if !enabled { t.text_disabled } else if it.pressed { t.text2 } else { t.text };
        self.c.text(r, label, Font::Body, fg, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
        it.clicked
    }

    pub fn accent_button(&mut self, id: Id, r: Rect, label: &str, enabled: bool) -> bool {
        let it = self.interact(id, r, enabled);
        let t = self.t;
        let rad = self.px(4.0) as f32;
        if enabled {
            let bg = if it.pressed { t.accent_pressed } else if it.hovered { t.accent_hover } else { t.accent };
            self.c.fill_round_rect(r, rad, t.card);
            self.c.fill_round_rect(r, rad, bg);
        } else {
            self.c.fill_round_rect(r, rad, t.control_disabled);
        }
        let fg = if enabled { t.on_accent } else { t.text_disabled };
        self.c.text(r, label, Font::Body, fg, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
        it.clicked
    }

    /// Transparent button with an icon-font glyph.
    pub fn icon_button(&mut self, id: Id, r: Rect, glyph: char, size: u8, enabled: bool, tip: &str) -> bool {
        let it = self.interact(id, r, enabled);
        let t = self.t;
        let bg = if it.pressed { t.subtle_pressed } else if it.hovered { t.subtle_hover } else { Color::rgba(0, 0) };
        self.c.fill_round_rect(r, self.px(4.0) as f32, bg);
        let fg = if enabled { if it.pressed { t.text2 } else { t.text } } else { t.text_disabled };
        self.c.glyph(r, glyph, size, fg);
        if !tip.is_empty() {
            self.set_tooltip(id, r, tip);
        }
        it.clicked
    }

    /// Closed state of a ComboBox; returns true when clicked (caller opens the dropdown).
    pub fn combo(&mut self, id: Id, r: Rect, text: &str, enabled: bool, open: bool) -> bool {
        let it = self.interact(id, r, enabled);
        self.control_bg(r, enabled, &it);
        let t = self.t;
        let fg = if enabled { t.text } else { t.text_disabled };
        let pad = self.px(11.0);
        let chev = self.px(32.0);
        self.label(Rect::new(r.x + pad, r.y, r.w - pad - chev, r.h), text, Font::Body, fg);
        let cr = Rect::new(r.right() - chev, r.y, chev - self.px(6.0), r.h);
        // ComboBox keeps its pressed look while the dropdown is open.
        let chev_color = if !enabled { t.text_disabled } else if open { t.text } else { t.text2 };
        self.c.glyph(cr, '\u{E70D}', 12, chev_color);
        it.clicked
    }

    pub fn checkbox(&mut self, id: Id, r: Rect, label: &str, checked: bool, enabled: bool) -> bool {
        let it = self.interact(id, r, enabled);
        let t = self.t;
        let s = self.px(20.0);
        let b = Rect::new(r.x, r.y + (r.h - s) / 2, s, s);
        let rad = self.px(4.0) as f32;
        if checked {
            let bg = if !enabled { t.text_disabled } else if it.pressed { t.accent_pressed } else if it.hovered { t.accent_hover } else { t.accent };
            self.c.fill_round_rect(b, rad, t.card);
            self.c.fill_round_rect(b, rad, bg);
            self.c.glyph(b, '\u{E73E}', 12, if enabled { t.on_accent } else { t.card });
        } else {
            let bg = if it.pressed { t.subtle_pressed } else if it.hovered { t.subtle_hover } else { Color::rgba(0, 0) };
            self.c.fill_round_rect(b, rad, bg);
            self.c.stroke_round_rect(b, rad, 1.0, if enabled { t.text2 } else { t.text_disabled });
        }
        let fg = if enabled { t.text } else { t.text_disabled };
        let lx = b.right() + self.px(8.0);
        self.label(Rect::new(lx, r.y, r.right() - lx, r.h), label, Font::Body, fg);
        it.clicked
    }

    pub fn radio(&mut self, id: Id, r: Rect, label: &str, checked: bool, enabled: bool) -> bool {
        let it = self.interact(id, r, enabled);
        let t = self.t;
        let s = self.px(20.0) as f32;
        let cx = r.x as f32 + s / 2.0;
        let cy = r.y as f32 + r.h as f32 / 2.0;
        if checked {
            self.c.fill_circle(cx, cy, s / 2.0, if enabled { t.accent } else { t.text_disabled });
            let inner = if it.pressed { 4.0 } else if it.hovered { 5.0 } else { 4.5 } * self.c.scale;
            self.c.fill_circle(cx, cy, inner, t.on_accent);
        } else {
            if it.hovered {
                self.c.fill_circle(cx, cy, s / 2.0, t.subtle_hover);
            }
            self.c.stroke_circle(cx, cy, s / 2.0, 1.0, if enabled { t.text2 } else { t.text_disabled });
        }
        if !label.is_empty() {
            let lx = r.x + s as i32 + self.px(8.0);
            let fg = if enabled { t.text } else { t.text_disabled };
            self.label(Rect::new(lx, r.y, r.right() - lx, r.h), label, Font::Body, fg);
        }
        it.clicked
    }

    /// 0..=100 slider. Returns Some(new value) while the user drags or clicks.
    pub fn slider(&mut self, id: Id, r: Rect, value: i32, enabled: bool, opacity: f32) -> Option<i32> {
        let it = self.interact(id, r, enabled);
        let t = self.t;
        let thumb_r = self.px(10.0) as f32;
        let x0 = r.x as f32 + thumb_r;
        let x1 = r.right() as f32 - thumb_r;
        let mut v = value.clamp(0, 100);
        let mut changed = None;
        if self.st.active == id && enabled {
            let nv = (((self.mx as f32 - x0) / (x1 - x0).max(1.0)) * 100.0).round().clamp(0.0, 100.0) as i32;
            if nv != v {
                v = nv;
                changed = Some(nv);
            }
            self.repaint = true;
        }
        let cy = r.y as f32 + r.h as f32 / 2.0;
        let pos = x0 + (x1 - x0) * v as f32 / 100.0;
        let th = self.px(4.0);
        let track = Rect::new(x0 as i32 - th / 2, cy as i32 - th / 2, (x1 - x0) as i32 + th, th);
        let accent = if enabled { t.accent } else { t.text_disabled };
        self.c.fill_round_rect(track, th as f32 / 2.0, t.slider_track.with_alpha(opacity));
        let filled = Rect::new(track.x, track.y, (pos as i32 - track.x).max(th), th);
        self.c.fill_round_rect(filled, th as f32 / 2.0, accent.with_alpha(opacity));
        // Thumb: outer disc with a hairline border, inner accent disc that grows on hover.
        self.c.fill_circle(pos, cy, thumb_r, t.thumb_outer.with_alpha(opacity.max(0.6)));
        self.c.stroke_circle(pos, cy, thumb_r, 1.0, t.control_stroke.with_alpha(opacity.max(0.6)));
        let inner = if it.pressed || self.st.active == id { 5.0 } else if it.hovered { 7.0 } else { 6.0 } * self.c.scale;
        self.c.fill_circle(pos, cy, inner, accent.with_alpha(opacity.max(0.6)));
        changed
    }

    /// Read-only box that looks like a TextBox.
    pub fn readonly_box(&mut self, r: Rect, text: &str) {
        let t = self.t;
        let rad = self.px(4.0) as f32;
        self.c.fill_round_rect(r, rad, t.input_bg);
        self.c.control_border(r, rad, t.control_stroke, t.text3);
        let pad = self.px(11.0);
        self.label(Rect::new(r.x + pad, r.y, r.w - 2 * pad, r.h), text, Font::Body, t.text);
    }

    /// Editable single-line text box. Returns true when Enter was pressed while focused.
    pub fn text_box(&mut self, id: Id, r: Rect, edit: &mut TextEdit, placeholder: &str) -> bool {
        let over = self.input_enabled && self.mouse_in(r);
        if over {
            self.next_hot = id;
            self.next_cursor = CursorKind::IBeam;
        }
        let focused = self.st.focus == id;
        let t = self.t;
        let pad = self.px(11.0);
        let inner = Rect::new(r.x + pad, r.y, r.w - 2 * pad, r.h);
        let pos = self.c.char_positions(&edit.text, Font::Body);
        let hit = |mx: i32, scroll: i32| -> usize {
            let x = mx - inner.x + scroll;
            let mut best = 0;
            for (i, &p) in pos.iter().enumerate() {
                if (p - x).abs() < (pos[best] - x).abs() {
                    best = i;
                }
            }
            best
        };
        let mut submitted = false;
        let ctrl_shift = |ev: &Ev| match ev {
            Ev::Key { shift, ctrl, .. } => (*shift, *ctrl),
            _ => (false, false),
        };
        match self.ev {
            Some(Ev::MouseDown) if over => {
                self.st.focus = id;
                self.st.active = id;
                let i = hit(self.mx, edit.scroll_x);
                edit.caret = i;
                if !(unsafe { GetKeyState(VK_SHIFT as i32) } < 0) {
                    edit.anchor = i;
                }
                self.st.caret_visible = true;
                self.take_event();
            }
            Some(Ev::Key { vk, .. }) if focused => {
                let (shift, ctrl) = ctrl_shift(&self.ev.unwrap());
                let mut moved = None;
                match vk {
                    VK_LEFT => moved = Some(if !shift && edit.caret != edit.anchor { edit.sel().0 } else { edit.prev(edit.caret) }),
                    VK_RIGHT => moved = Some(if !shift && edit.caret != edit.anchor { edit.sel().1 } else { edit.next(edit.caret) }),
                    VK_HOME => moved = Some(0),
                    VK_END => moved = Some(edit.text.len()),
                    VK_BACK => {
                        if !edit.delete_sel() && edit.caret > 0 {
                            let p = edit.prev(edit.caret);
                            edit.text.drain(p..edit.caret);
                            edit.caret = p;
                            edit.anchor = p;
                        }
                    }
                    VK_DELETE => {
                        if !edit.delete_sel() && edit.caret < edit.text.len() {
                            let n = edit.next(edit.caret);
                            edit.text.drain(edit.caret..n);
                        }
                    }
                    VK_RETURN => submitted = true,
                    0x41 if ctrl => {
                        edit.anchor = 0;
                        edit.caret = edit.text.len();
                    }
                    0x43 | 0x58 if ctrl => {
                        let (a, b) = edit.sel();
                        if a != b {
                            clipboard_set(&edit.text[a..b]);
                            if vk == 0x58 {
                                edit.delete_sel();
                            }
                        }
                    }
                    0x56 if ctrl => {
                        if let Some(s) = clipboard_get() {
                            let line: Vec<u16> = s.into_iter().take_while(|&c| c != 13 && c != 10).collect();
                            edit.insert(&line);
                        }
                    }
                    _ => {}
                }
                if let Some(m) = moved {
                    edit.caret = m;
                    if !shift {
                        edit.anchor = m;
                    }
                }
                self.st.caret_visible = true;
                self.take_event();
            }
            Some(Ev::Char(ch)) if focused => {
                if ch >= 0x20 && ch != 0x7F {
                    edit.insert(&[ch]);
                }
                self.st.caret_visible = true;
                self.take_event();
            }
            _ => {}
        }
        if self.st.active == id && focused && matches!(self.ev, None) {
            // Drag-select.
            let i = hit(self.mx, edit.scroll_x);
            if i != edit.caret {
                edit.caret = i;
                self.repaint = true;
            }
        }

        // Keep the caret in view.
        let pos = self.c.char_positions(&edit.text, Font::Body);
        let cx = pos[edit.caret.min(pos.len() - 1)];
        if cx - edit.scroll_x > inner.w - 2 {
            edit.scroll_x = cx - inner.w + 2;
        }
        if cx - edit.scroll_x < 0 {
            edit.scroll_x = cx;
        }
        let total = *pos.last().unwrap_or(&0);
        edit.scroll_x = edit.scroll_x.clamp(0, (total - inner.w + 2).max(0));

        let rad = self.px(4.0) as f32;
        let hovered = self.st.hot == id;
        self.c.fill_round_rect(r, rad, if focused { t.input_focus_bg } else if hovered { t.control_hover } else { t.input_bg });
        self.c.control_border(r, rad, t.control_stroke, if focused { t.accent } else { t.text3 });
        if focused {
            // Focused TextBox: 2px accent underline following the rounded corners.
            self.c.push_clip(Rect::new(r.x, r.bottom() - self.px(2.0), r.w, self.px(2.0)));
            self.c.fill_round_rect(r, rad, t.accent);
            self.c.pop_clip();
        }

        self.c.push_clip(inner);
        let (a, b) = edit.sel();
        if focused && a != b {
            let sx = inner.x + pos[a] - edit.scroll_x;
            let ex = inner.x + pos[b] - edit.scroll_x;
            let h = self.px(20.0);
            self.c.fill_rect(Rect::new(sx, r.y + (r.h - h) / 2, ex - sx, h), t.accent.with_alpha(0.45));
        }
        if edit.text.is_empty() {
            if !placeholder.is_empty() {
                self.label(inner, placeholder, Font::Body, t.text2);
            }
        } else {
            let tr = Rect::new(inner.x - edit.scroll_x, r.y, total + self.px(4.0), r.h);
            let s = edit.value();
            self.c.text(tr, &s, Font::Body, t.text, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
        }
        if focused && self.st.caret_visible {
            let x = inner.x + cx - edit.scroll_x;
            let h = self.px(18.0);
            self.c.fill_rect(Rect::new(x, r.y + (r.h - h) / 2, self.px(1.0).max(1), h), t.text);
        }
        self.c.pop_clip();
        submitted
    }

    pub fn draw_tooltip(&mut self) {
        let Some((anchor, text)) = self.tooltip.take() else { return };
        let t = self.t;
        let (w, _) = self.c.measure(&text, Font::Caption);
        let pad = self.px(8.0);
        let h = self.px(28.0);
        let tw = w + 2 * pad;
        let mut x = self.mx - tw / 2;
        let mut y = anchor.bottom() + self.px(6.0);
        if y + h > self.c.height() {
            y = anchor.y - h - self.px(6.0);
        }
        x = x.clamp(self.px(4.0), (self.c.width() - tw - self.px(4.0)).max(0));
        let r = Rect::new(x, y, tw, h);
        let rad = self.px(4.0) as f32;
        self.c.fill_round_rect(r, rad, t.tooltip);
        self.c.stroke_round_rect(r, rad, 1.0, t.card_stroke);
        self.c.text(r, &text, Font::Caption, t.text, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    }

    pub fn image(&mut self, r: Rect, img: &Image, opacity: f32) {
        let x = r.x + (r.w - img.w) / 2;
        let y = r.y + (r.h - img.h) / 2;
        self.c.image(x, y, img, opacity);
    }
}

fn clipboard_set(text: &[u16]) {
    unsafe {
        if OpenClipboard(0) == 0 {
            return;
        }
        EmptyClipboard();
        let bytes = (text.len() + 1) * 2;
        let h = GlobalAlloc(GMEM_MOVEABLE, bytes);
        if h != 0 {
            let p = GlobalLock(h) as *mut u16;
            if !p.is_null() {
                core::ptr::copy_nonoverlapping(text.as_ptr(), p, text.len());
                *p.add(text.len()) = 0;
                GlobalUnlock(h);
                if SetClipboardData(CF_UNICODETEXT, h) == 0 {
                    GlobalFree(h);
                }
            }
        }
        CloseClipboard();
    }
}

fn clipboard_get() -> Option<Vec<u16>> {
    unsafe {
        if OpenClipboard(0) == 0 {
            return None;
        }
        let h = GetClipboardData(CF_UNICODETEXT);
        let mut out = None;
        if h != 0 {
            let p = GlobalLock(h) as *const u16;
            if !p.is_null() {
                let mut n = 0;
                while *p.add(n) != 0 {
                    n += 1;
                }
                out = Some(core::slice::from_raw_parts(p, n).to_vec());
                GlobalUnlock(h);
            }
        }
        CloseClipboard();
        out
    }
}
