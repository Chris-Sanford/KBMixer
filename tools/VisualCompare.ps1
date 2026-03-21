#Requires -Version 5.1
<#
.SYNOPSIS
  Runs KBMixer in --ui-golden mode and optionally compares to baseline (ImageMagick SSIM).

.PARAMETER Baseline
  Path to baseline PNG (repo: tests/visual/baseline/kbmixer.png).

.PARAMETER ActualDir
  Directory for actual capture (default: artifacts/ui under repo root).

.PARAMETER SkipCompare
  If set, only produces actual.png (no ImageMagick).

.EXAMPLE
  .\tools\VisualCompare.ps1
  .\tools\VisualCompare.ps1 -SkipCompare
#>
param(
    [string] $Baseline = "",
    [string] $ActualDir = "",
    [switch] $SkipCompare
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $Baseline) { $Baseline = Join-Path $repoRoot "tests\visual\baseline\kbmixer.png" }
if (-not $ActualDir) { $ActualDir = Join-Path $repoRoot "artifacts\ui" }

New-Item -ItemType Directory -Force -Path $ActualDir | Out-Null
$actual = Join-Path $ActualDir "actual.png"

Push-Location $repoRoot
try {
    dotnet build (Join-Path $repoRoot "KBMixer\KBMixer.csproj") -c Release --verbosity minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

    dotnet run --project (Join-Path $repoRoot "KBMixer\KBMixer.csproj") -c Release --no-build -- --ui-golden="$actual"
    if ($LASTEXITCODE -ne 0) { throw "dotnet run --ui-golden failed" }
    if (-not (Test-Path $actual)) { throw "Expected output missing: $actual" }
    Write-Host "Wrote: $actual"

    if ($SkipCompare) { return }

    if (-not (Test-Path $Baseline)) {
        Write-Warning "Baseline missing: $Baseline — copy actual.png to baseline after visual review."
        return
    }

    $magick = Get-Command magick -ErrorAction SilentlyContinue
    if (-not $magick) {
        Write-Warning "ImageMagick (magick) not on PATH — skipping SSIM. Install: https://imagemagick.org"
        return
    }

    $diff = Join-Path $ActualDir "diff.png"
    $args = @("compare", "-metric", "SSIM", $Baseline, $actual, $diff)
    & magick @args 2>&1 | ForEach-Object { Write-Host $_ }
    # magick returns SSIM on stderr; exit 0 if identical enough
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Compare reported differences; see $diff"
        exit 1
    }
    Write-Host "SSIM compare completed (see ImageMagick output above)."
}
finally {
    Pop-Location
}
