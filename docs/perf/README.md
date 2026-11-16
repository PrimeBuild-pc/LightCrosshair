# Performance measurements

These are measurements of local Release x64 pre-release builds on Windows 11 build 26200, AMD Ryzen 7 5800X3D. They describe the recorded builds and workload, not a guarantee for other machines or a measurement of game performance.

## Data

| File | Contents | Original local source |
|---|---|---|
| [idle-samples.csv](idle-samples.csv) | All idle private-byte and CPU samples for 1.9 and 2.0 | `scripts/perf/results-v1/idle.csv`, `scripts/perf/results-review2-idle/idle.csv` |
| [library-soak.csv](library-soak.csv) | Initial memory and every session in the before/after Library soak; original MiB and converted MB | `scripts/perf/results-library-50-before/summary.json`, `scripts/perf/results-library-50-final/summary.json` |
| [ui-cycles.csv](ui-cycles.csv) | Initial memory and verified UI open/close cycles on the default page | `scripts/perf/results-review-final/summary.json` |
| [runs.csv](runs.csv) | Machine, builds, dates, timing, methods and source paths | Original run summaries and Core handoff |
| [budgets.csv](budgets.csv) | The five-session growth target | Project performance budget |

MB means 1,000,000 bytes in the charts. Original PowerShell summaries used binary `1MB` (1,048,576 bytes); the soak and UI-cycle CSVs preserve those values in `*_mib` columns. Conversions do not add measurement precision. The public budget band uses the plan's literal +3 MB target and applies at session five. A zero CPU sample is below the timing resolution of this run, not proof of zero CPU work.

The idle runs used a static crosshair with FPS off and settings closed: a warmup followed by one-second samples. The old run measures the whole 1.9 process; the new run measures the resident 2.0 Core. Their target frameworks differ, as recorded in `runs.csv`. The 2.0 idle fixture also unbinds global shortcuts to avoid interference with concurrent work; it is a test fixture, not a game workload. The older baseline does not verify its requested settings toggles, so those toggles are not included in `ui-cycles.csv`.

The Library soak opens the real UI on Library, scrolls through all presets and waits for every thumbnail response. The continuously running Core is sampled after each UI process exits and settles. No forced GC occurs in the plotted samples; the final heap dump is taken afterwards. The separate default-page UI-cycle run predates the Library allocation-budget fix; it is historical data, not the final Library result or an interactive-start benchmark.

Core data stayed in temporary folders. The Library runs used the explicitly authorized window-placement exception, backing up and restoring that file and verifying that every real profile file path and SHA256 remained unchanged. No user files or logs are included here.

## Review 3 game probes

The separate [FPS matrix](review3-fps.csv), [UI-open samples](review3-ui-open.csv), [3D frame summaries](review3-ui-game-frames.csv) and [display-color results](review3-display-colors.csv) cover the in-game review. They do not replace the historical idle/soak chart data above. These runs use a Ryzen 7 5800X3D, Radeon RX 6950 XT (driver 32.0.21045.26001) and Windows build 26200.

- `fps-smoke.ps1` tests hardware DX11/DX12 swapchains in windowed and borderless modes. It verifies that the probe is visible and foreground, samples Core telemetry at three seconds and checks that it expires after the probe stops presenting. Baseline is efb081b with a read-only gated FPS counter; after includes raw ETW event decoding. Zero is a missing sample, not a zero-throughput renderer. The CSV records the separate reason/expiry counters.
- `ui-game-bench.ps1` runs five separate UI processes on each side with the same UI binary. Before uses normal process/thread priorities; after uses the Core launch/render-queue patch. First frame means the first WPF UCE Present-end ETW event (11035), using the original event timestamp and launch PID even if buffers arrive after exit; visible-window time is recorded separately. Core CPU covers the first two seconds after each open. CPU counters are quantized and the small sample does not establish a precise improvement percentage.
- The DX12 probe records QPC intervals around its own vsynced clear/Present/fence loop; summaries exclude the first 360 frames. This is a light hardware-swapchain workload, not displayed-frame pacing in a heavy game. All launches require a verified foreground 3D window. Earlier hidden-window runs were discarded.
- `display-colors-smoke.ps1` compares complete backend snapshots before/after the isolated Core applies/restores a game profile. Only counts and statuses are tracked; original channel values and local logs remain private. AMD gamma 100 now avoids an unnecessary unsupported gamma-ramp path; gamma 115 is refused by this machine's gamma-capture path and reports `unsupported` once. Every run restores the original snapshot exactly.

The [Core handoff](../v2/HANDOFF-core.md#review-3) records the full methods, before/after values and limitations. Fortnite/EAC itself was not retested. The unchanged UI still exceeds the cold 300 ms goal, and its parking/early-paint work must be integrated before testing the warm goal. The UI-open and hidden-window tests also compare all real profile file paths/SHA256 hashes, backing up/restoring only the explicitly permitted window-placement file. No real store, profiles or logs are changed.

## Regenerate

Install Python and `matplotlib`, then run from the repository:

```powershell
python scripts/perf/make-charts.py
```

The script uses only these tracked CSVs. It computes means, conversions, labels and the budget band from data, and writes [idle-v19-vs-v20.svg](idle-v19-vs-v20.svg) and [library-soak.svg](library-soak.svg). The SVGs are static, contain no JavaScript or external images/fonts, and retain Segoe UI text with the viewer's font fallback. `--preview-dir <local-folder>` also writes PNG previews outside the public documentation.
