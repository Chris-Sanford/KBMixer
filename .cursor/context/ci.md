## GitHub Actions

- Workflow: `.github/workflows/build-and-release.yml`
- Triggers: push to `main`, `workflow_dispatch`
- Runner: `windows-latest`, .NET 8 SDK
- Publishes `KBMixer/KBMixer.csproj` Release output (self-contained win-x64 single-file); release asset is `artifacts/publish/KBMixer.exe`
- Creates a GitHub Release; tag `v{Version}-r{run_number}` where `Version` comes from `KBMixer.csproj` (bump `<Version>` there when you want that to change; `-r{run_number}` keeps every push’s tag unique)
- Needs repo **Actions → Workflow permissions → Read and write** for `GITHUB_TOKEN` to publish releases
