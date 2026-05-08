# LightCrosshair 1.5.0 Release Preparation

This document tracks the safe packaging baseline for LightCrosshair 1.5.0.

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
- Hosting or advertising the PowerShell install script for 1.5.0.

## Version Baseline

- AssemblyVersion: `1.5.0.0`
- FileVersion: `1.5.0.0`
- Package Version: `1.5.0`
- Inno Setup AppVersion: `1.5.0`
- Chocolatey package version: `1.5.0`
- PowerShell install script default version: `1.5.0`

## 1.5.0 Product Notes

- Lightweight performance overlay modes: Off, Minimal, and Detailed remain product-focused instead of becoming a telemetry dashboard.
- Ultra-lightweight mode reduces overlay detail/update cost for low-overhead sessions.
- Crosshair visibility presets provide quick high-contrast crosshair colors without modifying game rendering.
- Frame Cap Assistant can suggest target FPS values, but it is assistant-only and has no active real limiter backend.
- PresentMon remains a research/validation tool only; LightCrosshair 1.5.0 does not include a PresentMon runtime provider.

## Readiness Matrix

| Area | 1.5.0 status | Required before public release |
| --- | --- | --- |
| Build | Done locally when `dotnet build LightCrosshair.sln` passes. | Re-run on the final release candidate commit. |
| Tests | Done locally when `dotnet test LightCrosshair.sln` passes. | Re-run on the final release candidate commit. |
| Release preflight | Prepared through `scripts/validate-release-preflight.ps1`. | Must pass with no failures before commit/release approval. |
| Manual QA | Prepared through `setup/QA_CHECKLIST_1.5.0.md`. | Complete the checklist against final local artifacts. |
| Portable ZIP | Dry-run/local artifact only. | Generate final ZIPs, inspect contents, record SHA256, then get release approval. |
| Inno installer | Prepared for local compile only. | Compile with Inno Setup, smoke test install/uninstall, record SHA256, then get release approval. |
| Chocolatey | Metadata prepared only; not published. | Validate local pack, confirm final artifact/checksum/dependency behavior, then get explicit approval before push. |
| WinGet | Submission docs prepared only; no 1.5.0 manifest should be submitted yet. | Create final manifests only after public final artifact URL and SHA256 exist; validate locally before any PR. |
| PowerShell install script | Prepared for future hosted use only. | Do not advertise hosted usage until final artifact URL and SHA256 exist. |
| Docs | Conservative release wording prepared. | Recheck no live package-manager/install commands are advertised before publication. |
| Special K attribution | Conservative notes and mapping prepared. | Confirm no GPL code or substantial logic was copied. |
| Frame pacing diagnostics | Diagnostics-only, read-only telemetry. | Manual validation on stable and jittery workloads. |
| Frame-generation estimate | Heuristic estimate/suspicion only unless a verified provider signal exists. | Keep `Detected`/verified wording gated to verified provider signals. |
| Limiter scaffold | No-op/unavailable backend model only. | Do not claim active limiting until a real backend exists and telemetry validation proves it. |
| Native backend | Blocked/out of scope for 1.5.0. | Requires separate design, licensing, signing, anti-cheat review, and explicit approval. |

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
.\scripts\validate-release-preflight.ps1
```

Use the normal preflight command on feature branches and release-candidate branches. After the PR is merged and final release artifact validation is intentionally running from `main`, use:

```powershell
.\scripts\validate-release-preflight.ps1 -AllowMain
```

`-AllowMain` is only a branch-gate override for final release validation. The script remains non-publishing and must not tag, upload, submit, or publish artifacts.

## Portable ZIP

Recommended command:

```powershell
.\scripts\build-release.ps1 -Version 1.5.0
```

Expected package names:

- `LightCrosshair-v1.5.0-x64.zip`
- `LightCrosshair-v1.5.0-ARM64.zip`

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

- `LightCrosshair-Setup-1.5.0`

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
choco push LightCrosshair.1.5.0.nupkg --source https://push.chocolatey.org/
```

## WinGet

During release finalization, create a new 1.5.0 manifest directory from the historical 1.3.0 manifests:

```powershell
Copy-Item `
  -Recurse `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0 `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.5.0
```

Then update `PackageVersion`, `InstallerUrl`, `InstallerSha256`, release notes URL, and validate locally before opening a PR.

## PowerShell Install Script

The script `scripts/install.ps1` is prepared for future hosted use. Do not advertise a live pipeline install command until the final 1.5.0 release artifact and SHA256 checksum are available.

Before a real release, update the script checksum map or invoke it with:

```powershell
& ([scriptblock]::Create((irm https://example.com/install.ps1))) -Version 1.5.0 -Checksum '<SHA256>'
```
