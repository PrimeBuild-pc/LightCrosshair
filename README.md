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

**LightCrosshair** adds a customizable crosshair overlay to Windows 10/11 games and desktop apps, with quick hotkeys, profiles, optional performance information, and supported per-app GPU driver settings. It is designed for borderless or windowed play and does not inject into games or use game hooks.

---

## Demo

<div align="center">

[![Watch the demo video](https://img.youtube.com/vi/CKbj2eObQ1E/maxresdefault.jpg)](https://www.youtube.com/watch?v=CKbj2eObQ1E)

**Watch the demo on YouTube**

</div>

## Screenshots

<table>
  <tr>
    <td width="33%">
      <img alt="LightCrosshair overlay and settings" src="https://github.com/user-attachments/assets/06fbdd78-4c0d-4a23-a63a-d9cb3943fd9f" />
    </td>
    <td width="33%">
      <img alt="Crosshair customization settings" src="https://github.com/user-attachments/assets/ded3a5c3-d734-49ab-8b50-1954d1250a3f" />
    </td>
    <td width="33%">
      <img alt="Profile management settings" src="https://github.com/user-attachments/assets/c7aa1464-5eb4-47ec-a67f-9e615f16a94e" />
    </td>
  </tr>
  <tr>
    <td width="33%">
      <img alt="Performance overlay settings" src="https://github.com/user-attachments/assets/32b0bcbd-d940-47da-8eda-20a90dddfb62" />
    </td>
    <td width="33%">
      <img alt="GPU driver settings" src="https://github.com/user-attachments/assets/c0743ae5-fb07-4ce7-b42f-e3a6b676c969" />
    </td>
    <td width="33%">
      <img alt="NVIDIA profile controls" src="https://github.com/user-attachments/assets/89edc82b-580a-4869-a647-8700b69fec1e" />
    </td>
  </tr>
</table>

---

## Quick Install

Download the latest build from the **[GitHub Releases page](../../releases/latest)**.

### Installer

Use the installer for the normal Start Menu experience:

- [`LightCrosshair-Setup-1.7.0.exe`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-Setup-1.7.0.exe)
  SHA256: `82E4D878DF7881F5DE88C4A9444C200F18CE1BD14E0C88AFEF9C05099808090E`

### Portable ZIP

Use the portable ZIP if you want to extract and run the app from a folder you control:

- [`LightCrosshair-v1.7.0-x64.zip`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-v1.7.0-x64.zip)
  SHA256: `672BC0E0C6DA33761969ACD3DD1D9BBC0A027B73140DCF64FD6C0C8B82765FD3`
- [`LightCrosshair-v1.7.0-ARM64.zip`](https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-v1.7.0-ARM64.zip)
  SHA256: `4AC93A4B3AC6DE15C08ECBA3CA151E387D701B1624689EA673FF4478ADDC0FBE`

### Package Managers

WinGet package updates may lag behind GitHub Releases. When available, this may install the latest approved package version, not necessarily v1.7.0:

```powershell
winget install --id PrimeBuild.LightCrosshair --exact
```

Chocolatey submission updates are pending and are not advertised as a live install channel yet.

---

## Quick Start

1. Launch **LightCrosshair** from the Start Menu or run `LightCrosshair.exe` from the portable folder.
2. Open Settings with `Alt + L` or the tray icon.
3. Customize the crosshair shape, size, gap, color, opacity, and outline.
4. Save profiles for different games or visibility needs.
5. Enable the performance overlay only when you need it.

### Default Hotkeys

| Hotkey | Action |
| --- | --- |
| `Alt + X` | Toggle crosshair visibility |
| `Alt + C` | Next profile |
| `Alt + V` | Previous profile |
| `Alt + L` | Toggle Settings window |

---

## Features

- Custom crosshair builder with shape, size, thickness, gap, color, opacity, and outline controls.
- Profile workflow for switching quickly between games or setups.
- Visibility presets for high-contrast crosshairs.
- Optional performance overlay with Off, Minimal, and Detailed modes.
- Frame Cap Assistant for target-FPS guidance.
- Supported NVIDIA per-app profile controls and AMD-related display/color paths.
- Multi-monitor and DPI-aware overlay behavior.
- Non-injected design with no game hooks.

---

## What's New in v1.7.0

- Fixed NVIDIA FPS cap profile binding so supported per-app cap controls target the selected application profile.
- Added NVIDIA profile audit information before applying supported per-app settings.
- Added NVIDIA Low Latency Off/On and VSync per-app controls.
- Added read-only NVIDIA G-SYNC and Low Latency CPL state display.
- Kept safety boundaries: no global NVIDIA profile writes, no raw setting editor, no DLSS writes, no G-SYNC writes, and the app remains `asInvoker`.

---

## Requirements

- Windows 10 or Windows 11.
- .NET 8 Windows Desktop Runtime for framework-dependent builds.
- Borderless windowed or windowed game mode is recommended for overlay visibility.

---

## Limitations & Safety

- LightCrosshair does **not** inject into games and does **not** use game hooks.
- Exclusive fullscreen can hide normal overlays; use borderless windowed or windowed mode when possible.
- Frame Cap Assistant provides guidance only and does not enforce a real FPS cap.
- Frame pacing diagnostics use conservative heuristics, ETW-style present telemetry analysis, and optional RTSS fallback paths; PresentMon integration remains a future target and is not implemented as a live runtime backend.
- Frame-generation and DLSS runtime control are not implemented.
- NVIDIA profile writes are per-app only where supported; LightCrosshair does not write global NVIDIA profiles.
- Some games or anti-cheat systems may block overlays or behave differently.

---

## Documentation

- [Settings & Options Guide](docs/SETTINGS.md)
- [Third-Party Notices](docs/THIRD_PARTY_NOTICES.md)

---

## Build From Source

```bash
git clone https://github.com/PrimeBuild-pc/LightCrosshair.git
cd LightCrosshair
dotnet build LightCrosshair.sln --configuration Release
dotnet test LightCrosshair.sln
```

Contributions are welcome through focused bug reports, feature requests, documentation updates, and tested pull requests.

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
