# 🎯 LightCrosshair

> **A lightweight crosshair overlay for competitive gaming on Windows**

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Codecov](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair/branch/main/graph/badge.svg)](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/badge/Release-Latest-brightgreen)](../../releases)

LightCrosshair is a lightweight, gamer-focused crosshair overlay for Windows 10/11. It keeps the core experience simple: a visible, customizable crosshair, profile-based settings, and optional low-overhead performance information for borderless/windowed games.

[![Watch the demo video](https://img.youtube.com/vi/CKbj2eObQ1E/maxresdefault.jpg)](https://www.youtube.com/watch?v=CKbj2eObQ1E)

**⬆️ Watch demo video on YouTube**

**v1.5.0 status:** GitHub Release downloads are live. WinGet is pending maintainer review. Chocolatey package metadata is locally validated, but publication is currently blocked by a Chocolatey account/API permission issue.

## Documentation

- [Settings & Options Guide](docs/SETTINGS.md) — Complete documentation for every setting, hotkey, profile, and performance overlay option.

---

## Highlights

- **Crosshair-first overlay:** customizable shape, size, thickness, gap, colors, opacity, outline, and profile workflow.
- **Visibility presets:** quick high-contrast crosshair presets for better visibility without changing game rendering.
- **Performance overlay modes:** Off, Minimal, and Detailed modes, plus ultra-lightweight behavior for lower-overhead sessions.
- **GPU Driver Integration:** Direct NVIDIA driver integration for FPS cap (via DRS) and digital vibrance. AMD color management via ADL2. Capability matrix shown in settings.
- **Frame Cap Assistant:** target-FPS guidance only. It does not enforce a real frame limit and has no active limiter backend.
- **Non-injected FPS telemetry:** optional ETW-style present telemetry with optional RTSS fallback caveats when detailed metrics are unavailable.
- **Borderless/windowed oriented:** normal overlays are expected to work best in borderless windowed or windowed games.
- **Anti-cheat-conscious design:** LightCrosshair avoids game hooks, injection, and native runtime backends.

---

## Installation

### GitHub Release Downloads

Download v1.5.0 from the [GitHub Releases page](../../releases/tag/v1.5.0).

Available assets:

- **Installer:** `LightCrosshair-Setup-1.5.0.exe`
- **Portable ZIP x64:** `LightCrosshair-v1.5.0-x64.zip`
- **Portable ZIP ARM64:** `LightCrosshair-v1.5.0-ARM64.zip`

Recommended path for most users: download the installer, run it, then launch LightCrosshair from the Start Menu.

Portable ZIP flow:

1. Download the ZIP matching your CPU architecture.
2. Extract it to a folder you control.
3. Run `LightCrosshair.exe`.
4. Open Settings with `Alt + L` or by left-clicking the tray icon.

### Package Managers

- **WinGet:** manifest PR is open and checks pass, but maintainer review is still required. Do not use a `winget install` command until the manifest is merged and available from the public WinGet source.
- **Chocolatey:** package files are prepared and locally validated. Public publication is blocked by a Chocolatey `403` account/API/permission issue, so no live Chocolatey install command is advertised yet.
- **PowerShell install script:** planned only. A live `irm`/`iwr` command is not advertised until the hosted script is public, versioned for v1.5.0, downloads the correct artifact, and verifies the final SHA256.

---

## Requirements

- Windows 10 or Windows 11.
- .NET 8 Windows Desktop Runtime for the current framework-dependent packages.
- Borderless windowed or windowed game mode is recommended for overlay visibility.

---

## Usage

1. Launch LightCrosshair.
2. Open Settings with `Alt + L` or the tray icon.
3. Customize the crosshair shape, size, colors, opacity, outline, and visibility preset.
4. Save profiles for different games or visibility needs.
5. Enable the performance overlay only if you want FPS/frametime information.

Default hotkeys:

- `Alt + X` toggles crosshair visibility.
- `Alt + C` cycles to the next profile.
- `Alt + V` cycles to the previous profile.
- `Alt + L` toggles the Settings window.

---

## Limitations And Safety

- LightCrosshair does not inject into games and does not install hook/native backends.
- Exclusive fullscreen can hide normal overlays. Use borderless windowed or windowed mode when possible.
- Frame Cap Assistant is assistant-only; it does not apply or enforce a real FPS cap.
- Frame-generation information is conservative. Timing/FPS differences are treated as estimates or suspicion unless explicit provider evidence is available.
- PresentMon is not a LightCrosshair runtime provider in v1.5.0.
- Some games and anti-cheat systems may block overlays or behave differently. No overlay can guarantee universal compatibility.

---

## Build From Source

```bash
git clone https://github.com/PrimeBuild-pc/LightCrosshair.git
cd LightCrosshair
dotnet restore
dotnet build LightCrosshair.sln --configuration Release
dotnet test LightCrosshair.sln
```

Publish framework-dependent output:

```bash
dotnet publish LightCrosshair/LightCrosshair.csproj --configuration Release --runtime win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false
```

---

## Architecture

- **Framework:** .NET 8.0 Windows desktop app.
- **Graphics:** SkiaSharp primary renderer with automatic GDI fallback.
- **Settings:** profile-based configuration saved under the user profile.
- **Performance:** cached rendering and config-driven redraws to keep idle overhead low.

---

## Contributing

Bug reports, focused feature requests, documentation improvements, and tested pull requests are welcome.

Development prerequisites:

- Visual Studio 2022 or VS Code.
- .NET 8 SDK.
- Windows 10/11 for desktop app testing.

Before opening a pull request, run:

```bash
dotnet build LightCrosshair.sln
dotnet test LightCrosshair.sln
```

---

## Third-Party Notices

This application uses the following third-party libraries:
- **NvAPIWrapper.Net** (LGPL-3.0) — NVIDIA GPU driver integration. See [docs/THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md) for details.
- **AMD ADLX SDK** (Proprietary AMD License) — AMD GPU detection. See [docs/THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md) for details.

---

## License

This project is licensed under the **MIT License**. See [LICENSE](LICENSE).

---

## Support

- **Issues:** [GitHub Issues](../../issues)
- **Discussions:** [GitHub Discussions](../../discussions)
- **Releases:** [GitHub Releases](../../releases)

---

<div align="center">

**Made with ❤️ for the gaming community**

[![PayPal](https://img.shields.io/badge/Supporta%20su-PayPal-blue?logo=paypal)](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

[⭐ Star this repo](../../stargazers) • [🐛 Report Bug](../../issues) • [💡 Request Feature](../../issues) • [🤝 Contribute](../../pulls)

</div>
