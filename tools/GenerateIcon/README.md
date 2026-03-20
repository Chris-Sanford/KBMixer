# GenerateIcon

Regenerates `KBMixer/KBMixer.ico` from `images/KBMixerIconClean.svg` (multi-size PNG-in-ICO).

From the repo root:

```powershell
dotnet run --project tools/GenerateIcon/GenerateIcon.csproj -c Release
```

Requires the **nuget.org** package source (SkiaSharp, Svg.Skia).
