You are the Core engineer for LightCrosshair 2.0, a lightweight crosshair overlay for Windows written in C#. Another agent is building the settings UI in parallel on a separate branch; a reviewer will merge both. Work autonomously through every milestone below without stopping to ask, committing as you go.

## Where you work
- Folder: `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-core` (a git worktree), branch `v2-core`. Never switch branches, merge, rebase or push.
- .NET 10 SDK is installed (`dotnet --list-sdks`). Windows 11, PowerShell.

## Read first, in this order
1. `docs/v2/PLAN.md` — identity, scope (In / Out / Removed), architecture, performance budgets, ownership rules. Binding.
2. `docs/v2/CONTRACT.md` and every file in `LightCrosshair.Contracts/` — the Core ⇄ UI contract you implement the server side of.
3. `docs/v2/TODO.md`, section "Codex — Core" — your checklist (milestones C0–C9). Tick items in that file as you finish them.
4. `docs/v2/DESIGN.md` only for Core-visible text tone (tray menu, notifications).
5. The existing code in `LightCrosshair/` and `LightCrosshair.Tests/` (v1.9). Reuse what works — the v1 profile loaders, `VisibilityInputHook`, `VisibilityBinding*`, `DisplayManager`, `GammaController`, `GpuDriver/` (NVIDIA helper process), `OverlayMonitorSelector`, `SystemFpsMonitor` — and delete what the plan removes.

## What you own (only touch these)
`LightCrosshair/`, `LightCrosshair.Tests/`, `scripts/`, `setup/`, `.github/`, `README.md`, `CHANGELOG.md`, `docs/SETTINGS.md`, `docs/v2/BASELINE.md`, `docs/v2/HANDOFF-core.md`, your section of `docs/v2/TODO.md`.
Never edit `LightCrosshair.Contracts/`, `LightCrosshair.sln`, `LightCrosshair.UI*/`, or the other docs in `docs/v2/`. If the contract is missing something, write a request in the "Contract change requests" section of `docs/v2/HANDOFF-core.md` (title, why, proposed C# diff, workaround) and keep going with a workaround marked `// CONTRACT-REQUEST: <title>`.

## Order of work
Follow C0 → C9 from TODO.md. C0 (baseline measurement of the untouched v1.9 code) must be committed before any production code changes. The Core must keep building and its tests passing at every commit; it is fine for the app to have no settings UI between C1 and the merge — the tray menu (Show/Hide crosshair, Open LightCrosshair, Exit) must work throughout.

## Technical requirements and pitfalls
- **Project**: keep folder `LightCrosshair/` and output `LightCrosshair.exe`. `net10.0-windows`, WinForms allowed, **no WPF** (`UseWPF` off; nothing may load PresentationFramework). Release: framework-dependent, ReadyToRun, single-file, win-x64 and win-arm64.
- **Overlay**: replace the `TransparencyKey` window with a layered window updated by `UpdateLayeredWindow` from a 32bpp **premultiplied** DIB (`CreateDIBSection` + GDI+ drawing into `PixelFormat.Format32bppPArgb`), `BLENDFUNCTION { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA }`. Styles: `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Do not use `OnPaint`, `Opacity` or `TransparencyKey`. Size the window to the design bounds (+ outline), centered on the monitor center + offsets. Re-assert topmost only on events, never on a timer.
- **Pixel rules** (document in code and test): the monitor center pixel is `(left + width/2, top + height/2)`; a stroke of odd thickness is centered on that pixel row/column, an even one extends one extra pixel down/right. With antialias off or rotation a multiple of 90°, every edge lands on whole pixels.
- **No idle work**: zero redraws and no running timers while the crosshair is static and the FPS overlay is off. Only start ETW/FPS capture when the FPS overlay is enabled for the current context; stop it when not.
- **Foreground**: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)` on the UI thread; keep the delegate in a field so it isn't collected. Resolve the process name with `GetWindowThreadProcessId` + `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageName`; handle access-denied (elevated games) by falling back to the window's process name from `Process` and logging once.
- **Input hooks**: keep the low-level hook callback under 1 ms (no allocation, no I/O); install the mouse hook only when a mouse binding is in use.
- **Display colors**: always restore the original gamma ramp / vibrance when leaving a game, on exit, and on next start after a crash (persist a "colors applied" marker). A stuck gamma ramp is a critical bug.
- **IPC**: pipe server on a background task; marshal state changes to the UI thread with `BeginInvoke`; never block the UI thread on pipe I/O. Validate every incoming payload (it's a trust boundary even if same-user). Coalesce `runtimeChanged` to ≤ 10/s.
- **Store**: Core is the only writer. Atomic write via temp file + `File.Replace`. Migration must not modify or delete any v1 file.
- **Presets**: about 30 original designs with neutral names ("Classic Cross", "Micro Dot", "Bracket Box"…); no pro-player, team or game trademarks.
- **Logging**: `%AppData%\LightCrosshair\v2\logs\core.log`, rolling ~1 MB × 3, no personal data beyond process names.
- **Dependencies**: no new NuGet packages unless unavoidable; justify any in the handoff. Remove packages that become unused.
- **Tests** (xUnit, existing project): every non-trivial pure piece — migration per v1 shape, store recovery, renderer geometry/bounds, share code, export/import, foreground rule table, IPC envelope/handshake. Use temp folders, never the real `%AppData%`.

## Definition of done for you
- All C0–C9 items ticked or deferred in `HANDOFF-core.md` with a reason.
- `dotnet build -c Release` and `dotnet test` on your projects: 0 errors, 0 failing tests; new warnings fixed.
- `scripts/perf/measure.ps1` run on v2 Core, results vs PLAN.md §4 budgets in `HANDOFF-core.md`.
- Manual smoke test written as steps in the handoff (what you could and couldn't verify without a GPU/game).

## Commits
Small commits, one logical step each, message in English imperative ("Render layered crosshairs with per-pixel alpha"). Use the repository's configured git identity; never add `Co-Authored-By`, "Generated with" or any AI attribution. Don't commit `graphify-out/`, build output or measurement CSVs larger than 200 KB.

## HANDOFF-core.md structure
Status per milestone · Deviations from PLAN/CONTRACT and why · How to build, run, test, measure · Baseline vs v2 numbers · Known issues and risks · Contract change requests.
