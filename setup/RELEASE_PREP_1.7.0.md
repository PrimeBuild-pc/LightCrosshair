# LightCrosshair 1.7.0 Release Preparation

This document tracks the local test/release build scope for LightCrosshair 1.7.0 after the NVIDIA profile controls work.

## Scope

Prepared distribution channels:

- Portable ZIP package.
- Inno Setup installer.

Excluded from this release-prep pass:

- Official GitHub release creation.
- Git tags.
- Pushes to any remote.
- MSIX packaging.
- New services, updaters, installers, or background tasks.
- Package-manager publication/submission without explicit approval.

## Version Baseline

- AssemblyVersion: `1.7.0.0`
- FileVersion: `1.7.0.0`
- Package Version: `1.7.0`
- Inno Setup AppVersion: `1.7.0`
- Release script default version: `1.7.0`

## 1.7.0 Product Notes

- Fixed NVIDIA FPS cap profile binding so per-app cap controls target the selected application profile.
- Added NVIDIA profile audit information for safer inspection before applying supported per-app settings.
- Added NVIDIA Low Latency Off/On per-app controls.
- Added NVIDIA VSync per-app controls.
- Added read-only NVIDIA G-SYNC and Low Latency CPL State display.
- Isolated NVIDIA profile operations in a helper process so helper crashes/timeouts become UI errors instead of main-process crashes.
- Polished the GPU Driver tab so NVIDIA profile status/details wrap below each setting and command controls remain visible at narrower window widths.
- Clarified that Display Management saturation/brightness controls are separate from NVIDIA Digital Vibrance driver color control.
- Initialized Frame Cap Assistant recommendations from the primary display refresh rate when safe refresh-rate data is available.
- Safety constraints remain in place: no global profile writes, no raw setting editor, no DLSS writes, no G-SYNC writes, and `app.manifest` remains `asInvoker`.

## Build Checks

Run from the repository root:

```powershell
dotnet build .\LightCrosshair.sln
dotnet test .\LightCrosshair.sln
```

## Portable Package

Recommended local release-prep command:

```powershell
.\scripts\build-release.ps1 -Version 1.7.0
```

Expected package names:

- `releases/LightCrosshair-v1.7.0-x64.zip`
- `releases/LightCrosshair-v1.7.0-ARM64.zip`

The release script currently produces framework-dependent single-file output by default. These ZIPs require the .NET 8 Windows Desktop Runtime unless `-SelfContained` is explicitly used and separately validated.

## Inno Setup

The release script builds the Inno Setup installer automatically when Inno Setup 6 is installed at the default path.

Expected installer base filename:

- `LightCrosshair-Setup-1.7.0`

Do not publish the installer until the release step is explicitly approved.

## Package Manager Prep

Do not submit 1.7.0 Chocolatey or WinGet package metadata with guessed public URLs or hashes. Final public GitHub release assets now exist, so the repository has prepared package-manager metadata using the verified installer URL and SHA256.

Use `setup/PACKAGE_MANAGER_RELEASE_CHECKLIST_1.7.0.md` for the final Chocolatey and WinGet fields, validation commands, and explicit submit/publish gates.

## Manual NVIDIA Validation

Manual NVIDIA helper/process-isolation validation has been completed externally for the release-blocker scenario. Before public publication, repeat a final smoke check on a machine with supported NVIDIA hardware and drivers:

- Profile audit reads the intended app profile.
- FPS cap writes bind only to the selected application profile.
- Low Latency Off/On writes affect only the selected application profile.
- VSync writes affect only the selected application profile.
- G-SYNC and Low Latency CPL State remain read-only.
- No global profile writes are produced.
- Main UI remains open if the helper reports an error, timeout, or nonzero exit.
