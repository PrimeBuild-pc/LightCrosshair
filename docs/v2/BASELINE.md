# LightCrosshair 1.9 baseline

Measured 2026-11-16 on Windows 11 build 26200, AMD Ryzen 7 5800X3D (16 logical processors), .NET SDK 10.0.403 building the untouched net8.0-windows application at 72a3ed8.

Command: `pwsh scripts/perf/measure.ps1 -OutputDirectory scripts/perf/results-v1`.
Release build, framework dependent win-x64, 10 seconds warmup, 60 samples at one second intervals. CPU is percent of one logical core, not divided by the processor count. This is a development desktop measurement, not an isolated benchmark.

| Metric | v1.9 |
|---|---:|
| Private memory, average | 20.15 MiB |
| Working set, average | 73.00 MiB |
| CPU, average, one core | 0.5652% |
| First top-level HWND | 670.7 ms |
| GDI / USER handles, final idle sample | 37 / 41 |
| Private memory delta after 5 requested settings toggles | -0.93 MiB |

The five settings toggles were delivered as targeted WM_HOTKEY messages; their open/closed state was **not verified** (agent-desktop exposed no window for this hidden process). The delta therefore does not establish settings retention. First HWND is not UI interactive time. Ten-minute memory and redraw count are not measured by this baseline run. FPS-off/static defaults were used; no GPU/game workload was launched. Raw local CSV and summary are excluded from commits.

Validation before production changes: Release build, zero warnings/errors; 283/283 existing xUnit tests pass.
