# 🎯 LightCrosshair

> **A lightweight, high-performance crosshair overlay designed for competitive gaming**

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Codecov](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair/branch/main/graph/badge.svg)](https://codecov.io/gh/PrimeBuild-pc/LightCrosshair)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/badge/Release-Latest-brightgreen)](../../releases)

LightCrosshair is a professional-grade crosshair and telemetry overlay application built for competitive gaming. It provides pixel-precise rendering, process-aware display color control, and a modern profile workflow with minimal system impact.

[![Watch the demo video](https://img.youtube.com/vi/CKbj2eObQ1E/maxresdefault.jpg)](https://www.youtube.com/watch?v=CKbj2eObQ1E)

**⬆️ Watch demo video on YouTube**

**v1.4.0 baseline**: Distribution assets are being prepared for portable ZIP, Inno Setup, Chocolatey, WinGet, and the PowerShell install script. These channels are not final until release artifacts, checksums, and explicit publication approval exist. MSIX is intentionally excluded.

---

## ✨ Key Features

<details>
  <summary><b>🎨 Advanced Customization</b></summary>
  <br>

- **Custom Crosshair Builder**: Full control over shape, size, thickness, gap, edge, and inner layers
- **Integrated FPS Overlay**: Optional on-screen FPS, frametime graph, 1% lows, and source status from ETW-style present telemetry with optional RTSS fallback
- **Process-Aware Display Color Management**: Per-profile gamma/contrast/brightness/vibrance that auto-applies when the target game process is in foreground
- **Modern Profile Workflow**: Immutable default profile, up to 10 profile slots, working-state editing, and save-current-to-selected behavior
- **Unified Settings Experience**: All customization is centralized in the modern Settings Window
</details>

<details>
  <summary><b>🚀 Performance Optimized</b></summary>
  <br>

- **Low CPU Footprint**: Designed for minimal overhead during gameplay sessions
- **Cached Rendering Path**: SkiaSharp primary renderer with automatic GDI fallback and config-driven redraws
- **Non-injected FPS Counter**: LightCrosshair uses ETW-style present telemetry and does not inject into games. Optional RTSS fallback may inherit RTSS compatibility or anti-cheat risks depending on RTSS configuration and the target game. PresentMon is not an implemented runtime backend in 1.4.0.
- **Independent Flip Mode**: Run the crosshair and FPS overlay in Independent Flip for an ultra-low latency setup. Note: Requires Optimizations for windowed games enabled in Windows 11 settings and MPO (Multi-Plane Overlay) active (Windows default).
</details>

<details>
  <summary><b>🎮 Gaming Features</b></summary>
  <br>

- **Pixel-Perfect Centering**: Mathematically precise positioning on all displays
- **Screen Recording Detection**: Auto-hide during streaming/recording
- **Multi-Monitor Support**: Works correctly on all display configurations
- **DPI Awareness**: Scales properly on high-DPI displays
</details>

<details>
  <summary><b>🔧 User Experience</b></summary>
  <br>

- **Modern Settings Window**: Dedicated settings UI for all crosshair controls, profiles, and hotkeys
- **Minimal Tray Menu**: Right-click tray menu is intentionally slimmed down to About and Exit
- **System Tray Integration**: Unobtrusive background operation
- **Profile Management**: Save and switch between multiple configurations
- **Atomic Cloud-Ready Saves**: Configuration files safely stored in `%AppData%` using atomic writes to prevent corruption
</details>

---

## 📦 Installation

### Release Status

The 1.4.0 release is not published yet. GitHub Releases, Chocolatey, WinGet, and the PowerShell install script are prepared/planned channels only until final artifacts and SHA256 checksums are created and explicitly approved for publication.

### Portable Package

The current portable ZIP pipeline is framework-dependent by default. The prepared ZIPs require the .NET 8 Windows Desktop Runtime unless a self-contained package is built explicitly.

Future release flow after approval:

1. Download the final `LightCrosshair-v1.4.0-<arch>.zip` from GitHub Releases.
2. Verify the published SHA256 checksum.
3. Extract the ZIP and run `LightCrosshair.exe`.
4. Configure your crosshair from the Settings Window (`Alt + L` by default, or left-click tray icon).

### Prepared Package Channels

- Chocolatey: package metadata is prepared, but no 1.4.0 package should be installed or advertised until it is pushed after release approval.
- WinGet: manifest work is prepared/planned, but no 1.4.0 submission should be advertised until final release URLs and hashes exist.
- PowerShell install script: the script is prepared, but should not be hosted or advertised for 1.4.0 until it has a final artifact URL and SHA256 checksum.
- Inno Setup: `setup/LightCrosshair.iss` can build a local installer from `setup/publish/win-x64` after publish, but the installer must not be published without release approval.

### Build from Source

```bash
# Clone the repository
git clone https://github.com/PrimeBuild-pc/LightCrosshair.git
cd LightCrosshair

# Build the application
dotnet build LightCrosshair.sln --configuration Release

# Publish framework-dependent output matching the current release baseline
dotnet publish LightCrosshair/LightCrosshair.csproj --configuration Release --runtime win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false
```

---

## 🎯 Usage Guide

> **Important:** For the overlay to be visible in games, use **Borderless Windowed** mode. In **Exclusive Fullscreen**, the overlay may not be visible.

<details>
  <summary><b>Getting Started</b></summary>
  <br>

1. **Launch** the application — on first run, the default profile appears centered on screen and the app starts in the system tray
2. **Open Settings Window** with `Alt + L` (default) if you want full settings UI (or left click on icon tray)
3. **Customize** shape, size, colors, outline, and rendering from the Settings Window
4. **Set target process behavior** for crosshair visibility and display color management (global or game-specific)
5. **Save and switch profiles** directly in Settings; active working state and source profile are persisted across restarts
</details>

<details>
  <summary><b>Context Menu Navigation</b></summary>
  <br>

- **About** → Shows app info, license, and project links
- **Exit** → Safely closes the app and tray process
- **Left-click tray icon** → Opens the full Settings Window for all crosshair customization
</details>

<details>
  <summary><b>Keyboard Shortcuts</b></summary>
  <br>

- `Alt + X` — Toggle crosshair visibility
- `Alt + C` — Cycle to next profile (default)
- `Alt + V` — Cycle to previous profile (default)
- `Alt + L` — Toggle settings window (default)
- Right-click tray icon — Open minimal tray menu (About / Exit)

All hotkeys above are configurable in Settings.
</details>

---

## 🛠️ Technical Specifications

<details>
  <summary><b>Architecture</b></summary>
  <br>

- **Framework**: .NET 8.0 (Windows) Windows Forms
- **Graphics**: SkiaSharp primary renderer with automatic GDI fallback
- **Rendering**: Cached rendering with regeneration only when config changes
- **Threading**: Asynchronous operations for UI responsiveness
</details>

<details>
  <summary><b>Performance Metrics</b></summary>
  <br>

- **Startup Time**: <500ms (ReadyToRun optimized)
- **Memory Usage**: ~50MB baseline, stable during operation
- **CPU Impact**: <1% during idle gaming, <2% during settings interactions
- **Rendering Latency**: <16ms (60+ FPS equivalent)
</details>

<details>
  <summary><b>Compatibility</b></summary>
  <br>

- **Windows Versions**: 10 (1809+), 11 (all versions)
- **Display Scaling**: 100%, 125%, 150%, 200% DPI scaling
- **Multi-Monitor**: Primary and secondary display support
- **Gaming Software**: Compatible with OBS, XSplit, Discord overlay
</details>

---

## 🤝 Contributing

We welcome contributions from the gaming and development community! Here's how you can help:

### Ways to Contribute

- 🐛 **Report Bugs** — Submit detailed issue reports
- 💡 **Suggest Features** — Share ideas for new functionality
- 🔧 **Submit Code** — Fix bugs or implement new features
- 📖 **Improve Documentation** — Help make guides clearer
- 🧪 **Test Builds** — Try pre-release versions and provide feedback

### Development Setup

```bash
# Prerequisites:
# - Visual Studio 2022 or VS Code
# - .NET 8.0 SDK
# - Git

git clone https://github.com/PrimeBuild-pc/LightCrosshair.git
cd LightCrosshair
dotnet restore
dotnet build
```

<details>
  <summary><b>Coding Standards</b></summary>
  <br>

- Follow C# naming conventions
- Add XML documentation for public methods
- Include unit tests for new features
- Maintain <1% performance impact
- Test on multiple Windows versions
</details>

<details>
  <summary><b>Pull Request Process</b></summary>
  <br>

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request with a detailed description
</details>

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

| Permitted | Restricted |
|---|---|
| ✅ Commercial use | ❌ Liability |
| ✅ Modification | ❌ Trademark use |
| ✅ Distribution | |
| ✅ Private use | |

---

## 🙏 Acknowledgments

- **Gaming Community** — For feedback and feature requests
- **Open Source Contributors** — For code improvements and bug fixes
- **Beta Testers** — For helping identify and resolve issues
- **.NET Team** — For the excellent framework and tools

---

## 📞 Support & Contact

- **Issues**: [GitHub Issues](../../issues) — Bug reports and feature requests
- **Discussions**: [GitHub Discussions](../../discussions) — Community support
- **Documentation**: [Wiki](../../wiki) — Detailed guides and tutorials

---

<div align="center">

**Made with ❤️ for the gaming community**

[![PayPal](https://img.shields.io/badge/Supporta%20su-PayPal-blue?logo=paypal)](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

[⭐ Star this repo](../../stargazers) • [🐛 Report Bug](../../issues) • [💡 Request Feature](../../issues) • [🤝 Contribute](../../pulls)

</div>
