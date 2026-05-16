# LightCrosshair 1.7.0 Package Manager Release Checklist

This checklist is intentionally not a publishable package manifest. Fill the fields below only after the final GitHub release assets are uploaded and their public URLs and SHA256 hashes are known.

## Inputs Required After Artifact Upload

- Final installer asset URL.
- Final installer SHA256.
- Final release notes URL.
- Actual release date.
- Explicit approval to submit package-manager updates.

## Chocolatey

Update these files only after the final public installer URL and SHA256 exist:

- `setup/chocolatey/LightCrosshair.nuspec`
  - Set `<version>` to `1.7.0`.
  - Set `<projectUrl>` to the final `v1.7.0` GitHub release page.
  - Update the installer description from `1.6.0` to `1.7.0`.
  - Update release notes to summarize the NVIDIA profile controls, helper isolation, GPU Driver UI polish, and Frame Cap Assistant default recommendation.
- `setup/chocolatey/tools/chocolateyinstall.ps1`
  - Set `$installerUrl` to the final public `LightCrosshair-Setup-1.7.0.exe` URL.
  - Set `$checksum` to the final installer SHA256.
- `setup/chocolatey/tools/VERIFICATION.txt`
  - Update the installer URL.
  - Update the SHA256.
  - Note how the checksum was calculated from the final public release asset.

Validation before submission:

```powershell
choco pack .\setup\chocolatey\LightCrosshair.nuspec --out .\setup\chocolatey
```

Then install and uninstall the generated `.nupkg` from the local `setup/chocolatey` source on a test machine. Do not install from or push to the public Chocolatey source until explicit release approval is given.

Do not run `choco push` until explicit release approval is given.

## WinGet

Do not create a 1.7.0 WinGet manifest directory with placeholder values. After the final public installer URL and SHA256 exist:

1. Copy the most recent manifest directory to `setup/winget/manifests/p/PrimeBuild/LightCrosshair/1.7.0/`.
2. Set `PackageVersion` to `1.7.0` in all copied manifests.
3. Set the installer URL to the final public `LightCrosshair-Setup-1.7.0.exe` asset.
4. Set `InstallerSha256` to the final installer SHA256.
5. Set `ReleaseNotesUrl` to the final `v1.7.0` GitHub release page.
6. Set the release date, if present, to the actual release date.
7. Validate locally:

```powershell
winget validate --manifest .\setup\winget\manifests\p\PrimeBuild\LightCrosshair\1.7.0\
```

Do not open a `microsoft/winget-pkgs` PR until explicit release approval is given.

## Install Script

`scripts/install.ps1` is a prepared template, not a published install channel for 1.7.0. If it is revived for this release, update its asset URL model and checksum handling against the final public artifacts before advertising it.
