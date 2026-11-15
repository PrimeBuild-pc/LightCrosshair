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

None yet.
