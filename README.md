# LightCrosshair 2.0

A layered crosshair overlay for Windows. Version 2.0 is under development; released 1.x packages remain on the [releases page](https://github.com/PrimeBuild-pc/LightCrosshair/releases).

Build a crosshair from up to eight layers: arms, dots, rings, boxes, corner brackets and PNG images. Start from 30 original local presets, save favorites, share a compact `LC2:` code, or export a `.lcx.json` file with embedded images.

Game profiles select a crosshair, display colors, visibility binding and optional FPS overlay when a configured executable is in the foreground. NVIDIA per-app FPS caps are available on supported hardware. Display colors return to their original values when you leave the game or exit; a saved recovery journal supports restoration after a crash.

The tray Core and settings app are separate processes. Closing settings ends the UI process. The Core uses event-driven drawing and foreground detection; FPS capture starts only when enabled. It does not inject into games, read game memory or capture the screen. Input continues to reach other applications.

## Run

Use Windows 11 or a supported Windows 10 installation with [.NET Desktop Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Extract the package and keep `LightCrosshair.exe` and `LightCrosshair.UI.exe` together. Start `LightCrosshair.exe`, then open settings from the tray or with Alt+L. Borderless or windowed games work best; exclusive fullscreen may hide ordinary desktop overlays.

| Default binding | Action |
|---|---|
| Alt+X | Show/hide crosshair |
| Alt+C / Alt+V | Next/previous favorite (all designs if no favorites) |
| Alt+L | Open/close settings |

Bindings support keyboard and mouse modifiers. Visibility also supports Hold to hide and Hold to show; wheel bindings toggle only. Registration conflicts appear in settings.

## Upgrade and data

On first v2 launch, existing v1 profiles and preferences are imported into `%AppData%\LightCrosshair\v2`. Original v1 files remain unchanged. Core alone writes the new store, with automatic backups and corrupt-file recovery. Copy the entire v2 folder to back up both designs and images. English, Italian, Spanish, Simplified Chinese, Russian and Japanese are supported.

## Build

Install .NET 10 SDK, then run:

```powershell
dotnet build LightCrosshair/LightCrosshair.csproj -c Release
dotnet test LightCrosshair.Tests/LightCrosshair.Tests.csproj -c Release
pwsh scripts/build-release.ps1
```

A full package requires the UI project in the same checkout. Packaging produces framework-dependent ReadyToRun single-file executables for win-x64 and win-arm64. `-CoreOnly` is a development build switch. Release artifacts are prepared locally; see [release preparation](setup/RELEASE_PREP_2.0.0.md).

## Documentation and license

[Settings guide](docs/SETTINGS.md) · [Third-party notices](docs/THIRD_PARTY_NOTICES.md) · [MIT license](LICENSE)

## Support the Project

<div align="center">

If LightCrosshair is useful to you, consider supporting the project.

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)
[![Report Bug](https://img.shields.io/badge/Report-Bug-red?logo=github)](../../issues)
[![Request Feature](https://img.shields.io/badge/Request-Feature-blue?logo=github)](../../issues)
[![Open Discussions](https://img.shields.io/badge/Open-Discussions-purple?logo=github)](../../discussions)

**Every star, issue report, feature request, and donation helps keep the project alive.**

Made with ❤️ for the gaming community.

</div>
