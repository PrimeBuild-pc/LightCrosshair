# WinGet Package Submission Guide

This document describes how to prepare and submit WinGet manifests for LightCrosshair 1.6.0.

Do not submit a PR to `microsoft/winget-pkgs`, push a branch, or publish any artifact until the 1.6.0 release URL and SHA256 are final and explicit release approval has been given.

## Prerequisites

- GitHub account.
- Fork of `microsoft/winget-pkgs`.
- Git CLI.
- WinGet CLI for local validation, or access to a manifest validator.
- Final 1.6.0 release artifact URL: `https://github.com/PrimeBuild/LightCrosshair/releases/download/v1.6.0/LightCrosshair-Setup-1.6.0.exe`.
- Final 1.6.0 artifact SHA256: `414E50D3A6E24F107A48CF7A35E6C7E06A9CA4E2C3527912CC79C2BBF723EDD8`.

Do not store GitHub tokens or other credentials in this repository.

## Local Manifest Preparation

The repository currently contains historical manifests under:

```text
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0/
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.5.0/
```

For 1.6.0, the manifest directory already exists at:

```text
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.6.0/
```

If creating from a previous version, copy the prior manifest directory and update the fields:

```powershell
Copy-Item `
  -Recurse `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.5.0 `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.6.0
```

Then update the copied manifests:

- `PackageVersion: 1.6.0`
- `InstallerUrl: https://github.com/PrimeBuild/LightCrosshair/releases/download/v1.6.0/LightCrosshair-Setup-1.6.0.exe`
- `InstallerSha256: 414E50D3A6E24F107A48CF7A35E6C7E06A9CA4E2C3527912CC79C2BBF723EDD8`
- `ReleaseNotesUrl: https://github.com/PrimeBuild/LightCrosshair/releases/tag/v1.6.0`
- `ReleaseDate`, if present, to the actual release date.

## Validate Locally

Preferred:

```powershell
winget validate --manifest .\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.6.0\
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

Copy the validated 1.6.0 directory into your fork:

```powershell
Copy-Item `
  -Recurse `
  C:\path\to\LightCrosshair\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.6.0 `
  C:\path\to\winget-pkgs\manifests\p\PrimeBuild\LightCrosshair\1.6.0
```

Expected files:

```text
manifests/p/PrimeBuild/LightCrosshair/1.6.0/
  PrimeBuild.LightCrosshair.yaml
  PrimeBuild.LightCrosshair.installer.yaml
  PrimeBuild.LightCrosshair.locale.en-US.yaml
```

## Commit And PR

Only after explicit release approval:

```bash
git checkout -b add/lightcrosshair-1.6.0
git add manifests/p/PrimeBuild/LightCrosshair/1.6.0/
git commit -m "Add LightCrosshair v1.6.0"
git push origin add/lightcrosshair-1.6.0
```

Open a PR against `microsoft/winget-pkgs`.

PR checklist:

- Manifest validates locally.
- Installer URL is public and points to the final 1.6.0 artifact.
- SHA256 matches the final artifact.
- PackageVersion is `1.6.0` in all manifest files.
- Release notes URL points to `v1.6.0`.
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
