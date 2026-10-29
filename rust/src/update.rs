//! Checks the latest GitHub release over WinHTTP (no TLS or HTTP crates).

use crate::json;
use crate::win::*;
use core::ffi::c_void;
use core::ptr::null;

const HOST: &str = "api.github.com";
const PATH: &str = "/repos/Chris-Sanford/KBMixer/releases/latest";
pub const RELEASES_PAGE: &str = "https://github.com/Chris-Sanford/KBMixer/releases/latest";

pub fn current_version() -> (u32, u32, u32) {
    parse_version(env!("CARGO_PKG_VERSION")).unwrap_or((0, 0, 0))
}

pub fn version_string(v: (u32, u32, u32)) -> String {
    format!("{}.{}.{}", v.0, v.1, v.2)
}

/// "v2.0.0-r123" → (2,0,0). None if the tag is not a version.
pub fn parse_version(tag: &str) -> Option<(u32, u32, u32)> {
    let s = tag.trim().trim_start_matches(['v', 'V']);
    let s = s.split('-').next()?;
    let parts: Vec<&str> = s.split('.').collect();
    if parts.len() < 2 || parts.len() > 4 {
        return None;
    }
    let n = |i: usize| -> Option<u32> { parts.get(i).map_or(Some(0), |p| p.parse().ok()) };
    Some((n(0)?, n(1)?, n(2)?))
}

pub enum UpdateResult {
    Available { latest: String, current: String, url: String },
    UpToDate { current: String },
    Failed(String),
    Skipped,
}

pub fn check() -> UpdateResult {
    if std::env::var_os("KBMIXER_NO_UPDATE_CHECK").is_some_and(|v| !v.is_empty()) {
        return UpdateResult::Skipped;
    }
    let current = current_version();
    if current == (0, 0, 0) {
        return UpdateResult::Skipped;
    }
    let body = match http_get() {
        Ok(b) => b,
        Err(e) => return UpdateResult::Failed(e),
    };
    let root = match json::parse(&body) {
        Ok(v) => v,
        Err(e) => return UpdateResult::Failed(e),
    };
    let tag = root.get("tag_name").and_then(|v| v.as_str()).unwrap_or("");
    let html = root.get("html_url").and_then(|v| v.as_str()).map(str::to_string);
    let mut download = None;
    let mut fallback_exe = None;
    for a in root.get("assets").and_then(|v| v.as_array()).unwrap_or(&[]) {
        let name = a.get("name").and_then(|v| v.as_str()).unwrap_or("");
        let Some(url) = a.get("browser_download_url").and_then(|v| v.as_str()) else { continue };
        if name.eq_ignore_ascii_case("KBMixer-self-contained.zip") {
            download = Some(url.to_string());
            break;
        }
        if fallback_exe.is_none() && url.to_ascii_lowercase().ends_with(".exe") {
            fallback_exe = Some(url.to_string());
        }
    }
    let Some(latest) = parse_version(tag) else {
        return UpdateResult::Failed(format!("Unrecognized release tag '{tag}'."));
    };
    if latest > current {
        UpdateResult::Available {
            latest: version_string(latest),
            current: version_string(current),
            url: download.or(fallback_exe).or(html).unwrap_or_else(|| RELEASES_PAGE.to_string()),
        }
    } else {
        UpdateResult::UpToDate { current: version_string(current) }
    }
}

struct H(*mut c_void);
impl Drop for H {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe { WinHttpCloseHandle(self.0) };
        }
    }
}

fn last_error(what: &str) -> String {
    let code = unsafe { GetLastError() };
    let mut buf = [0u16; 512];
    let n = unsafe {
        let module = wide("winhttp.dll");
        let mut hmod = 0;
        GetModuleHandleExW(0, module.as_ptr(), &mut hmod);
        // FORMAT_MESSAGE_FROM_HMODULE | FROM_SYSTEM | IGNORE_INSERTS
        FormatMessageW(0x800 | 0x1000 | 0x200, hmod as *const c_void, code, 0, buf.as_mut_ptr(), buf.len() as u32, null())
    };
    let msg = String::from_utf16_lossy(&buf[..n as usize]).trim().to_string();
    if msg.is_empty() { format!("{what} failed (error {code}).") } else { msg }
}

fn http_get() -> Result<String, String> {
    unsafe {
        let ua = wide(&format!("KBMixer/{}", version_string(current_version())));
        // WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY = 4
        let session = H(WinHttpOpen(ua.as_ptr(), 4, null(), null(), 0));
        if session.0.is_null() {
            return Err(last_error("WinHttpOpen"));
        }
        WinHttpSetTimeouts(session.0, 15_000, 15_000, 15_000, 15_000);
        let host = wide(HOST);
        let conn = H(WinHttpConnect(session.0, host.as_ptr(), 443, 0));
        if conn.0.is_null() {
            return Err(last_error("WinHttpConnect"));
        }
        let verb = wide("GET");
        let path = wide(PATH);
        // WINHTTP_FLAG_SECURE = 0x00800000
        let req = H(WinHttpOpenRequest(conn.0, verb.as_ptr(), path.as_ptr(), null(), null(), null(), 0x0080_0000));
        if req.0.is_null() {
            return Err(last_error("WinHttpOpenRequest"));
        }
        let headers = wide("Accept: application/vnd.github+json\r\n");
        // WINHTTP_ADDREQ_FLAG_ADD = 0x20000000
        WinHttpAddRequestHeaders(req.0, headers.as_ptr(), u32::MAX, 0x2000_0000);
        if WinHttpSendRequest(req.0, null(), 0, null(), 0, 0, 0) == 0 || WinHttpReceiveResponse(req.0, core::ptr::null_mut()) == 0 {
            return Err(last_error("Request"));
        }
        // WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER
        let mut status = 0u32;
        let mut len = 4u32;
        WinHttpQueryHeaders(req.0, 19 | 0x2000_0000, null(), &mut status as *mut u32 as *mut c_void, &mut len, core::ptr::null_mut());
        let mut body = Vec::new();
        let mut chunk = [0u8; 8192];
        loop {
            let mut read = 0u32;
            if WinHttpReadData(req.0, chunk.as_mut_ptr() as *mut c_void, chunk.len() as u32, &mut read) == 0 {
                return Err(last_error("WinHttpReadData"));
            }
            if read == 0 {
                break;
            }
            body.extend_from_slice(&chunk[..read as usize]);
            if body.len() > 4 << 20 {
                break;
            }
        }
        if !(200..300).contains(&status) {
            return Err(format!("Response status code does not indicate success: {status}."));
        }
        Ok(String::from_utf8_lossy(&body).into_owned())
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn tags() {
        assert_eq!(super::parse_version("v2.1.2-r57"), Some((2, 1, 2)));
        assert_eq!(super::parse_version("2.0"), Some((2, 0, 0)));
        assert_eq!(super::parse_version("nightly"), None);
    }
}
