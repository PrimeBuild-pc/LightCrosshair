# WinGet Package Submission Guide

This document describes how to prepare and submit WinGet manifests for LightCrosshair 1.7.0.

Do not submit a PR to `microsoft/winget-pkgs`, push a branch, or publish any artifact until the final 1.7.0 public installer URL and SHA256 are available and explicit release approval has been given.

## Prerequisites

- GitHub account.
- Fork of `microsoft/winget-pkgs`.
- Git CLI.
- WinGet CLI for local validation, or access to a manifest validator.
- Final public 1.7.0 installer asset URL from the GitHub Release.
- Final 1.7.0 installer SHA256 calculated from that public asset.

Do not store GitHub tokens or other credentials in this repository.

## Local Manifest Preparation

The repository currently contains historical manifests under:

```text
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.3.0/
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.5.0/
setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.6.0/
```

For 1.7.0, do not create a manifest directory with placeholder values. After the final public installer URL and SHA256 exist, copy the most recent manifest directory:

```powershell
Copy-Item `
  -Recurse `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.6.0 `
  setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.7.0
```

Then update the copied manifests:

- `PackageVersion: 1.7.0`
- `InstallerUrl` to the final public `LightCrosshair-Setup-1.7.0.exe` release asset URL.
- `InstallerSha256` to the final installer SHA256.
- `ReleaseNotesUrl` to the final `v1.7.0` release page.
- `ReleaseDate`, if present, to the actual release date.

## Validate Locally

Preferred:

```powershell
winget validate --manifest .\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.7.0\
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

Copy the validated 1.7.0 directory into your fork:

```powershell
Copy-Item `
  -Recurse `
  C:\path\to\LightCrosshair\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.7.0 `
  C:\path\to\winget-pkgs\manifests\p\PrimeBuild\LightCrosshair\1.7.0
```

Expected files:

```text
manifests/p/PrimeBuild/LightCrosshair/1.7.0/
  PrimeBuild.LightCrosshair.yaml
  PrimeBuild.LightCrosshair.installer.yaml
  PrimeBuild.LightCrosshair.locale.en-US.yaml
```

## Commit And PR

Only after explicit release approval:

```bash
git checkout -b add/lightcrosshair-1.7.0
git add manifests/p/PrimeBuild/LightCrosshair/1.7.0/
git commit -m "Add LightCrosshair v1.7.0"
git push origin add/lightcrosshair-1.7.0
```

Open a PR against `microsoft/winget-pkgs`.

PR checklist:

- Manifest validates locally.
- Installer URL is public and points to the final 1.7.0 artifact.
- SHA256 matches the final artifact.
- PackageVersion is `1.7.0` in all manifest files.
- Release notes URL points to `v1.7.0`.
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
