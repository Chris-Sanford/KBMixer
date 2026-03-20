# KBMixer
`KBMixer` allows you to adjust the volume of individual applications on your Windows device using specified global keyboard hotkeys and your mouse scroll wheel without needing to focus a specific volume mixer application.


## Development

KBMixer targets **.NET 8** (`net8.0-windows`) and **Windows Forms**. You need **Windows** and tooling that includes the Windows desktop workload.

### Prerequisites

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and, for a full IDE experience, [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) with the **.NET desktop development** workload.

**Install with [winget](https://learn.microsoft.com/en-us/windows/package-manager/winget/)** (use an elevated PowerShell if a package asks for administrator approval):

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e --source winget
winget install --id Microsoft.VisualStudio.Community -e --source winget
```

After Visual Studio finishes, open **Visual Studio Installer** → **Modify** on your Community install and enable the **.NET desktop development** workload if the installer did not already select it. That workload covers WinForms and `net8.0-windows` targeting.

### Build and run

From the repository root (where `KBMixer.sln` lives):

```powershell
dotnet restore KBMixer.sln
dotnet build KBMixer.sln
dotnet run --project KBMixer\KBMixer.csproj
```
