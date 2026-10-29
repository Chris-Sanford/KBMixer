//! Per-user logon entry under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.

use crate::text::eq_ic;
use crate::win::*;

const RUN_KEY: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
const VALUE_NAME: &str = "KBMixer";
/// Passed on the command line so logon starts go straight to the notification area.
pub const MINIMIZED_FLAG: &str = "--minimized";

fn current_exe() -> Option<String> {
    std::env::current_exe().ok().map(|p| p.display().to_string())
}

pub fn is_registered_for_current_exe() -> bool {
    let Some(raw) = read_run_value() else { return false };
    let Some(exe) = parse_executable(&raw) else { return false };
    let Some(this) = current_exe() else { return false };
    same_file(&exe, &this)
}

fn read_run_value() -> Option<String> {
    let sub = wide(RUN_KEY);
    let name = wide(VALUE_NAME);
    let mut len = 0u32;
    unsafe {
        if RegGetValueW(HKEY_CURRENT_USER, sub.as_ptr(), name.as_ptr(), RRF_RT_REG_SZ, core::ptr::null_mut(), core::ptr::null_mut(), &mut len) != ERROR_SUCCESS {
            return None;
        }
        let mut buf = vec![0u16; (len as usize / 2) + 1];
        let mut len2 = (buf.len() * 2) as u32;
        if RegGetValueW(HKEY_CURRENT_USER, sub.as_ptr(), name.as_ptr(), RRF_RT_REG_SZ, core::ptr::null_mut(), buf.as_mut_ptr() as *mut _, &mut len2) != ERROR_SUCCESS {
            return None;
        }
        Some(from_wide(&buf))
    }
}

pub fn set_registered(enable: bool) -> Result<(), String> {
    let sub = wide(RUN_KEY);
    let name = wide(VALUE_NAME);
    let mut key: HKEY = 0;
    unsafe {
        let rc = RegOpenKeyExW(HKEY_CURRENT_USER, sub.as_ptr(), 0, KEY_READ | KEY_WRITE, &mut key);
        if rc != ERROR_SUCCESS {
            return Err(r"Could not open HKCU\Software\Microsoft\Windows\CurrentVersion\Run.".into());
        }
        let result = if enable {
            match current_exe() {
                None => Err("Could not determine the executable path.".to_string()),
                Some(exe) => {
                    let cmd = wide(&format!("\"{exe}\" {MINIMIZED_FLAG}"));
                    let rc = RegSetValueExW(key, name.as_ptr(), 0, REG_SZ, cmd.as_ptr() as *const u8, (cmd.len() * 2) as u32);
                    if rc == ERROR_SUCCESS { Ok(()) } else { Err(format!("RegSetValueEx failed ({rc}).")) }
                }
            }
        } else {
            RegDeleteValueW(key, name.as_ptr());
            Ok(())
        };
        RegCloseKey(key);
        result
    }
}

pub fn parse_executable(value: &str) -> Option<String> {
    let v = value.trim();
    if v.is_empty() {
        return None;
    }
    if let Some(rest) = v.strip_prefix('"') {
        let end = rest.find('"')?;
        let exe = &rest[..end];
        return (!exe.is_empty()).then(|| exe.to_string());
    }
    let exe = v.split(' ').next().unwrap_or("");
    (!exe.is_empty()).then(|| exe.to_string())
}

fn same_file(a: &str, b: &str) -> bool {
    let norm = |p: &str| std::path::absolute(p.trim()).map(|p| p.display().to_string()).unwrap_or_else(|_| p.trim().to_string());
    eq_ic(&norm(a), &norm(b))
}

#[cfg(test)]
mod tests {
    #[test]
    fn parses_run_values() {
        assert_eq!(super::parse_executable(r#""C:\x y\KBMixer.exe" --minimized"#).as_deref(), Some(r"C:\x y\KBMixer.exe"));
        assert_eq!(super::parse_executable(r"C:\KBMixer.exe --minimized").as_deref(), Some(r"C:\KBMixer.exe"));
        assert_eq!(super::parse_executable("  "), None);
    }
}
