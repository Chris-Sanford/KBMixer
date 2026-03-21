# UI definition of done (Win11 Volume Mixer parity)

This checklist is the **exit gate** for the WPF “Settings-like” redesign. Work is not “done” until every item is satisfied or explicitly waived (with rationale and screenshot in the PR).

## Automated gates (run every change)

1. `dotnet build KBMixer\KBMixer.csproj -c Release` succeeds with **0 errors**.
2. `dotnet test KBMixer.Tests\KBMixer.Tests.csproj -c Release` — **all tests pass**.
3. **Golden capture** runs without crash:
   ```powershell
   dotnet run --project KBMixer\KBMixer.csproj -c Release -- --ui-golden=artifacts\ui\check.png
   ```
4. Optional (local/CI when ImageMagick installed): `.\tools\VisualCompare.ps1` — SSIM compare against `tests/visual/baseline/kbmixer.png`.

## Visual / UX rubric

| # | Criterion | Pass |
|---|-----------|------|
| 1 | **Header** is honest app branding: primary title **KBMixer** (no fake Settings breadcrumb path). | [ ] |
| 2 | **Subtitle** (if present) is a short, accurate line — not impersonating Windows Settings. | [ ] |
| 3 | **System** section: one **Volume** row; device name on second line for master. | [ ] |
| 4 | **Output device** appears **below** System volume, full-width combo inside the same card. | [ ] |
| 5 | **Apps** rows: icon, name, small volume glyph, numeric level, **thin** horizontal slider, **chevron** (no permanent “Set target” on the collapsed row). | [ ] |
| 6 | **Expanded row** shows KBMixer-only **“Set as profile target”** (hidden when `App` is null). | [ ] |
| 7 | **Sliders**: track ~**2px**, thumb ~**10–12px** (`UiSliderHorizontal` in `Themes/FluentSettingsDark.xaml`). | [ ] |
| 8 | **Profile & hotkeys** card uses the same **card style** (`UiCard`) and section spacing as the mixer card. | [ ] |
| 9 | **Contrast**: primary text readable on `UiBrush.Card` / `UiBrush.Window`; focus states visible on buttons and combo. | [ ] |
|10 | **Mica / backdrop**: applied on Win11 when supported (`MicaHelper`); app still runs on older Windows if DWM call fails. | [ ] |

## Iteration protocol (don’t stop early)

1. Implement or adjust UI against this doc.
2. Run **Automated gates** above.
3. Walk the **rubric** top to bottom; any failure → fix → repeat from step 2.
4. If updating the shared baseline PNG, run the command in `tests/visual/baseline/README.md` and **visually diff** before commit.
5. Merge only when rubric is all checked **or** waiver is recorded in the PR description.

## Scope note

KBMixer is **not** hosted inside the Windows Settings process; full Mica tabbed shell + left navigation is out of scope for the WPF app. This rubric targets **Volume mixer page parity** for layout, controls, and density—not a pixel-perfect clone of the entire Settings app.
