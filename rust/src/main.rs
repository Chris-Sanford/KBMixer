//! KBMixer: adjust per-app volume with global hotkeys + mouse wheel. Zero-dependency Rust port.
#![cfg_attr(not(test), windows_subsystem = "windows")]

mod app;
mod audio;
mod com;
mod config;
mod gfx;
mod icons;
mod input;
mod json;
mod keys;
mod startup;
mod text;
mod tray;
mod ui;
mod update;
mod win;
mod worker;

fn main() {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let has = |flag: &str| args.iter().any(|a| a.eq_ignore_ascii_case(flag));

    if has("--list-audio-sessions") {
        let path = worker::write_session_diagnostic_file();
        let _com = com::ComInit::sta();
        win::shell_open(&path.display().to_string());
        return;
    }

    unsafe { win::SetProcessDpiAwarenessContext(win::DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) };
    let _com = com::ComInit::sta();
    app::run(has(startup::MINIMIZED_FLAG));
}
