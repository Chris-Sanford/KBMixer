//! Profiles stored as %APPDATA%\KBMixer\{ConfigId}.json, byte-compatible with the .NET build.

use crate::audio::DeviceIdentity;
use crate::json::{self, ObjectWriter, Value};
use crate::keys;
use crate::text::{eq_ic, is_blank};
use std::path::PathBuf;

#[derive(Clone, Debug, PartialEq)]
pub struct Config {
    pub config_id: String,
    pub device_id: String,
    pub app_file_name: String,
    pub app_friendly_name: String,
    pub hotkeys: Vec<i32>,
    pub control_single_session: bool,
    pub process_index: i32,
    /// When true, hotkeys + wheel adjust this output device's master volume instead of a single app.
    pub control_device_master_volume: bool,
    /// When set, shown in the profile list instead of the auto-generated name.
    pub custom_display_name: Option<String>,
    /// Endpoint name when `device_id` was chosen; endpoint ids can rotate, so this lets us re-bind.
    pub device_friendly_name: Option<String>,
    /// Adapter name; survives the user renaming the endpoint in Sound settings.
    pub device_description: Option<String>,
}

#[derive(Debug, PartialEq, Eq, Clone, Copy)]
pub enum Reconcile {
    Unchanged,
    Backfilled,
    Rematched,
    Orphaned,
}

pub fn data_dir() -> PathBuf {
    // Test hook: run against a scratch profile folder instead of the real one.
    if let Some(d) = std::env::var_os("KBMIXER_DATA_DIR").filter(|d| !d.is_empty()) {
        return PathBuf::from(d);
    }
    let base = std::env::var_os("APPDATA").map(PathBuf::from).unwrap_or_else(std::env::temp_dir);
    base.join("KBMixer")
}

fn names_equal(a: &str, b: Option<&str>) -> bool {
    !is_blank(a) && eq_ic(a.trim(), b.unwrap_or("").trim())
}

fn non_blank(s: &Option<String>) -> bool {
    s.as_deref().is_some_and(|s| !is_blank(s))
}

impl Config {
    pub fn new_empty(id: String, device: Option<&DeviceIdentity>) -> Config {
        Config {
            config_id: id,
            device_id: device.map(|d| d.id.clone()).unwrap_or_default(),
            app_file_name: String::new(),
            app_friendly_name: String::new(),
            hotkeys: Vec::new(),
            control_single_session: false,
            process_index: 0,
            control_device_master_volume: false,
            custom_display_name: None,
            device_friendly_name: device.map(|d| d.friendly_name.clone()),
            device_description: device.and_then(|d| d.description.clone()),
        }
    }

    pub fn has_target(&self) -> bool {
        self.control_device_master_volume || !is_blank(&self.app_file_name) || !is_blank(&self.app_friendly_name)
    }

    pub fn set_device(&mut self, id: &str, friendly: &str, description: Option<&str>) {
        self.device_id = id.to_string();
        if !is_blank(friendly) {
            self.device_friendly_name = Some(friendly.to_string());
        }
        if let Some(d) = description.filter(|d| !is_blank(d)) {
            self.device_description = Some(d.to_string());
        }
    }

    /// Re-point at a live device when the stored endpoint id no longer exists.
    /// Match order: exact id → friendly name + description → friendly name → description (only if unique).
    pub fn reconcile_device(&mut self, devices: &[DeviceIdentity]) -> Reconcile {
        if devices.is_empty() {
            return Reconcile::Unchanged;
        }
        if let Some(exact) = devices.iter().find(|d| eq_ic(&d.id, &self.device_id)) {
            let mut changed = false;
            if !non_blank(&self.device_friendly_name) && !is_blank(&exact.friendly_name) {
                self.device_friendly_name = Some(exact.friendly_name.clone());
                changed = true;
            }
            if !non_blank(&self.device_description) && non_blank(&exact.description) {
                self.device_description = exact.description.clone();
                changed = true;
            }
            return if changed { Reconcile::Backfilled } else { Reconcile::Unchanged };
        }

        let has_name = non_blank(&self.device_friendly_name);
        let has_desc = non_blank(&self.device_description);
        if !has_name && !has_desc {
            return Reconcile::Orphaned;
        }
        let name = self.device_friendly_name.as_deref();
        let desc = self.device_description.as_deref();

        let mut found = None;
        if has_name && has_desc {
            found = devices.iter().find(|d| names_equal(&d.friendly_name, name) && names_equal(d.description.as_deref().unwrap_or(""), desc));
        }
        if found.is_none() && has_name {
            found = devices.iter().find(|d| names_equal(&d.friendly_name, name));
        }
        if found.is_none() && has_desc {
            let by_desc: Vec<_> = devices.iter().filter(|d| names_equal(d.description.as_deref().unwrap_or(""), desc)).collect();
            if by_desc.len() == 1 {
                found = Some(by_desc[0]);
            }
        }
        match found.cloned() {
            Some(m) => {
                self.set_device(&m.id, &m.friendly_name, m.description.as_deref());
                Reconcile::Rematched
            }
            None => Reconcile::Orphaned,
        }
    }

    pub fn auto_display_name(&self, device_friendly_name: Option<&str>) -> String {
        if !self.has_target() {
            return "New profile".to_string();
        }
        let keys = if self.hotkeys.is_empty() {
            "(no hotkeys)".to_string()
        } else {
            keys::join(&self.hotkeys)
        };
        let dev = match device_friendly_name {
            Some(d) if !is_blank(d) => d,
            _ => "(unknown device)",
        };
        if self.control_device_master_volume {
            return format!("Control {dev} master volume with {keys}");
        }
        let app = if is_blank(&self.app_friendly_name) { "(no app)" } else { &self.app_friendly_name };
        format!("Control {app} with {keys} on {dev}")
    }

    pub fn to_json(&self) -> String {
        let mut w = ObjectWriter::new();
        w.string("ConfigId", &self.config_id);
        w.string("DeviceId", &self.device_id);
        w.string("AppFileName", &self.app_file_name);
        w.string("AppFriendlyName", &self.app_friendly_name);
        w.int_array("Hotkeys", &self.hotkeys);
        w.bool("ControlSingleSession", self.control_single_session);
        w.int("ProcessIndex", self.process_index as i64);
        w.bool("ControlDeviceMasterVolume", self.control_device_master_volume);
        w.opt_string("CustomDisplayName", self.custom_display_name.as_deref());
        w.opt_string("DeviceFriendlyName", self.device_friendly_name.as_deref());
        w.opt_string("DeviceDescription", self.device_description.as_deref());
        w.finish()
    }

    /// Same rules as System.Text.Json with default options: case-sensitive names, `required` members must exist.
    pub fn from_json(src: &str) -> Result<Config, String> {
        let v = json::parse(src)?;
        let Value::Object(_) = v else { return Err("not an object".into()) };

        let req = |k: &str| v.get(k).ok_or_else(|| format!("missing required property '{k}'"));
        let string = |val: &Value, k: &str| -> Result<String, String> {
            match val {
                Value::String(s) => Ok(s.clone()),
                Value::Null => Ok(String::new()),
                _ => Err(format!("'{k}' must be a string")),
            }
        };
        let opt_string = |k: &str| -> Result<Option<String>, String> {
            match v.get(k) {
                None | Some(Value::Null) => Ok(None),
                Some(Value::String(s)) => Ok(Some(s.clone())),
                _ => Err(format!("'{k}' must be a string")),
            }
        };
        let boolean = |val: &Value, k: &str| match val {
            Value::Bool(b) => Ok(*b),
            _ => Err(format!("'{k}' must be a boolean")),
        };
        let int = |val: &Value, k: &str| match val {
            Value::Number(n) if n.fract() == 0.0 && *n >= i32::MIN as f64 && *n <= i32::MAX as f64 => Ok(*n as i32),
            _ => Err(format!("'{k}' must be an integer")),
        };

        let config_id = match v.get("ConfigId") {
            None => "00000000-0000-0000-0000-000000000000".to_string(),
            Some(Value::String(s)) if is_guid(s) => s.to_ascii_lowercase(),
            _ => return Err("'ConfigId' is not a GUID".into()),
        };
        let hotkeys = match req("Hotkeys")? {
            Value::Null => Vec::new(),
            Value::Array(a) => a.iter().map(|x| int(x, "Hotkeys")).collect::<Result<_, _>>()?,
            _ => return Err("'Hotkeys' must be an array".into()),
        };

        Ok(Config {
            config_id,
            device_id: string(req("DeviceId")?, "DeviceId")?,
            app_file_name: string(req("AppFileName")?, "AppFileName")?,
            app_friendly_name: string(req("AppFriendlyName")?, "AppFriendlyName")?,
            hotkeys,
            control_single_session: boolean(req("ControlSingleSession")?, "ControlSingleSession")?,
            process_index: int(req("ProcessIndex")?, "ProcessIndex")?,
            control_device_master_volume: match v.get("ControlDeviceMasterVolume") {
                None => false,
                Some(x) => boolean(x, "ControlDeviceMasterVolume")?,
            },
            custom_display_name: opt_string("CustomDisplayName")?,
            device_friendly_name: opt_string("DeviceFriendlyName")?,
            device_description: opt_string("DeviceDescription")?,
        })
    }

    fn path(&self) -> PathBuf {
        data_dir().join(format!("{}.json", self.config_id))
    }

    pub fn save(&self) {
        let dir = data_dir();
        let _ = std::fs::create_dir_all(&dir);
        if let Err(e) = std::fs::write(self.path(), self.to_json()) {
            crate::win::debug_log(&format!("save config failed: {e}"));
        }
    }

    pub fn delete(&self) {
        let p = self.path();
        if p.exists() {
            if let Err(e) = std::fs::remove_file(p) {
                crate::win::debug_log(&format!("Failed to delete config: {e}"));
            }
        }
    }
}

fn is_guid(s: &str) -> bool {
    let t = s.trim_matches(|c| c == '{' || c == '}');
    let b = t.as_bytes();
    b.len() == 36
        && b.iter().enumerate().all(|(i, c)| if matches!(i, 8 | 13 | 18 | 23) { *c == b'-' } else { c.is_ascii_hexdigit() })
}

pub struct LoadResult {
    pub configs: Vec<Config>,
    /// Set when corrupt files were found (and deleted), for the startup error dialog.
    pub error: Option<String>,
}

pub fn load_all() -> LoadResult {
    let dir = data_dir();
    let Ok(entries) = std::fs::read_dir(&dir) else {
        return LoadResult { configs: Vec::new(), error: None };
    };
    let mut files: Vec<PathBuf> = entries
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().and_then(|e| e.to_str()).is_some_and(|e| e.eq_ignore_ascii_case("json")))
        .collect();
    files.sort_by_key(|p| p.file_name().map(|n| n.to_string_lossy().to_lowercase()));

    let mut configs = Vec::new();
    let mut failed = Vec::new();
    for f in files {
        match std::fs::read(&f) {
            Ok(bytes) => {
                let text = String::from_utf8_lossy(&bytes).into_owned();
                match Config::from_json(&text) {
                    Ok(c) => configs.push(c),
                    Err(_) => failed.push((f, text)),
                }
            }
            Err(_) => failed.push((f, "[Unable to read file content]".to_string())),
        }
    }

    let error = (!failed.is_empty()).then(|| {
        let mut sb = String::from("Failed to load one or more configurations. These configurations will be deleted:\n");
        for (path, content) in failed {
            let name = path.file_name().map(|n| n.to_string_lossy().into_owned()).unwrap_or_default();
            sb.push_str(&format!("\nFile: {name}\nContent: {content}\n"));
            if let Err(e) = std::fs::remove_file(&path) {
                sb.push_str(&format!("Failed to delete file: {e}\n"));
            }
        }
        sb
    });
    LoadResult { configs, error }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn dev(id: &str, name: &str, desc: &str) -> DeviceIdentity {
        DeviceIdentity { id: id.into(), friendly_name: name.into(), description: Some(desc.into()) }
    }

    fn base() -> Config {
        let mut c = Config::new_empty("3e0d19f1-6ba0-48c7-b886-f2f6ce0a4802".into(), None);
        c.device_id = "device-1".into();
        c
    }

    #[test]
    fn empty_profile_is_unconfigured() {
        let c = base();
        assert!(!c.has_target());
        assert_eq!(c.auto_display_name(Some("Speakers")), "New profile");
    }

    #[test]
    fn reconcile_prefers_name_and_description() {
        let mut c = base();
        c.device_id = "old".into();
        c.device_friendly_name = Some("Speakers".into());
        c.device_description = Some("USB DAC".into());
        let r = c.reconcile_device(&[dev("wrong", "Speakers", "Onboard Audio"), dev("match", "Speakers", "USB DAC")]);
        assert_eq!(r, Reconcile::Rematched);
        assert_eq!(c.device_id, "match");
    }

    #[test]
    fn reconcile_backfills_identity() {
        let mut c = base();
        c.device_id = "live".into();
        let r = c.reconcile_device(&[dev("live", "Speakers", "USB DAC")]);
        assert_eq!(r, Reconcile::Backfilled);
        assert_eq!(c.device_friendly_name.as_deref(), Some("Speakers"));
        assert_eq!(c.device_description.as_deref(), Some("USB DAC"));
    }

    #[test]
    fn json_matches_dotnet_layout() {
        let src = r#"{
  "ConfigId": "3e0d19f1-6ba0-48c7-b886-f2f6ce0a4802",
  "DeviceId": "{0.0.0.00000000}.{0497471a-3389-4185-a065-05f27e28a929}",
  "AppFileName": "chrome.exe",
  "AppFriendlyName": "Google Chrome",
  "Hotkeys": [
    16
  ],
  "ControlSingleSession": false,
  "ProcessIndex": 0,
  "ControlDeviceMasterVolume": false,
  "CustomDisplayName": "Chrome",
  "DeviceFriendlyName": "Hyper-X Game (HyperX Cloud Alpha S Game)",
  "DeviceDescription": "HyperX Cloud Alpha S Game"
}"#;
        let c = Config::from_json(src).unwrap();
        assert_eq!(c.to_json(), src);
        assert_eq!(c.hotkeys, vec![16]);
    }

    #[test]
    fn missing_required_fails() {
        assert!(Config::from_json(r#"{"ConfigId":"3e0d19f1-6ba0-48c7-b886-f2f6ce0a4802","DeviceId":"x"}"#).is_err());
    }
}
