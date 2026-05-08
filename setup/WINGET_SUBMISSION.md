# WinGet Package Submission Guide

This document describes how to prepare and submit WinGet manifests for LightCrosshair 1.5.0.

Do not submit a PR to `microsoft/winget-pkgs`, push a branch, or publish any artifact until the 1.5.0 release URL and SHA256 are final and explicit release approval has been given.

## Prerequisites

- GitHub account.
- Fork of `microsoft/winget-pkgs`.
- Git CLI.
- WinGet CLI for local validation, or access to a manifest validator.
- Final 1.5.0 release artifact URL: `https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.5.0/LightCrosshair-Setup-1.5.0.exe`.
- Final 1.5.0 artifact SHA256: `e79186a1dffdd2223bf694a2c3c6b7c21a7f61d4ab6c47d695f3a9e15db26d21`.

Do not store GitHub tokens or other credentials in this repository.

## Local Manifest Preparation

The repository currently contains historical 1.3.0 manifests under:

```text
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0/
```

For 1.5.0, create a new local manifest directory during release finalization:

```powershell
Copy-Item `
  -Recurse `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0 `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.5.0
```

Then update the copied manifests:

- `PackageVersion: 1.5.0`
- `InstallerUrl: https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.5.0/LightCrosshair-Setup-1.5.0.exe`
- `InstallerSha256: e79186a1dffdd2223bf694a2c3c6b7c21a7f61d4ab6c47d695f3a9e15db26d21`
- `ReleaseNotesUrl: https://github.com/PrimeBuild-pc/LightCrosshair/releases/tag/v1.5.0`
- `ReleaseDate`, if present, to the actual release date.

## Validate Locally

Preferred:

```powershell
winget validate --manifest .\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.5.0\
```

If WinGet CLI validation is unavailable, validate YAML syntax and schema with the current Microsoft WinGet manifest validation workflow before opening a PR.

## Prepare Your WinGet Fork

After explicit release approval:

```bash
git clone https://github.com/YOUR_USERNAME/winget-pkgs.git
cd winget-pkgs
git remote add upstream https://github.com/microsoft/winget-pkgs.git
git fetch upstream
git checkout master
git merge upstream/master
```

If your fork uses `main` instead of `master`, use that branch name consistently.

## Copy Manifests To The Fork

Copy the validated 1.5.0 directory into your fork:

```powershell
Copy-Item `
  -Recurse `
  C:\path\to\LightCrosshair\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.5.0 `
  C:\path\to\winget-pkgs\manifests\p\PrimeBuild\LightCrosshair\1.5.0
```

Expected files:

```text
manifests/p/PrimeBuild/LightCrosshair/1.5.0/
  PrimeBuild.LightCrosshair.yaml
  PrimeBuild.LightCrosshair.installer.yaml
  PrimeBuild.LightCrosshair.locale.en-US.yaml
```

## Commit And PR

Only after explicit release approval:

```bash
git checkout -b add/lightcrosshair-1.5.0
git add manifests/p/PrimeBuild/LightCrosshair/1.5.0/
git commit -m "Add LightCrosshair v1.5.0"
git push origin add/lightcrosshair-1.5.0
```

Open a PR against `microsoft/winget-pkgs`.

PR checklist:

- Manifest validates locally.
- Installer URL is public and points to the final 1.5.0 artifact.
- SHA256 matches the final artifact.
- PackageVersion is `1.5.0` in all manifest files.
- Release notes URL points to `v1.5.0`.
- No credentials, tokens, or private URLs are included.

## Troubleshooting

Checksum mismatch:

```powershell
(Get-FileHash -Path "C:\path\to\artifact" -Algorithm SHA256).Hash
```

Manifest validation fails:

- Check indentation: YAML uses spaces, not tabs.
- Confirm all three manifest files use the same `PackageIdentifier` and `PackageVersion`.
- Confirm `ManifestType` matches each file.
- Confirm URLs are publicly accessible.

## References

- WinGet documentation: https://learn.microsoft.com/windows/package-manager/
- microsoft/winget-pkgs: https://github.com/microsoft/winget-pkgs
- WinGet manifest schemas: https://github.com/microsoft/winget-cli/tree/master/schemas
