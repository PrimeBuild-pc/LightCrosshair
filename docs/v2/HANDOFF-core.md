# Core handoff — LightCrosshair 2.0

Branch: `v2-core`, worktree: `../LightCrosshair-core`. Started from the reviewer’s committed plan/contract at 72a3ed8; the requested worktree did not yet exist. No branch switch, merge, rebase, push or publication. Contracts, solution, UI source and the other planning documents are unchanged.

## Status per milestone

| Milestone | Status |
|---|---|
| C0 | Baseline committed as 2189a6c before production edits. Untouched v1.9: Release build, zero warnings/errors, 283 passing tests. Five requested v1 settings toggles were not verified; see BASELINE.md. |
| C1 | .NET 10 Core/tests, contract reference, no WPF dependency. Removed in-process settings and the excluded FPS/diagnostics/update functionality. Tray remains available when the UI executable is absent. |
| C2 | Atomic, durable, debounced single-writer store; hourly backups, retention, recovery; read-only v1 migration, including every shape/composite, active state, bindings and settings. Disabled saved color values and v1 driver-only cap adoption have the deferrals below. |
| C3 | Shared Arms/Dot/Ring/Box/Image renderer and premultiplied DIB/UpdateLayeredWindow overlay. Pixel convention, bounds, outlines, rotations, images and determinism tested. No crosshair redraw timer. |
| C4 | Event-driven foreground contexts, default/game visibility, color recovery journal, FPS overrides, visibility overrides, 53 local suggestions and NVIDIA cap save/delete. Actual driver feature probing and v1 cap adoption deferred. |
| C5 | Same-user asynchronous pipe, bounded strict UTF-8, handshake, all IpcMethods, responses/events, UI launch/focus/close and transient preview lifecycle. |
| C6 | All four actions support keyboard/mouse; visibility supports holds; wheel toggles; context cycling, registration errors and conflicts. Hook health uses raw-input activity rather than a watchdog timer. Capture suspension needs the contract request below. |
| C7 | 30 original presets, share codes, bounded inflate, validated PNG import and JSON import/export with embedded images. |
| C8 | Six Core locales with complete-key tests, autorun setting, .NET 10 CI/packaging/installer/templates and v2 public docs. Both architectures publish. |
| C9 | Release solution build: zero warnings/errors; 215 tests pass. Published IPC and real-UI smoke checks pass. Performance measured below. Hardware checks and the explicitly unmet/deferred budgets remain review items. |

Shutdown waits for an in-flight store mutation instead of overwriting it with an older snapshot. Failed initial migration/load does not write a default v2 store marker. Queued obsolete color applies are discarded behind slow driver calls; leaving/exiting requests restoration immediately. A failed restoration retains the original-state journal and blocks new color changes.

## Deviations from PLAN/CONTRACT and why

- Legacy profile/config models, the old renderer used by migration reference tests, and frozen GPU compatibility interfaces remain compiled. They are not the runtime overlay/settings path; no WPF assembly is referenced by Core.
- Driver color transactions run serially in the background. Crosshair changes happen immediately; potentially slow vendor calls cannot be promised within the 50 ms foreground budget.
- Library JSON is capped at 7 MiB compact (with a separate 32 MiB file ceiling), reserving room for RuntimeState inside the 8 MiB pipe limit. An oversized save fails before replacing the last good store.
- `LIGHTCROSSHAIR_DATA_ROOT` redirects the whole v2 store/log/journal for tests. Optional `LIGHTCROSSHAIR_TEST_INSTANCE=<up to 32 ASCII alphanumerics>` appends `.test.<id>` to pipe/mutex names. Normal launches use the exact contract names. This is needed because other worktrees in the same Windows session can otherwise connect to, modify or exit a benchmark Core. Isolated perf fixtures unbind live system shortcuts and do not launch the UI.
- Original rounded line caps and some old outline/radius behavior are approximated by editable integer-pixel layers. Base T/X continuity and pen-centered ring/box size are preserved. Filled legacy boxes use up to two fill layers beneath the border, including 256 px fills.
- No Core NuGet dependencies were added. TraceEvent and NvAPIWrapper.Net remain from v1 and are used by the retained features.

## How to build, run, test, measure

From this worktree:

```powershell
dotnet build -c Release
dotnet test LightCrosshair.Tests/LightCrosshair.Tests.csproj -c Release
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
- **C2 deferred:** disabled v1 display-color values remain in untouched originals and `backups/migration`, but are not retained as editable disabled values in GameProfile; see the contract request.
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

## Contract change requests

### Filled boxes

Why: a single Box layer has no independent fill; migration needs extra editable layers.

Proposed optional C# addition to CrosshairLayer:

```csharp
public string? FillColor { get; set; } // null = unfilled
```

Workaround: `// CONTRACT-REQUEST: Filled boxes` in LegacyMigration adds one or two nested thick boxes beneath the border. It covers sizes through 256 px within the current layer/stroke limits.

### Suspend actions during binding capture

Why: Core cannot know when the UI is capturing a replacement binding. Existing global actions, including OpenSettings, can fire during capture; v1 could suspend them in-process.

Proposed C# additions:

```csharp
// IpcMethods
public const string SetInputCapture = "setInputCapture";
// Payload
public sealed class InputCaptureRequest
{
    public bool Active { get; set; }
}
```

Behavior: transient suppression of registered actions/hooks, reset on disconnect; UI captures locally.

Workaround: the marked comment in RunInputActionAsync records that no capture lifecycle message exists. Actions remain active; no unreliable focus-based guess is used.

### Preserve disabled display color profiles

Why: v1 can retain saved color values with its enable switch off. A nullable DisplayColors currently represents off and cannot also retain those values.

Proposed optional addition to GameProfile:

```csharp
public bool DisplayColorsEnabled { get; set; } = true;
```

Workaround: the marked migration branch uses null to preserve disabled behavior; source values remain recoverable from v1 and its migration backup. Backfill them before final release if the contract is extended.

## Review 1

- C-1: Remap invalid legacy IDs consistently across designs/default/game links; merge a v1.9 `working::<32 hex>` into `SourceProfileId` before validating v2 identity; skip/log individual invalid profiles. Exact JSON fixture and regression tests pass; published x64 Core starts from a copied fixture and returns one edited source/default via IPC (`scripts/perf/migration-smoke.ps1`).
- C-2: Failed load/migration keeps Core and IPC running with editable in-memory defaults; saves/shutdown cannot create `store.json`, allowing retry next launch. One localized tray notification is logged; errors include type, message and stack. Exclusive legacy-file lock regression and published `shutdown-smoke.ps1` verify survival, one notification, unchanged v1, no marker and graceful exit; all 218 Core + 17 UI tests pass.
