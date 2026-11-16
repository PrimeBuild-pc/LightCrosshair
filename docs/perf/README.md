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

The idle runs used a static crosshair with FPS off and settings closed: a warmup followed by one-second samples. The old run measures the whole 1.9 process; the new run measures the resident 2.0 Core. Their target frameworks differ, as recorded in `runs.csv`. The older baseline does not verify its requested settings toggles, so those toggles are not included in `ui-cycles.csv`.

The Library soak opens the real UI on Library, scrolls through all presets and waits for every thumbnail response. The continuously running Core is sampled after each UI process exits and settles. No forced GC occurs in the plotted samples; the final heap dump is taken afterwards. The separate default-page UI-cycle run predates the Library allocation-budget fix; it is historical data, not the final Library result or an interactive-start benchmark.

Core data stayed in temporary folders. The Library runs used the explicitly authorized window-placement exception, backing up and restoring that file and verifying that every real profile file path and SHA256 remained unchanged. No user files or logs are included here.

## Regenerate

Install Python and `matplotlib`, then run from the repository:

```powershell
python scripts/perf/make-charts.py
```

The script uses only these tracked CSVs. It computes means, conversions, labels and the budget band from data, and writes [idle-v19-vs-v20.svg](idle-v19-vs-v20.svg) and [library-soak.svg](library-soak.svg). The SVGs are static, contain no JavaScript or external images/fonts, and retain Segoe UI text with the viewer's font fallback. `--preview-dir <local-folder>` also writes PNG previews outside the public documentation.
