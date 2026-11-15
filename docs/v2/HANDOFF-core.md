# Core handoff

## Status per milestone

- C0 complete: baseline committed before production edits (2189a6c). Settings-cycle verification and ten-minute baseline remain unmeasured; see BASELINE.md.
- C1 platform build complete: .NET 10, WinForms tray bootstrap, no WPF reference. Removed legacy settings, frame-cap assistant, detailed FPS, RTSS, heuristics, diagnostics and update checking, with their feature-specific tests. Driver/color compatibility APIs are frozen; remaining legacy config fields are migration data only.
- C2–C9 in progress.

## Deviations from PLAN/CONTRACT and why

The requested worktree did not exist. Created v2-core from the reviewer’s committed v2 planning commit 72a3ed8. No merge, rebase or push.

## How to build, run, test, measure

`dotnet build LightCrosshair/LightCrosshair.csproj -c Release`

`dotnet test LightCrosshair.Tests/LightCrosshair.Tests.csproj -c Release`

`pwsh scripts/perf/measure.ps1`

## Baseline vs v2 numbers

See BASELINE.md. v2 pending.

## Known issues and risks

The settings executable is supplied by the separate UI workstream. Bootstrap currently uses the old bitmap renderer until C3.

## Contract change requests

### Filled boxes
Why: v1 Box has an independent fill color; the contract only defines an outlined Box.
Proposed optional C# addition: `public string? FillColor { get; set; }` on CrosshairLayer (null = unfilled).
Workaround: `// CONTRACT-REQUEST: Filled boxes` in LegacyMigration uses a thick inset Box below the border. Large fills (>128 px) cannot be represented exactly with max Thickness 64. Other migrated geometry follows the new integer-pixel convention; old rounded-cap X geometry and radius rounding can differ by a pixel.

### Legacy driver cap ownership
v1 stores NVIDIA caps in driver profiles, not in its JSON profile model. No v1 file is changed. Driver caps remain untouched until the v2 game profile explicitly manages one. Reading matching driver caps during migration is deferred to hardware validation to avoid initializing GPU APIs on first launch.
