# Core handoff

## Status per milestone

- C0 complete: baseline committed before production edits (2189a6c). Settings-cycle verification and ten-minute baseline remain unmeasured; see BASELINE.md.
- C1 platform build complete: .NET 10, WinForms tray bootstrap, no WPF reference. Removed legacy settings, frame-cap assistant, detailed FPS, RTSS, heuristics, diagnostics and update checking, with their feature-specific tests. Driver/color compatibility APIs are frozen; remaining legacy config fields are migration data only.
- C2–C9 in progress.

## Deviations from PLAN/CONTRACT and why

The requested worktree did not exist. Created v2-core from the reviewer’s committed v2 planning commit 72a3ed8. No merge, rebase or push.

Color driver transactions are serialized in background tasks; the crosshair updates immediately. The 50 ms foreground budget cannot include potentially slow vendor calls and must be measured separately. Capability flags currently reflect detected vendor, not a successful write probe; actual unsupported driver operations return errors. Physical NVIDIA/AMD restoration, HDR and hot-plug need hardware validation.

## How to build, run, test, measure

`dotnet build LightCrosshair/LightCrosshair.csproj -c Release`

`dotnet test LightCrosshair.Tests/LightCrosshair.Tests.csproj -c Release`

`pwsh scripts/perf/measure.ps1`

Packaging verification command: `pwsh scripts/build-release.ps1 -UiProject ../LightCrosshair-ui/LightCrosshair.UI/LightCrosshair.UI.csproj`. Outputs under `releases/LightCrosshair-v2.0.0-win-{x64,arm64}`. This uses the sibling UI worktree as it exists at build time, not a merged/reviewed release. Build intermediates are redirected into this worktree's releases folder. The installer keeps the original machine-wide installation scope; the local ZIP installer supports per-user extraction. Runtime/package identifiers were checked against [Microsoft deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) and [runtime installation documentation](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

## Baseline vs v2 numbers

See BASELINE.md. v2 pending.

## Known issues and risks

The settings executable is supplied by the separate UI workstream. Layered rendering is active. Hardware/DPI/hot-plug smoke checks remain to be run in C9.

## Contract change requests

### Filled boxes
Why: v1 Box has an independent fill color; the contract only defines an outlined Box.
Proposed optional C# addition: `public string? FillColor { get; set; }` on CrosshairLayer (null = unfilled).
Workaround: `// CONTRACT-REQUEST: Filled boxes` in LegacyMigration uses a thick inset Box below the border. Large fills (>128 px) cannot be represented exactly with max Thickness 64. Other migrated geometry follows the new integer-pixel convention; old rounded-cap X geometry and radius rounding can differ by a pixel.

### Legacy driver cap ownership
v1 stores NVIDIA caps in driver profiles, not in its JSON profile model. No v1 file is changed. Driver caps remain untouched until the v2 game profile explicitly manages one. Reading matching driver caps during migration is deferred to hardware validation to avoid initializing GPU APIs on first launch.
