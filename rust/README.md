# KBMixer (Rust)

A zero-dependency Rust port of KBMixer: no crates, no .NET, and no Windows App SDK runtime. Everything goes straight to Win32 and COM through the hand-written FFI in `src/win.rs` and `src/com.rs`.

| | .NET 8 / WinUI 3 | Rust |
|---|---|---|
| Release download | ~400 MB self-contained exe (or a runtime-dependent zip) | **~600 KB single exe** |
| Startup to window | seconds (single-file extraction + WinUI) | **~130 ms** |
| Memory (working set) | ~180 MB | **~19 MB** (5 MB private) |
| CPU while hidden in tray | — | ~0.2% of one core-second per second |

It reads and writes the same profiles in `%APPDATA%\KBMixer\{id}.json`, byte-for-byte in System.Text.Json's layout, so the two builds can be swapped freely.

## Build

```powershell
rustup toolchain install stable-x86_64-pc-windows-gnu --profile minimal   # once; pinned by rust-toolchain.toml
cargo build --release      # target\release\KBMixer.exe
cargo test
```

The GNU toolchain is pinned because it ships its own linker; the code also builds on `x86_64-pc-windows-msvc` when the Visual Studio C++ tools are installed. `build.rs` compiles the icon, version info and manifest with the Windows SDK's `rc.exe`. For GNU it also converts the `.res` to a COFF object itself, so `windres` isn't needed.

## Layout

| File | What it is |
|---|---|
| `worker.rs` | The audio worker (MTA thread). It owns every Core Audio call, coalesces writes, rate-limits scans, and polls only while the window is visible |
| `input.rs` | A raw-input thread with its own hidden window. It handles hotkeys plus the wheel, and registers for the mouse only while a hotkey is held |
| `audio.rs` | Session grouping, matching and identity rules (pure, unit-tested) |
| `config.rs`, `json.rs` | Profiles and a minimal JSON reader/writer |
| `gfx.rs`, `ui.rs` | The software renderer (anti-aliased shapes, GDI ClearType text, real Mica alpha) and an immediate-mode Fluent widget set |
| `app.rs` | The main window, dialogs, tray and profile logic (port of MainWindow.xaml.cs) |
| `update.rs` | The GitHub release check over WinHTTP |

## Environment variables

- `KBMIXER_NO_UPDATE_CHECK`: skip the GitHub release check (same as the .NET build).
- `KBMIXER_NO_MICA`: use a solid background instead of Mica.
- `KBMIXER_DATA_DIR`: use a different profile folder (handy for testing next to a running copy).
- `KBMIXER_LOG`: append a debug trace (actions, scans, paint timings) to this file.

Command-line flags carry over unchanged: `--minimized` and `--list-audio-sessions`.
