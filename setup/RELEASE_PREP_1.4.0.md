# LightCrosshair 1.4.0 Release Preparation

This document tracks the safe packaging baseline for LightCrosshair 1.4.0.

## Scope

Prepared distribution channels:

- Portable ZIP package.
- Inno Setup installer.
- Chocolatey package.
- WinGet manifest submission.
- PowerShell install script, pending final artifact URL and SHA256 checksum.

These channels are not published yet. Chocolatey, WinGet, GitHub Releases, hosted install-script usage, and public installer distribution all require explicit release approval.

Excluded packaging:

- MSIX.

Out of scope until explicit release approval:

- Official GitHub release creation.
- Git tags.
- Pushes to any remote.
- Chocolatey push.
- WinGet PR submission.
- Hosting or advertising the PowerShell install script for 1.4.0.

## Version Baseline

- AssemblyVersion: `1.4.0.0`
- FileVersion: `1.4.0.0`
- Package Version: `1.4.0`
- Inno Setup AppVersion: `1.4.0`
- Chocolatey package version: `1.4.0`
- PowerShell install script default version: `1.4.0`

## Secrets

No real API keys, tokens, passwords, or signing secrets should be committed.

Use placeholders in docs and environment variables locally, for example:

```powershell
$env:CHOCOLATEY_API_KEY = '<CHOCOLATEY_API_KEY>'
choco apikey --key $env:CHOCOLATEY_API_KEY --source https://push.chocolatey.org/
```

## Build Checks

Run from the repository root:

```powershell
dotnet restore
dotnet build LightCrosshair.sln
dotnet test LightCrosshair.sln
```

## Portable ZIP

Recommended command:

```powershell
.\scripts\build-release.ps1 -Version 1.4.0
```

Expected package names:

- `LightCrosshair-v1.4.0-x64.zip`
- `LightCrosshair-v1.4.0-ARM64.zip`

Current release scripts default to framework-dependent output. These ZIPs require the .NET 8 Windows Desktop Runtime unless `-SelfContained` is explicitly used and separately validated.

## Inno Setup

Publish `win-x64` output into the path expected by `setup/LightCrosshair.iss`:

```powershell
dotnet publish LightCrosshair/LightCrosshair.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output setup/publish/win-x64 `
  /p:PublishSingleFile=true `
  /p:PublishReadyToRun=true `
  /p:PublishTrimmed=false
```

Then compile `setup/LightCrosshair.iss` with Inno Setup.

Expected installer base filename:

- `LightCrosshair-Setup-1.4.0`

## Chocolatey

Prepare locally only:

```powershell
Push-Location setup/chocolatey
choco pack LightCrosshair.nuspec
Pop-Location
```

The current package metadata is a release candidate only. If the package remains framework-dependent, it must depend on the .NET 8 Windows Desktop Runtime package and must not be pushed until the final executable, checksum, and dependency behavior are validated.

Authenticate and push only after explicit release approval:

```powershell
choco apikey --key $env:CHOCOLATEY_API_KEY --source https://push.chocolatey.org/
choco push LightCrosshair.1.4.0.nupkg --source https://push.chocolatey.org/
```

## WinGet

During release finalization, create a new 1.4.0 manifest directory from the historical 1.3.0 manifests:

```powershell
Copy-Item `
  -Recurse `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0 `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.4.0
```

Then update `PackageVersion`, `InstallerUrl`, `InstallerSha256`, release notes URL, and validate locally before opening a PR.

## PowerShell Install Script

The script `scripts/install.ps1` is prepared for future hosted use. Do not advertise a live pipeline install command until the final 1.4.0 release artifact and SHA256 checksum are available.

Before a real release, update the script checksum map or invoke it with:

```powershell
& ([scriptblock]::Create((irm https://example.com/install.ps1))) -Version 1.4.0 -Checksum '<SHA256>'
```
