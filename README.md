<div align="center">

# 🎯 LightCrosshair

**A lightweight, customizable crosshair overlay for competitive gaming on Windows.**

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Latest Release](https://img.shields.io/github/v/release/PrimeBuild-pc/LightCrosshair?label=release&logo=github)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/PrimeBuild-pc/LightCrosshair/total?label=downloads&logo=github)](../../releases)
[![Issues](https://img.shields.io/github/issues/PrimeBuild-pc/LightCrosshair?label=issues&logo=github)](../../issues)
[![Pull Requests](https://img.shields.io/github/issues-pr/PrimeBuild-pc/LightCrosshair?label=pull%20requests&logo=github)](../../pulls)
[![Stars](https://img.shields.io/github/stars/PrimeBuild-pc/LightCrosshair?style=flat&label=stars&logo=github)](../../stargazers)
[![License](https://img.shields.io/github/license/PrimeBuild-pc/LightCrosshair?label=license)](LICENSE)
[![Codecov](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair/branch/main/graph/badge.svg)](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair)

[Download](../../releases/latest) • [Documentation](docs/SETTINGS.md) • [Report a bug](../../issues) • [Request a feature](../../issues) • [Support](#support-the-project)

</div>

---

## Overview

**LightCrosshair** is a lightweight crosshair overlay for Windows 10/11, designed for players who want a simple, customizable, and non-invasive overlay for borderless or windowed games.

It focuses on the essentials: a visible crosshair, profile-based configuration, quick hotkeys, optional performance information, and a design that avoids game injection or hook-based backends.

<div align="center">

[![Watch the demo video](https://img.youtube.com/vi/CKbj2eObQ1E/maxresdefault.jpg)](https://www.youtube.com/watch?v=CKbj2eObQ1E)

**Watch the demo on YouTube**

</div>

---

## Screenshots

<img width="1279" height="862" alt="image" src="https://github.com/user-attachments/assets/e658c09a-0768-48d9-89c6-c27e46c3d427" />

<img width="1280" height="1052" alt="image" src="https://github.com/user-attachments/assets/7b8463e6-8088-422d-bb97-532bf9edab6f" />

<img width="1259" height="1083" alt="image" src="https://github.com/user-attachments/assets/b7c6a235-d757-4175-8b29-588a470eebc5" />

<img width="1398" height="838" alt="image" src="https://github.com/user-attachments/assets/dca6b81a-c945-4ba8-811f-dfedadd3abbe" />

| Crosshair overlay | Settings window | Profiles |
| --- | --- | --- |
| `assets/screenshots/overlay.png` | `assets/screenshots/settings.png` | `assets/screenshots/profiles.png` |

Suggested folder structure:

```text
assets/
└── screenshots/
    ├── overlay.png
    ├── settings.png
    └── profiles.png
```

---

## Features

- **Custom crosshair overlay** with configurable shape, size, thickness, gap, color, opacity, and outline.
- **Profile workflow** for switching quickly between different games or visibility needs.
- **Visibility presets** for high-contrast setups without changing in-game rendering.
- **Optional performance overlay** with Off, Minimal, and Detailed display modes.
- **GPU driver integration** for supported NVIDIA and AMD-related display/color features.
- **Frame Cap Assistant** for target-FPS guidance.
- **Non-injected design**: no game hooks, no injection, and no native runtime backend.
- **Borderless/windowed friendly** for better overlay visibility.

---

## What's New in v1.7.0

- Fixed NVIDIA FPS cap profile binding so per-app cap controls target the selected application profile.
- Added NVIDIA profile audit information for safer inspection before applying supported per-app settings.
- Added NVIDIA Low Latency Off/On per-app controls.
- Added NVIDIA VSync per-app controls.
- Added read-only NVIDIA G-SYNC and Low Latency CPL State display.
- Kept NVIDIA safety boundaries: no global profile writes, no raw setting editor, no DLSS writes, no G-SYNC writes, and the app remains `asInvoker`.

---

## Download & Installation

Download the latest version from the **[GitHub Releases page](../../releases/latest)**.

### Package managers

Chocolatey and WinGet submission updates for v1.7.0 are being prepared. Use the
GitHub Release assets below until those package-manager updates are accepted and
published.

### Available release assets

- **Installer:** [`LightCrosshair-Setup-1.7.0.exe`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-Setup-1.7.0.exe)
  SHA256: `82E4D878DF7881F5DE88C4A9444C200F18CE1BD14E0C88AFEF9C05099808090E`
- **Portable ZIP x64:** [`LightCrosshair-v1.7.0-x64.zip`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-v1.7.0-x64.zip)
  SHA256: `672BC0E0C6DA33761969ACD3DD1D9BBC0A027B73140DCF64FD6C0C8B82765FD3`
- **Portable ZIP ARM64:** [`LightCrosshair-v1.7.0-ARM64.zip`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-v1.7.0-ARM64.zip)
  SHA256: `4AC93A4B3AC6DE15C08ECBA3CA151E387D701B1624689EA673FF4478ADDC0FBE`

### Recommended installation

1. Download the installer from the latest release.
2. Run the installer.
3. Launch **LightCrosshair** from the Start Menu.

### Portable version

1. Download the ZIP that matches your CPU architecture.
2. Extract it to a folder you control.
3. Run `LightCrosshair.exe`.
4. Open Settings with `Alt + L` or by clicking the tray icon.

---

## Requirements

- Windows 10 or Windows 11.
- .NET 8 Windows Desktop Runtime for framework-dependent builds.
- Borderless windowed or windowed game mode is recommended for overlay visibility.

---

## Usage

1. Launch **LightCrosshair**.
2. Open Settings with `Alt + L` or from the tray icon.
3. Customize your crosshair.
4. Save profiles for different games or preferences.
5. Enable the performance overlay only when needed.

### Default hotkeys

| Hotkey | Action |
| --- | --- |
| `Alt + X` | Toggle crosshair visibility |
| `Alt + C` | Next profile |
| `Alt + V` | Previous profile |
| `Alt + L` | Toggle Settings window |

---

## Limitations & Safety

- LightCrosshair does **not** inject into games.
- Exclusive fullscreen can hide normal overlays; use borderless windowed or windowed mode when possible.
- Frame Cap Assistant provides guidance only and does not enforce a real FPS cap.
- Frame-generation and pacing diagnostics currently use conservative heuristics, ETW-style present telemetry analysis, and optional RTSS fallback paths where available. Full PresentMon integration remains a future target and is not advertised as an implemented live runtime backend yet.
- Some games or anti-cheat systems may block overlays or behave differently.
- No overlay can guarantee universal compatibility with every game.

---

## Documentation

- [Settings & Options Guide](docs/SETTINGS.md)
- [Third-Party Notices](docs/THIRD_PARTY_NOTICES.md)

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

## Tech Stack

- **Framework:** .NET 8 Windows desktop app.
- **Graphics:** SkiaSharp primary renderer with automatic GDI fallback.
- **Settings:** profile-based configuration stored under the user profile.
- **Performance:** cached rendering and config-driven redraws to keep idle overhead low.

---

## Contributing

Bug reports, focused feature requests, documentation improvements, and tested pull requests are welcome.

Before opening a pull request, please run:

```bash
dotnet build LightCrosshair.sln
dotnet test LightCrosshair.sln
```

Recommended development setup:

- Visual Studio 2022 or VS Code.
- .NET 8 SDK.
- Windows 10/11 for desktop app testing.

---

## License

This project is licensed under the **MIT License**. See [LICENSE](LICENSE).

---

## Support the Project

<div align="center">

If LightCrosshair is useful to you, consider supporting the project.

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)
[![Star on GitHub](https://img.shields.io/github/stars/PrimeBuild-pc/LightCrosshair?style=social)](../../stargazers)
[![Report Bug](https://img.shields.io/badge/Report-Bug-red?logo=github)](../../issues)
[![Request Feature](https://img.shields.io/badge/Request-Feature-blue?logo=github)](../../issues)
[![Open Discussions](https://img.shields.io/badge/Open-Discussions-purple?logo=github)](../../discussions)

**Every star, issue report, feature request, and donation helps keep the project alive.**

Made with ❤️ for the gaming community.

</div>
