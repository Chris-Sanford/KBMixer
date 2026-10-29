//! Virtual key codes → short labels for the hotkey UI (same table as the .NET KeyDisplayNames).

pub fn display_name(vk: i32) -> String {
    let s: &str = match vk {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0C => "Clear",
        0x0D => "Enter",
        0x10 | 0xA0 | 0xA1 => "Shift",
        0x11 | 0xA2 | 0xA3 => "Ctrl",
        0x12 | 0xA4 | 0xA5 => "Alt",
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x15 => "Kana",
        0x17 => "Junja",
        0x18 => "Final",
        0x19 => "Hanja",
        0x1B => "Esc",
        0x1C => "IME Convert",
        0x1D => "IME Nonconvert",
        0x1E => "IME Accept",
        0x1F => "IME Mode Change",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left Arrow",
        0x26 => "Up Arrow",
        0x27 => "Right Arrow",
        0x28 => "Down Arrow",
        0x29 => "Select",
        0x2A => "Print",
        0x2B => "Execute",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x2F => "Help",
        0x5B | 0x5C => "Win",
        0x5D => "Menu",
        0x5F => "Sleep",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6C => "Num Separator",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0x90 => "Num Lock",
        0x91 => "Scroll Lock",
        0xA6 => "Browser Back",
        0xA7 => "Browser Forward",
        0xA8 => "Browser Refresh",
        0xA9 => "Browser Stop",
        0xAA => "Browser Search",
        0xAB => "Browser Favorites",
        0xAC => "Browser Home",
        0xAD => "Volume Mute",
        0xAE => "Volume Down",
        0xAF => "Volume Up",
        0xB0 => "Next Track",
        0xB1 => "Previous Track",
        0xB2 => "Media Stop",
        0xB3 => "Play/Pause",
        0xB4 => "Mail",
        0xB5 => "Media Select",
        0xB6 => "App 1",
        0xB7 => "App 2",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC | 0xE2 => "\\",
        0xDD => "]",
        0xDE => "'",
        0xDF => "OEM 8",
        // Fallbacks mirror Windows.System.VirtualKey's enum names.
        0x01 => "LeftButton",
        0x02 => "RightButton",
        0x03 => "Cancel",
        0x04 => "MiddleButton",
        0x05 => "XButton1",
        0x06 => "XButton2",
        0x60..=0x69 => return format!("Num {}", vk - 0x60),
        0x70..=0x87 => return format!("F{}", vk - 0x70 + 1),
        0x30..=0x39 => return format!("Number{}", vk - 0x30),
        0x41..=0x5A => return char::from(vk as u8).to_string(),
        _ => return vk.to_string(),
    };
    s.to_string()
}

/// "Shift + Ctrl + M"
pub fn join(keys: &[i32]) -> String {
    keys.iter().map(|&k| display_name(k)).collect::<Vec<_>>().join(" + ")
}

#[cfg(test)]
mod tests {
    #[test]
    fn names() {
        assert_eq!(super::join(&[16, 77, 0x70, 0x63]), "Shift + M + F1 + Num 3");
    }
}
