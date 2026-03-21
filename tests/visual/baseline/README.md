# Visual baseline (`kbmixer.png`)

This image is produced with **mock mixer data** so CI and local runs do not depend on your audio devices.

## Regenerate

From the repo root (100% display scaling recommended):

```powershell
dotnet run --project KBMixer\KBMixer.csproj -c Release -- --ui-golden=tests\visual\baseline\kbmixer.png
```

Or use the helper (writes `artifacts/ui/actual.png` and optionally SSIM-compares):

```powershell
.\tools\VisualCompare.ps1 -SkipCompare
# Then copy artifacts\ui\actual.png over this baseline after review.
```

## When to update the baseline

- Intentional UI changes to `MainWindow.xaml` or `Themes/FluentSettingsDark.xaml`.
- After completing items in `docs/ui-definition-of-done.md`, if the new UI is approved.

Always review the diff visually before committing a new baseline.
