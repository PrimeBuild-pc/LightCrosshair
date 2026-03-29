# 🎯 LightCrosshair

> **A lightweight, high-performance crosshair overlay designed for competitive gaming**

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/badge/Release-Latest-brightgreen)](../../releases)

LightCrosshair is a professional-grade crosshair overlay application that provides pixel-perfect accuracy and customization for gamers. With its transparent edges, vibrant neon colors, and intuitive interface, it's designed to enhance your gaming experience without impacting performance.

---

## ✨ Key Features

<details>
  <summary><b>🎨 Advanced Customization</b></summary>
  <br>

- **Custom Crosshair Builder**: Full control over shape, size, thickness, gap, edge, and inner layers
- **Integrated FPS Counter**: Optional on-screen FPS telemetry powered by an anti-cheat-safe pipeline
- **Display Color Adjustment**: Per-display gamma, contrast, brightness, and vibrance controls
- **Profile System**: Save, load, clone, rename, and switch presets for different games and playstyles
- **Unified Settings Experience**: All customization is centralized in the modern Settings Window
</details>

<details>
  <summary><b>🚀 Performance Optimized</b></summary>
  <br>

- **<1% CPU Usage**: Minimal impact during gaming sessions, powered by **SkiaSharp** rendering
- **Hardware Accelerated**: Zero-GC allocations and pixel-perfect native memory pinning
- **Safe FPS Counter**: Uses **ETW/PresentMon** for anti-cheat safe telemetry (no in-game hooking/injection)
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

### Option 1: Standalone Executable (Recommended)

1. **Download** the latest `LightCrosshair.exe` from the [Releases](../../releases) page
2. **Place** the executable in your preferred directory
3. **Run** `LightCrosshair.exe` — no installation required!
4. **Configure** your crosshair from the Settings Window (`Alt + L` by default, or left-click tray icon)

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/PrimeBuild-pc/LightCrosshair.git
cd LightCrosshair

# Build the application
dotnet build LightCrosshair.sln --configuration Release


dotnet publish LightCrosshair/LightCrosshair.csproj --configuration Release --runtime win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false
```

---

## 🎯 Usage Guide

<details>
  <summary><b>Getting Started</b></summary>
  <br>

1. **Launch** the application — on first run, the default profile appears centered on screen and the app starts in the system tray
2. **Open Settings Window** with `Alt + L` (default) if you want full settings UI (or left click on icon tray)
3. **Customize** shape, size, colors, rendering, and profiles from the Settings Window
4. **Save and switch profiles** directly in Settings; active configuration is persisted across restarts
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
- `Ctrl + Shift + Left` — Cycle to previous profile (default)
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