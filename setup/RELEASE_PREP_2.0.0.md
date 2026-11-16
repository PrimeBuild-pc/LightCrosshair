# Release 2.0.0 preparation

Merge Core and UI before packaging. Use .NET 10 SDK and Desktop Runtime. Both executables must come from the same contract version and stay in the same folder.

`pwsh scripts/build-release.ps1` creates win-x64 and win-arm64 framework-dependent, ReadyToRun, single-file executables, ZIPs and SHA256 files. The development `-CoreOnly` switch produces an explicitly incomplete package; never distribute it as a release.

`pwsh scripts/validate-release-preflight.ps1` validates both workstreams after merge. Compile the installer with Inno Setup using `setup/LightCrosshair.iss`; supply `/DAppVersion=2.0.0` and `/DSourceDir=<absolute x64 publish directory>` when overriding the defaults. The current installer targets x64; the ARM64 portable ZIP is separate.

Test install, v1 upgrade, uninstall/reinstall, Start Menu and autorun on clean Windows. Verify GPU color recovery after forced termination and test an ARM64 machine. Verify Authenticode signatures (or explicitly document unsigned artifacts), final hashes and tag before publication.

Chocolatey uses `dotnet-10.0-desktopruntime`; replace its checksum placeholder with the final installer hash. WinGet's new template declares `Microsoft.DotNet.DesktopRuntime.10`; do not update an old runtime-8 manifest without replacing that dependency. Historical v1 manifests remain unchanged. Publication is a separate release step.

Publishing properties follow [Microsoft single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview). Runtime dependency: [Chocolatey .NET 10 Desktop Runtime](https://community.chocolatey.org/packages/dotnet-10.0-desktopruntime).
