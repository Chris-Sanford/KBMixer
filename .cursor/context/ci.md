## GitHub Actions

- Workflow: `.github/workflows/build-and-release.yml`
- Triggers: push to `main`, `workflow_dispatch`
- Runner: `windows-latest`, .NET 8 SDK
- Publishes `KBMixer/KBMixer.csproj` Release output (csproj already pins self-contained win-x64 single-file), zips publish folder → `KBMixer-win-x64.zip`
- Creates a GitHub Release; tag `v{Version}-r{run_number}` where `Version` is MSBuild-evaluated from the csproj
- Needs repo **Actions → Workflow permissions → Read and write** for `GITHUB_TOKEN` to publish releases
