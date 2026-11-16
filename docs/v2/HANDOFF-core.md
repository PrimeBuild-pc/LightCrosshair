# Core handoff — LightCrosshair 2.0

Branch: `v2-core`, worktree: `../LightCrosshair-core`. Started from the reviewer’s committed plan/contract at 72a3ed8; the requested worktree did not yet exist. No branch switch, merge, rebase, push or publication. Contracts, solution, UI source and the other planning documents are unchanged.

Round 1 history follows; the **Review 1** section supersedes resolved deferrals and records this round's final validation (224 Core + 17 UI tests).

## Status per milestone

| Milestone | Status |
|---|---|
| C0 | Baseline committed as 2189a6c before production edits. Untouched v1.9: Release build, zero warnings/errors, 283 passing tests. Five requested v1 settings toggles were not verified; see BASELINE.md. |
| C1 | .NET 10 Core/tests, contract reference, no WPF dependency. Removed in-process settings and the excluded FPS/diagnostics/update functionality. Tray remains available when the UI executable is absent. |
| C2 | Atomic, durable, debounced single-writer store; hourly backups, retention, recovery; read-only v1 migration, including every shape/composite, active state, bindings and settings. Disabled saved color values are preserved (Review 1); v1 driver-only cap adoption remains deferred. |
| C3 | Shared Arms/Dot/Ring/Box/Image renderer and premultiplied DIB/UpdateLayeredWindow overlay. Pixel convention, bounds, outlines, rotations, images and determinism tested. No crosshair redraw timer. |
| C4 | Event-driven foreground contexts, default/game visibility, color recovery journal, FPS overrides, visibility overrides, 53 local suggestions and NVIDIA cap save/delete. Actual driver feature probing and v1 cap adoption deferred. |
| C5 | Same-user asynchronous pipe, bounded strict UTF-8, handshake, all IpcMethods, responses/events, UI launch/focus/close and transient preview lifecycle. |
| C6 | All four actions support keyboard/mouse; visibility supports holds; wheel toggles; context cycling, registration errors and conflicts. Hook health uses raw-input activity rather than a watchdog timer. Capture suspension is implemented in Review 1. |
| C7 | 30 original presets, share codes, bounded inflate, validated PNG import and JSON import/export with embedded images. |
| C8 | Six Core locales with complete-key tests, autorun setting, .NET 10 CI/packaging/installer/templates and v2 public docs. Both architectures publish. |
| C9 | Release solution build: zero warnings/errors; 215 tests pass. Published IPC and real-UI smoke checks pass. Performance measured below. Hardware checks and the explicitly unmet/deferred budgets remain review items. |

Shutdown waits for an in-flight store mutation instead of overwriting it with an older snapshot. Failed initial migration/load does not write a default v2 store marker. Queued obsolete color applies are discarded behind slow driver calls; leaving/exiting requests restoration immediately. A failed restoration retains the original-state journal and blocks new color changes.

## Deviations from PLAN/CONTRACT and why

- Legacy profile/config models, the old renderer used by migration reference tests, and frozen GPU compatibility interfaces remain compiled. They are not the runtime overlay/settings path; no WPF assembly is referenced by Core.
- Driver color transactions run serially in the background. Crosshair changes happen immediately; potentially slow vendor calls cannot be promised within the 50 ms foreground budget.
- Library JSON is capped at 7 MiB compact (with a separate 32 MiB file ceiling), reserving room for RuntimeState inside the 8 MiB pipe limit. An oversized save fails before replacing the last good store.
- `LIGHTCROSSHAIR_DATA_ROOT` redirects the whole v2 store/log/journal for tests. Optional `LIGHTCROSSHAIR_TEST_INSTANCE=<up to 32 ASCII alphanumerics>` appends `.test.<id>` to pipe/mutex names. Normal launches use the exact contract names. This is needed because other worktrees in the same Windows session can otherwise connect to, modify or exit a benchmark Core. Isolated perf fixtures unbind live system shortcuts and do not launch the UI.
- Original rounded line caps and some old outline/radius behavior are approximated by editable integer-pixel layers. Base T/X continuity and pen-centered ring/box size are preserved. Filled legacy boxes/rings now use the accepted FillColor field (Review 1), including 256 px fills.
- No Core NuGet dependencies were added. TraceEvent and NvAPIWrapper.Net remain from v1 and are used by the retained features.

## How to build, run, test, measure

From this worktree:

```powershell
dotnet build -c Release
dotnet test -c Release
pwsh scripts/validate-release-preflight.ps1 -CoreOnly
pwsh scripts/build-release.ps1 -UiProject ../LightCrosshair-ui/LightCrosshair.UI/LightCrosshair.UI.csproj
pwsh scripts/perf/smoke.ps1 -Isolated
pwsh scripts/perf/shutdown-smoke.ps1
pwsh scripts/perf/measure.ps1 -SkipBuild -Isolated -WarmupSeconds 600 -Executable <absolute-published-Core.exe>
```

After merge, omit `-UiProject`; the UI project must be in the same checkout. The publish script redirects build intermediates into this worktree. The sibling UI working tree was read/built, not edited; its current build is not a substitute for a reviewed merged release.

For actual UI lifecycle measurement, close other production Core/UI instances and run measure without `-Isolated`. `-Legacy` supports the old first-HWND probe for v1. Raw CSV/JSON results are local under ignored `scripts/perf/results*/`.

Packages: `releases/LightCrosshair-v2.0.0-win-{x64,arm64}` and corresponding ZIP/SHA256 files. PE machine values were verified as 8664/AA64; all four executables report 2.0.0.0. Both processes are framework-dependent, single-file and ReadyToRun. Packaging follows [Microsoft single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview); the required runtime identifier is documented by [Microsoft’s Windows installation guide](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

Inno Setup 6.6.1 compiled `setup/Output/LightCrosshair-Setup-2.0.0.exe` with both executables. The installer preserves machine-wide scope; ARM64 is provided as a portable ZIP. WinGet validated generated manifest syntax, with its standard warning that the declared runtime dependency was not validated. ZIP extraction with an actual SHA256 was verified. Artifacts are **not signed or published**. Rebuild final installer/hashes/manifests after merging the UI.

## Baseline vs v2 numbers

2026-11-16, Windows 11 build 26200, Ryzen 7 5800X3D (16 logical processors), Radeon RX 6950 XT, SDK 10.0.403 / runtime 10.0.14. Memory is MiB. CPU is percent of one logical core, not divided by 16.

| Metric | v1.9 | v2 | PLAN budget / result |
|---|---:|---:|---|
| Private memory | 20.15 average after 10 s warmup | 17.71 average after 600 s warmup + 60 s sampling | ≤40: pass in tested fixture |
| Working set | 73.00 | 65.57 | Informational |
| CPU, 60 s average | 0.5652% | 0.0770% | ≤0.1%: pass |
| Static overlay redraws/s | Not instrumented | 0 | 0: pass |
| Repeating application timers, FPS/recording off | Not instrumented | 0 | Pass |
| GDI / USER handles | 37 / 41 | 24 / 23 | Informational |
| First Core HWND | 670.7 ms, old auxiliary-window probe | 176.8 ms, actual Core window | Different probe targets; not an interactive-start comparison |
| Private-memory increase after 5 real UI open/exit cycles | Unverified | +5.27 MiB; repeat +5.91 MiB | **Target +3 MiB missed** |
| UI cold window + pipe connected | Unmeasured | 711.6 ms | Proxy only; interactive-ready budget ≤1.5 s not established |
| Render, 128 px, 50 IPC round trips | Unmeasured | median 1.355 ms, P95 1.536 ms | <5 ms renderer budget: pass in tested design |
| Preview, 50 IPC round trips including UpdateLayeredWindow | Unmeasured | median 0.900 ms, P95 1.040 ms | ≤50 ms: pass in tested design |
| Foreground event → configured game fully applied | Unmeasured | Unmeasured on a game | Deferred; rule transitions unit-tested, vendor completion excluded |

The second five-cycle test verified every UI window/connection and process exit. Idle private memory averaged 13.91 MiB in that short run; after each close it was 16.41, 17.67, 19.45, 19.64 and 19.80 MiB. Growth slowed after the third cycle, but this does not prove absence of long-term retention. Serializer/PNG/runtime initialization is a likely contributor, not a profiled attribution. The +3 MiB target remains unmet even though total private memory stays below 40 MiB.

Live read-only probes found no PresentationFramework/PresentationCore loaded in Core, including after real UI cycles. The long-run fixture used isolated names and unbound test shortcuts. One earlier production-name run was discarded: another client became active during idle and Core exited; stale-process samples are not used. The final non-idle migration/shutdown/FPS corrections were rebuilt and smoke-tested separately; the static overlay/FPS-off path measured above is unchanged. A final 60-second regression run on the final package measured 16.72 MiB private, 0.0258% CPU, zero redraws/timers and no WPF. Its warmup was 10 seconds, so it is not used as the ten-minute result.

## Known issues and risks

- **C4 deferred:** capabilities currently reflect detected vendor rather than successful per-feature driver probes. Operations still fail explicitly on unsupported hardware. NVIDIA hardware was unavailable for an actual probe/cap/vibrance test; no driver-write success is claimed.
- **C2/C4 deferred:** pre-existing v1 NVIDIA FPS caps live in the driver, with no ownership marker in v1 JSON. They are left untouched rather than adopted and later cleared without validated ownership. New v2 managed caps have save/delete behavior.
- Actual AMD/NVIDIA color changes, forced-kill recovery on hardware, HDR switching, elevated-game behavior, mixed-DPI/hot-plug and ARM64 execution remain manual checks. Runtime monitor DPI metadata currently defaults to 100%; physical-pixel rendering/display-change handling is implemented.
- Recording detection uses recorder-process presence at a two-second interval, not observation of actual capture.
- Input-hook callbacks were reviewed to avoid allocation/I/O; their <1 ms latency was not measured under game input load.
- Clean-VM install/upgrade/uninstall and signing validation were not performed. No VM/checkpoint was created or changed.
- Memory retention after repeated UI cycles and the unmeasured game/interactive-ready budgets require follow-up before performance claims.

Manual smoke evidence and repeat steps:

1. Launch packaged Core with a temporary `LIGHTCROSSHAIR_DATA_ROOT`; run `smoke.ps1 -Isolated`. Verified snapshot, 30 presets, design CRUD, hidden-visibility preview override, rendering, share codes, PNG import, JSON export/import, game CRUD, disconnect ending preview and graceful quit. The separate shutdown-smoke script also verified an interrupted save is retained on exit, and a failed migration neither writes store.json nor changes v1 data.
2. Launch the real packaged UI against a separate temporary production-name Core. With agent-desktop accessibility, uncheck **Show crosshair**; verified `crosshairVisible=false` in Core’s store.
3. Check **Live in game**. Verified the overlay becomes visible while the saved visibility remains false. Window styles were 0x080900A8, including layered, click-through, topmost, tool-window and no-activate flags.
4. Click the accessible **Close** button. Verified UI windows disappear, the preview overlay hides, and Core remains alive. WPF-loaded probe in Core remained zero. Closed only the Core started for this test.
5. Run five programmatic OpenSettings hotkey open/close cycles; verified five real UI process exits in two runs.
6. On appropriate hardware, repeat with a configured game, every binding mode, FPS on/off, color apply/leave, forced termination/restart, monitor unplug/DPI changes and installer upgrade/uninstall. These hardware/clean-system steps are **not verified here**.

## Contract change requests from round 1

All three additions (FillColor, DisplayColorsEnabled, setInputCapture) were accepted in the merged contract and are implemented in Review 1 below. No contract or UI files were edited by Core in this round.

## Review 1

- C-1: Remap invalid legacy IDs consistently across designs/default/game links; merge a v1.9 `working::<32 hex>` into `SourceProfileId` before validating v2 identity; skip/log individual invalid profiles. Exact JSON fixture and regression tests pass; published x64 Core starts from a copied fixture and returns one edited source/default via IPC (`scripts/perf/migration-smoke.ps1`).
- C-2: Failed load/migration keeps Core and IPC running with editable in-memory defaults; saves/shutdown cannot create `store.json`, allowing retry next launch. One localized tray notification is logged; errors include type, message and stack. Exclusive legacy-file lock regression and published `shutdown-smoke.ps1` verify survival, one notification, unchanged v1, no marker and graceful exit; all 218 Core + 17 UI tests pass.
- C-3 (partially fixed; final acceptance deferred): Remove duplicate IPC JSON/string buffers, finish/dispose connection writers and CTS, release saved store bytes, reuse the foreground process-path buffer, dispose launched Process handles and disable Release tiered compilation while retaining ReadyToRun. Twenty real-UI cycles improve five-cycle growth from +6.94 to +2.81 MiB, with +0.80 MiB residual drift from cycle 5 to 20. Heap/counter evidence and limits are below. A fully isolated repeat and strict flat-trend acceptance are deferred until the UI owner fixes its hard-coded placement path; Core cannot edit that file under this round's ownership.
- C-4: Every Core test gets an async-context data/log root under a unique temporary directory, plus a safe per-run fallback for discovery/constructors. A guard writes both default log and store, asserts the per-test path and excludes production AppData. Full Release build has 0 warnings/errors; 219 Core + 17 UI tests pass without external environment setup.
- C-5: All five diagnostic window probes (0x8064–0x8067, 0x8069) are gated by Debug or an explicit valid `LIGHTCROSSHAIR_TEST_INSTANCE`; production names expose none. Release build: 0 warnings/errors; 219 Core + 17 UI tests pass. `probe-smoke.ps1` verifies all production responses are zero and the test-instance magic is enabled.
- C-6: The 30 presets now rotate through white, green, yellow, cyan, magenta and red (five designs per color), retaining outlined variants for contrast. Stable preset identities/tags are unchanged. Release build: 0 warnings/errors; 219 Core + 17 UI tests pass.
- C-7: Implement accepted Box/Ring FillColor in shared rendering, validation/share round-trip and single-layer v1 migration; retain disabled display values and honor DisplayColorsEnabled in game rules; suspend/unregister all hotkeys/hooks during a renewable input-capture lease. Unit tests cover pixels/opacity, invalid fill, share, migration, rules and lease boundaries; published `input-capture-smoke.ps1` verifies all actions, false/disconnect, renewal and actual 60-second expiry. Release: 0 warnings/errors; 223 Core + 17 UI tests pass.

### Review 1 performance evidence and remaining limit

Same Windows/CPU/runtime as the round-1 table. Both compared runs used the merged real UI next to published x64 Core, production pipe names (no `-Isolated`), a fresh temporary Core data root, 10 s warmup, 60 s idle sampling and 20 verified UI open/close/process-exit cycles. The harness waits for the UI window plus pipe occupancy/handshake and 200 ms settling. Baseline package: code through d9d99a7; final package: all Review 1 changes. No other Core/UI was running at the start of either run.

| Metric (MiB unless stated) | Before | After |
|---|---:|---:|
| Idle private, 60 s average | 13.88 | 13.39 |
| Idle CPU, percent of one core | 0.0256 | 0.0000 (sampling resolution) |
| Private before UI cycles | 13.78 | 13.30 |
| Private after cycle 5 | 20.72 | 16.11 |
| Private after cycle 20 | 27.89 | 16.91 |
| Increase after 5 cycles (budget +3) | +6.94 | +2.81 |
| Increase from cycle 5 to 20 | +7.17 | +0.80 |
| Process handles, cycle 5 / 20 | 510 / 526 | 496 / 502 |
| UI cold window + connection + settling, ms | 895.9 | 915.4 |

Final per-cycle private values: 15.16, 15.73, 15.91, 15.99, 16.11, 15.51, 15.69, 15.82, 15.94, 16.08, 15.62, 15.76, 15.88, 15.89, 16.04, 16.23, 16.54, 16.66, 16.79, 16.91. The five-cycle budget passes in this sample; +0.80 MiB residual growth is not claimed as an exactly flat private-memory trend. Window/connection time does not establish interactive readiness.

**Isolation limitation found after these runs:** UI `WindowPlacementStore.FilePath` uses the real roaming `LightCrosshair/v2/ui-placement.json`, ignoring `LIGHTCROSSHAIR_DATA_ROOT`; UI closing/exit calls Save. Those UI runs could therefore read/overwrite real window placement. Core store, migration, logs and color journal used temporary roots throughout. The real placement file was not read, deleted or restored after discovering this (its original value was not captured). No further real-UI runs were made. `measure.ps1` now refuses real-UI cycles while that known hard-coded source path remains, rather than silently repeating this breach. The UI owner must route placement through the supplied data root, then repeat the final acceptance run. The numbers above are diagnostic evidence, not a claim that the entire UI run met the requested isolation rule.

Profiling used local dotnet-gcdump/dotnet-counters 10.0.750501, System.Runtime counters, and heap dumps after cycles 5 and 20. Before changes, the real-UI profiling run had 1,888,595 -> 1,891,249 live heap bytes (+2,654), despite growing private commitment. Both dumps had one PipeServer, two CTS and two Bitmap objects; no surviving per-connection reader/channel/MemoryStream accumulation appeared. This does not support a managed per-session object leak in those suspects. It also does not fully attribute native/runtime commitment.

After discovering the UI isolation issue, the separate `connection-profile.ps1` safely exercised 20 Core-only IPC connections and 600 preset PNG renders, without starting UI. Live heap was 1,727,709 -> 1,676,123 bytes, with the same stable PipeServer/CTS/Bitmap counts. This is a Core connection/render stress check, **not** a replacement for the deferred real-UI profiling repeat. Heap collection forces GC and adds diagnostic overhead; instrumented private/CPU values are excluded from budget comparisons. The tool warned that .NET 10 uses lossy EventPipe buffering; no completeness guarantee is asserted.

An experimental GCConserveMemory=9 run did not improve private growth and was discarded; it is not shipped. Disabling tiered compilation reduced the five-cycle increase in a controlled comparison, and the foreground callback was allocating a fresh 32,768-character buffer on every window change. Release now retains ReadyToRun without tiered recompilation, and reuses a 260-character path buffer, expanding only for long paths. This is workload-specific tuning, not a GC-on-disconnect workaround. Runtime behavior is documented by [Microsoft's compilation settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation).

Local raw evidence (ignored): `scripts/perf/results-review-before-verified`, `results-review-final`, `results-review-profile-before`, `results-review-connections-after`, `results-review-final-idle`. A final safe isolated 60 s Core-only run measured 14.65 MiB private, 0.0000% CPU at sampling resolution, zero redraws/repeating application timers and no WPF. Final published smoke: render median/P95 1.370/1.629 ms; preview median/P95 0.872/1.093 ms. `probe-smoke.ps1` passes for normal and test-instance launches. Full solution Release build: zero warnings/errors, 224 Core + 17 UI tests pass.

`TODO.md` is unchanged as explicitly required by the ownership boundary. Its Core milestones remain implemented, and its existing NVIDIA feature-probe/adoption deferral remains open. Reviewer-owned contract-answer/integration checkboxes should be reconciled with the already merged v2; this round does not edit that reviewer-owned file.

## Review 2

- C-8: Migrate the active v1 EnableCustomCrosshair switch into global Settings.CrosshairVisible, using the working copy when valid and the selected saved profile otherwise; ignore the flag on inactive profiles and keep every migrated layer visible. Opposite saved/working-copy JSON fixtures and six regression cases cover precedence, fallback and an already hidden overlay. Published x64 Core starts on copies of both fixtures; the Show crosshair settings mutation makes Runtime.CrosshairShown and the native overlay window visible and records a presented frame. Whole-solution Release: 0 warnings/errors; 230 Core + 22 UI tests pass.
- C-9: Complete a before/after 50-session real-UI Library soak, waiting for all 31 successful thumbnail replies in every session (1,550 per run). Bound Release Core's Gen 0 allocation budget to 4 MiB so short-lived render/IPC garbage is collected naturally instead of accumulating across idle gaps. Private memory now plateaus at 17.63–18.08 MiB over the last 20 sessions, ending at 18.078 MiB versus 51.191 before; five-session increase is +2.457 MiB. No forced GC or cached/fake render shortcuts were used in the curve. The authorized placement-only backup/restore exception is guarded by a full real-profile file-list/SHA256 comparison: all 9 files, including restored placement, are identical before/after. Full Release build has 0 warnings/errors; 230 Core + 22 UI tests pass.

### Review 2 Library soak

2026-11-16, same Windows 11 / Ryzen 7 5800X3D / .NET 10.0.14 machine as above. The baseline is commit 27884be (C-8 applied, no GC budget override); the final package adds only the Release Core `System.GC.Gen0MaxBudget=4194304` setting. The original GDI+ renderer and PNG encoder remain in use. No UI, contracts or solution source was edited.

| Session | Before (MiB) | After (MiB) |
|---|---:|---:|
| 0 | 13.727 | 13.680 |
| 1 | 15.641 | 15.668 |
| 5 | 17.008 | 16.137 |
| 10 | 20.891 | 17.488 |
| 15 | 24.344 | 17.711 |
| 20 | 28.938 | 17.633 |
| 25 | 31.902 | 17.723 |
| 30 | 36.090 | 17.984 |
| 35 | 39.902 | 18.055 |
| 40 | 43.051 | 18.062 |
| 45 | 47.555 | 18.078 |
| 50 | 51.191 | 18.078 |

Full 50-point before/after curve: [review2-library-soak.csv](../../scripts/perf/baselines/review2-library-soak.csv). Final sessions 31–50 range from 17.633 to 18.078 MiB (mean 17.980); the endpoint at session 50 is 18.078. GDI handles remain 24, USER handles settle at 27, process handles fall from 402 to 397, and Core's test-only assembly probe reports no WPF. The five-cycle increase falls from +3.281 to +2.457 MiB. These values are after each UI process exits and Core settles for 500 ms, rather than measurements of the UI process.

The baseline's final diagnostic heap is only 1,752,473 live bytes despite 51.191 MiB private commitment. A follow-up counter sample showed growing allocated heap and zero Gen 0/1/2 collections in its interval. This points to delayed collection of transient allocations, not a surviving per-session object graph. An alternative PNG encoder and GCConserveMemory experiment did not fix the curve and were removed. The final fix caps the allocation budget for generation 0, **not** the live heap or allowed library size; no heap hard limit, GC-on-disconnect, background cleanup timer, or render cache was added. This CLR tuning key is defined in the [.NET 10 runtime configuration source](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/gc/gcconfig.h); remeasure after runtime upgrades. The tradeoff is more natural collections while the UI renders thumbnails. Published IPC smoke still measures render median/P95 1.306/1.855 ms and preview 0.894/1.041 ms.

The harness starts `LightCrosshair.UI.exe --page library` 50 times against one continuously running Core. Core uses an isolated test pipe; an external same-user relay exposes the production pipe to the unchanged UI, forwards the original requests/responses, and counts successful 96px PNG replies by design id. It injects no requests and does not alter response payloads. Accessibility scrolls the Presets list so offscreen cards load too; after all 31 thumbnails are returned, the harness allows 500 ms for WPF to paint, closes the real UI window, verifies process exit and waits another 500 ms before sampling Core. The total ready time includes scrolling and automation; it is not a UI cold-interactive latency measurement. A final gcdump is collected only **after** all 50 samples and therefore cannot flatten the reported curve; its live heap is 1,567,377 bytes. The diagnostic tool's .NET 10 lossy-buffering warning still applies.

Reproduce after publishing Core and UI together:

```powershell
pwsh scripts/perf/library-soak.ps1 -Sessions 50 -AllowUiPlacementBackup
pwsh scripts/perf/profile-guard-smoke.ps1
```

`LIGHTCROSSHAIR_DATA_ROOT` points to a fresh temporary directory throughout. Under the user's explicit exception, only the real `ui-placement.json` may be used: it is copied before launching either process, restored byte-for-byte after both processes stop (or removed if originally absent), and every file path/hash under real `%AppData%\LightCrosshair` is compared with the original manifest. Both complete 50-session runs passed: 9 files before/after, no added/removed/changed files and placement restored. Store, profiles and logs stayed unchanged. `measure.ps1` also accepts the same opt-in `-AllowUiPlacementBackup` with identical protection; its one-session integration check passed. Guard smoke covers existing placement (including BOM/newlines), missing placement, an empty profile and detection of an unrelated file change, all in synthetic temporary roots.

Raw ignored evidence: `scripts/perf/results-library-50-before/`, `results-library-50-final/` (CSV, summary, final heap, plot and real-profile verification manifest). All temporary Core/UI processes have been closed. Review 1's memory deferral is superseded by this larger Library soak under the authorized placement exception; U-16 remains the UI owner's source change.

Final safe isolated idle regression (10 s warmup + 60 s sample): 14.69 MiB private, 0.0000% CPU at sampling resolution, zero redraws/repeating application timers and no WPF. The full solution Release build and all 230 Core + 22 UI tests pass with the shipped configuration.

## Release docs

- Produced tracked public data in `docs/perf/`: `idle-samples.csv` (120 original samples), `library-soak.csv` (baseline plus all 50 sessions before/after), `ui-cycles.csv` (baseline plus 20 verified sessions), `runs.csv`, `budgets.csv`, source/method README, and static `idle-v19-vs-v20.svg` / `library-soak.svg`.
- `scripts/perf/make-charts.py` regenerates both charts from the tracked CSVs alone. Font/colors follow DESIGN; memory uses explicitly labeled decimal MB, CPU is percent of one logical core, and zero CPU is marked as sampling resolution. Original binary values are retained separately. The +3 MB band is identified as the session-five target rather than a bound for all 50 sessions.
- Idle 1.9 source: `scripts/perf/results-v1/idle.csv` + summary, Release x64 target net8 at 72a3ed8. Idle 2.0 source: `scripts/perf/results-review2-idle/idle.csv` + summary, Release x64 .NET 10 Core matching ba83c0d. All samples are retained; plotted means are calculated, not copied from rounded summaries.
- Library before/after sources: `scripts/perf/results-library-50-before/summary.json` (27884be) and `results-library-50-final/summary.json` (ba83c0d). The per-session memory, 50-session count and 31 thumbnails/session come directly from those records. Default-page UI lifecycle source: `scripts/perf/results-review-final/summary.json` (37f1104); historical 20-session data is kept separate from final Library results.
- Measurement timing and machine/build captions come from the original summaries, baseline/handoff and benchmark script: AMD Ryzen 7 5800X3D, Windows 11 build 26200; 10 s warmup and 60 one-second idle samples; Library paint/post-exit settling 500 ms each. Public dates are CET. Budget 3 MB at session five comes from PLAN section 4.
- Replaced `readme-banner.svg` with a flat 1200×280 banner: five exact red rectangles, no gradients/filters/glow, Segoe UI wordmark and the requested tagline. `scripts/assets/make-banner.py` loads only the icon generator's draw function/constants, extracts its five solid rectangular components at 192 px and emits their SVG geometry without rewriting the app icon. Raster preview inspected; XML shape/style checks passed.
- Rewrote `README.md`, the 2.0.0 entry in `CHANGELOG.md`, and `docs/SETTINGS.md` against current UI XAML/view models, Contracts defaults and Core validation/runtime behavior. Download names/architectures match `scripts/publish.ps1`, `setup/LightCrosshair.iss`, WinGet template and Chocolatey nuspec. Release asset links use the releases index; concrete 2.0 checksums remain TBD and package-manager commands are explicitly conditional on publication. Official .NET Desktop Runtime and supported-Windows links were checked.
- Screenshot references are exactly `docs/images/ui-studio.png`, `ui-library.png`, `ui-games.png`, `ui-hotkeys.png`, `ui-settings.png`, using fixed-width thumbnails in a three-column and a two-column table. All five PNG names/signatures were checked in `../LightCrosshair-ui/docs/images`; they are not yet in this Core checkout and were not copied or edited. They must arrive through UI integration for GitHub images to display. `scripts/check-release-docs.py` checks local links/anchors, Windows case-sensitive paths, static SVG structure and absence of concrete README checksums; its optional screenshot-source argument reports these five paths as pending rather than pretending they are local.
- Settings verification includes the design-wide antialias switch, Ring/Box fill, disabled display-color preservation, editor-visible ranges versus Core import validation, save behavior, capture suspension, the persistent driver-cap behavior and recording-process detection. The automatic-update preference is stored but has no automatic check invocation in the current UI; the guide states that Check now performs checks rather than promising automatic updates. NVIDIA hardware remains explicitly unverified. No product code was changed.

### Source of every README number

| README number / numeric fact | Source |
|---|---|
| 2.0 / 2.0.0 in heading, package names and pinned install commands | Product/assembly versions, `scripts/publish.ps1`, Inno Setup output name, WinGet template and `setup/chocolatey/LightCrosshair.nuspec` |
| .NET 10 runtime/SDK, Windows 11 and supported Windows editions | Current project target frameworks and official Microsoft download/install pages linked in the README; no patch version is pinned |
| x64 / ARM64 package variants | Publish script Runtime values and installer x64 configuration |
| Eight layers | `ContractInfo.MaxLayers`, Core Validation and Studio layer controls |
| 30 presets | `PresetCatalog.Build()` entries and recorded getPresets/thumbnail counts |
| 1% low | `FpsOverlaySettings.Show1PercentLow`, SettingsPage and Core FpsBuffer/FpsOverlay |
| Upgrade from 1.9 / original v1 files / old 1.x driver caps | LegacyMigration, CoreStore, ApplyDriverCapAsync and v2 scope; original files are read-only and old unmanaged caps are not adopted |
| 60 one-second samples, 10-second warmup | Each idle run summary plus all 120 tracked `idle-samples.csv` rows; captions use `runs.csv` |
| Idle bars: private MB and CPU % | Means computed by `make-charts.py` directly from original PrivateBytes / CpuOneCorePercent columns. Memory divides by 1,000,000; no rounded summary value is substituted. CPU mean zero is marked as resolution-limited |
| 50 Library sessions, 31 thumbnails/session, before/after curve | Both Library summaries, now all rows in `library-soak.csv`; chart reads those rows and metadata |
| +3 MB band / five-session target | PLAN section 4, transcribed in `budgets.csv`; decimal MB is explicit and original MiB measurements remain in separate CSV columns |
| 20-session UI open/close link | VerifiedSettingsCycles and PrivateAfterEachSettingsCycleMB in `results-review-final/summary.json`, retained in `ui-cycles.csv`; explicitly historical, before final Library fix |
| Alt+X, Alt+C, Alt+V, Alt+L | `HotkeySettings` defaults in Contracts and InputBindings/Core action handlers |
| SHA256 / TBD | Algorithm/package placeholder names, not measured release hashes; no 1.x binary checksum remains in README |

The charts' machine, OS build, framework, date and source-commit numbers also come from `runs.csv`; the public performance-data README documents the conversions and original sources. The 1200×280 banner, 192 px icon sampling and screenshot thumbnail widths are layout specifications, not performance claims. Language names map to `ContractInfo.SupportedLanguages` and the UI locale resources.

Release-doc validation: every commit passed a whole-solution Release build with zero warnings/errors and all 230 Core + 24 UI tests. Chart regeneration is byte-identical locally; all 120 idle samples match their source columns exactly. SVG structure rejects script/filter/gradient/external-image elements. The link audit checks 19 unique local targets, rejects wrong-case paths on Windows and validates GitHub-style anchors; five screenshot targets remain explicitly pending UI integration, verified from the UI checkout without copying them. Fixed the old README license link to the real `LICENSE.md`. External release/runtime/package pages were checked; no release, tag, push or remote write was performed.

## Review 3

- Cycling (decision 3): use favorites only when at least two exist, otherwise all user crosshairs, ordered like Library (UpdatedAt descending). Persist into the enabled foreground game profile or the default and publish snapshotChanged through the normal store transaction; remove the temporary override. The published cycling smoke failed before the fix on the single-favorite branch and passes afterwards (next/previous, snapshot event and disk persistence). Ten tests cover 0/1/2/3 favorites, library order, directional boundaries, configured/unconfigured/disabled games, empty/single libraries and independent defaults. Whole-solution Release: zero warnings/errors; 240 Core + 28 UI tests pass.
- FPS (G-9, decision 7): retain DXGI/D3D9 Present-start ETW, add MPO Present-start (55), ignore test presents and keep independent bounded swapchain histories; restore v1's five-second retry with one error log per repeated failure, serialize session replacement and await shutdown. Show “–” plus a reason in all six Core locales after two seconds without frames, on permission failure or with no foreground PID. Fourteen tests cover providers, swapchains, stale data, PID changes, session cancellation, simulated access failure/recovery and locale resources. Release: zero warnings/errors; 254 Core + 28 UI tests pass. The DX11/DX12 matrix below passes, including actual visible numbers in local screenshots. The Fortnite/EAC-specific failure was not reproduced by these independent apps; this is not a claim of a completed Fortnite retest.

FPS comparison with `main:LightCrosshair/SystemFpsMonitor.cs`: v1 also enabled only DXGI/D3D9 and filtered event-header PID; it did not subscribe to kernel/DxgKrnl or resolve swapchain ownership through a kernel correlation graph. It retried ETW after failures and also read external RTSS shared memory. The latter remains excluded by PLAN §2. No automatic elevation, game injection or driver has been added. DX12 goes through DXGI Present just like DX11; the [PresentMon DXGI event definitions](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/ETW/Microsoft_Windows_DXGI.h) establish the ordinary/MPO IDs. Permission failures now explain the missing numbers instead of leaving an unexplained box; [Windows ETW access rules](https://learn.microsoft.com/en-us/windows/win32/etw/controlling-event-tracing-sessions) still apply.

The test application is `scripts/perf/SwapchainProbe.cpp`, built by `build-swapchain-probe.ps1` with the installed MSVC/Windows SDK and `/W4 /WX`. It clears and presents a hardware swapchain with vsync, records QPC intervals, supports a monitor-sized borderless window and deliberately pauses presentation at five seconds. `fps-smoke.ps1` runs all four cases against an isolated published Core, checks the foreground process, a positive FPS sample at three seconds and its expiry after presentation stops. Baseline is `efb081b` plus the read-only gated FPS counter; after is the FPS patch. CSV source: `results-review3/fps-{before,after}/matrix.csv`, curated in [review3-fps.csv](../perf/review3-fps.csv). No real store or log was used.

| Hardware swapchain | Before FPS | After FPS | After pause (before → after) |
| --- | ---: | ---: | --- |
| DX11 windowed | 359.95 | 359.96 | 0, no reason → 0, localized reason |
| DX11 borderless | 359.98 | 361.15 | 0, no reason → 0, localized reason |
| DX12 windowed | 359.96 | 357.87 | 0, no reason → 0, localized reason |
| DX12 borderless | 357.56 | 358.14 | 0, no reason → 0, localized reason |

These are spot readings, not a throughput improvement benchmark. A second visual run (`results-review3/fps-visual`, optional `-Screenshots`) confirms rendered FPS/frametime/1% low text over the DX12 borderless window; screenshots stay local. Session permission was available to the current non-elevated token; access-denied/recovery behavior was tested using a failing capture provider, without changing system privileges.
