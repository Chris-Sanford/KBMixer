# `--ui-golden` capture mode

KBMixer can render the main window with **fixed mock mixer rows** and write a PNG for visual regression.

## Usage

```powershell
dotnet run --project KBMixer\KBMixer.csproj -c Release -- --ui-golden
dotnet run --project KBMixer\KBMixer.csproj -c Release -- --ui-golden=C:\path\out.png
```

Default output if omitted: `kbmixer-golden.png` in the current working directory.

## DPI and scaling

- Baselines should be recorded at **100% Windows display scaling** so `RenderTargetBitmap` dimensions match layout expectations.
- If you regenerate baselines on a high-DPI display, verify the PNG in an image viewer and update `tests/visual/baseline/kbmixer.png` only after intentional UI changes.

## Behaviour

- Skips raw input registration, tray icon, startup registry sync, and missing-device message boxes.
- Uses `UiGoldenCapture.UseMockMixer` data (see `MainWindow.RebuildMixerStrip`).
- Exits with code `0` after writing the file.
