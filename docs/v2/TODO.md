# LightCrosshair 2.0 — TODO

Check items off in your own branch as you finish them. Scope and rules: [PLAN.md](PLAN.md).

## Claude — branch `v2`
- [x] Decisions, plan, design system, contract docs
- [x] `LightCrosshair.Contracts` project (types, IPC envelope, JSON options)
- [ ] Answer contract change requests from the handoff files
- [ ] Merge `v2-core`, then `v2-ui`; add UI projects to the solution
- [ ] Integration test, performance check against budgets, review
- [ ] `v2` → `main`, release 2.0.0

## Codex — Core, branch `v2-core`
### C0 Baseline (before changing code)
- [ ] `scripts/perf/measure.ps1`: Release build, start app, sample private bytes, working set, CPU % and GDI/USER handles for 60 s idle; open/close settings 5 times; startup time. Writes CSV + summary.
- [ ] Run it on v1.9 code, write `docs/v2/BASELINE.md` (machine, numbers, method)
### C1 Platform and cleanup
- [ ] Retarget `LightCrosshair` and `LightCrosshair.Tests` to `net10.0-windows`; reference `LightCrosshair.Contracts`
- [ ] Remove WPF from Core: `SettingsWindow.xaml(.cs)`, `WpfSettingsHost.cs`, `UseWPF`
- [ ] Remove Frame Cap Assistant, FPS Detailed mode, graphs, pacing diagnostics, frame-gen heuristics, RTSS fallback, G-Sync/FreeSync/color-management status code, update checker (moves to UI), and their tests
- [ ] Update CI workflow to .NET 10 and the new solution layout
### C2 Store
- [ ] `store.json` load/save (atomic, debounced), invariants, corrupt-file recovery
- [ ] Backups (hourly, newest 10, before migration)
- [ ] Migration from v1 files per CONTRACT.md §5, v1 files untouched
- [ ] Tests: round-trip, every v1 shape and composite, process-bound profiles, hotkeys, corrupt store, idempotent migration
### C3 Overlay and renderer
- [ ] Layer renderer for Arms / Dot / Ring / Box / Image with rotation, offset, opacity, outline, antialias toggle; pixel-crisp when rotation is a multiple of 90°
- [ ] Per-pixel alpha overlay via `UpdateLayeredWindow`; click-through, topmost, no taskbar/Alt-Tab, no activation
- [ ] Redraw only on change; no timers while static
- [ ] Monitor selection with fallback; handle `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, monitor hot-plug
- [ ] `render` to PNG with the same renderer (< 5 ms at 128 px)
- [ ] Tests: geometry per kind, outline, bounds, rotation, render determinism
### C4 Game profiles
- [ ] `SetWinEventHook` foreground detection; process name resolution
- [ ] Apply / restore crosshair, display colors, FPS overlay override, visibility binding override
- [ ] Default crosshair + "show only in configured games"
- [ ] NVIDIA per-app FPS cap on save/delete, capability detection
- [ ] Known-games list (~50) + suggestion + tray notification → UI `--page games --suggest`
- [ ] Tests: rule table for foreground transitions (pure logic)
### C5 IPC and lifecycle
- [ ] Single-instance Core; pipe server per CONTRACT.md §2; all `IpcMethods`; events
- [ ] Start/focus/close UI; UI-absent behavior (tray menu: Show/Hide crosshair, Open LightCrosshair, Exit)
- [ ] Tests: envelope round-trip, handshake mismatch, pipelined requests, disconnect ends preview
### C6 Hotkeys
- [ ] Global hotkeys + input-hook bindings from `AppSettings.Hotkeys` and game overrides; v1.9 semantics preserved
- [ ] Registration failures reported in `RuntimeState.HotkeyErrors` (never silent)
### C7 Library features
- [ ] ~30 original presets (generic names, no player/team/game trademarks), tagged
- [ ] Share code encode/decode per CONTRACT.md §6 + tests
- [ ] Export/import `*.lcx.json` with embedded images + tests
- [ ] PNG import validation (format, ≤ 512 px side, ≤ 2 MB)
### C8 Localization, packaging, docs
- [ ] Core strings (tray menu, notifications) in en, it, es, zh-Hans, ru, ja
- [ ] Publish scripts produce both exes in one folder (x64, ARM64); installer, ZIP, winget/choco templates reference .NET 10 Desktop Runtime
- [ ] README, CHANGELOG 2.0.0, docs/SETTINGS.md rewritten for v2
### C9 Verification
- [ ] Run `measure.ps1` on v2 Core, compare with budgets in PLAN.md §4, write results in HANDOFF-core.md
- [ ] `docs/v2/HANDOFF-core.md` complete

## Cursor — UI, branch `v2-ui`
### U0 Foundation
- [ ] `LightCrosshair.UI` (WPF, `net10.0-windows`, MVVM with CommunityToolkit.Mvvm) and `LightCrosshair.UI.Tests` (xUnit); reference `LightCrosshair.Contracts`
- [ ] Theme: `Tokens.xaml` + control styles per DESIGN.md
- [ ] Localization: resx for 6 languages, runtime language switch, key-completeness test
### U1 Core client
- [ ] Pipe client per CONTRACT.md §2 (NDJSON, request/response correlation, events, reconnect, start Core if missing)
- [ ] `--fake-core` in-memory implementation of the same interface for development and tests (renders simple previews itself only in fake mode)
- [ ] Command line `--page`, `--suggest`; single instance; `focusRequested` / `closeRequested`
### U2 Shell
- [ ] Custom chrome window, sidebar navigation, visibility switch, Core status, window placement memory
### U3 Studio
- [ ] Preview via `render`, zoom 1x/2x/4x, backgrounds, pixel grid
- [ ] Layers: add (by kind), duplicate, reorder by drag and keyboard, hide, delete, max 8
- [ ] Inspector per kind; slider + numeric field pairs; color field
- [ ] Undo/redo (Ctrl+Z / Ctrl+Y), unsaved-changes guard, Save / Save as new
- [ ] Live in game (`preview`, throttled ≤ 30/s, final value always sent)
- [ ] Share code copy/paste; PNG import → Image layer
### U4 Library
- [ ] Presets + my crosshairs grid with thumbnails (cached), favorites, tag filter, search
- [ ] Apply as default, edit / edit a copy, duplicate, delete with confirmation, export/import file
### U5 Games
- [ ] Profile list + detail panel per DESIGN.md; enable switch
- [ ] Add from running processes or .exe browse; suggestion banner (accept / not now / never)
- [ ] Display colors, NVIDIA cap, FPS overlay override, visibility override; unsupported features explained inline
### U6 Hotkeys
- [ ] Binding capture (keys, mouse buttons, wheel, modifiers, modes, wheel = toggle only), conflicts, Core errors inline
### U7 Settings
- [ ] Display, behavior, FPS overlay, language, updates (port v1 update check + its tests), about, exit
### U8 Finish
- [ ] Keyboard navigation and automation names everywhere
- [ ] ViewModel tests against the fake core (Studio edit/undo, games rules, hotkeys conflicts, settings)
- [ ] Measure UI cold start and memory, record in handoff
- [ ] Dev flag `--fake-core --screenshots <dir>`: renders every page (and Studio with a 3-layer design) to PNG at 1180×740 and exits
- [ ] `docs/v2/HANDOFF-ui.md` complete, with the screenshot command and output
