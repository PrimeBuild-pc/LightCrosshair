# LightCrosshair 2.0 — Plan

Status: agreed 2026-11-16. This file is the source of truth for scope and architecture.
Related: [TODO.md](TODO.md) (checklist), [CONTRACT.md](CONTRACT.md) (Core ⇄ UI contract), [DESIGN.md](DESIGN.md) (UI design system).

## 1. Identity

**The best crosshair editor on Windows, and you don't notice it's running once Settings is closed.**

- Crosshair-first. LightCrosshair is not a "gaming overlay hub".
- Look: serious, modern, clean. Dark theme only. One accent color. No gradients, glow, neon, italic shouting, mascots or "PRO" badges.
- Non-invasive: no injection, no game hooks, no game memory access, no screen capture, no kernel drivers.
- Lightweight by construction: the always-running process never loads the settings UI.

## 2. Scope

### In
| Area | What |
|---|---|
| Crosshair Studio | Layered editor: up to 8 layers of Arms (cross / T / X / single lines), Dot, Ring, Box (incl. corner brackets), Image (PNG). Per-layer color, opacity, offset, rotation, outline. Live in-game preview, undo/redo. |
| Library | Bundled local presets (~30 original designs), user crosshairs, favorites, search/filter by tag, one-click apply. |
| Sharing | Text share code (`LC2:…`), JSON export/import (`*.lcx.json`, images embedded), local automatic backups. No cloud, accounts or server. |
| Game profiles | Executable → crosshair + display colors + NVIDIA FPS cap + FPS overlay override + visibility binding override. Event-driven foreground detection, automatic switch, restore on leave. |
| Default behavior | A default crosshair shows everywhere else; option "Show only in configured games". |
| Game suggestions | Built-in local list of ~50 known game executables; discreet tray notification "Create a profile for X?". |
| Multi-monitor | Monitor choice, robust to hot-plug, DPI changes and resolution changes; fallback to primary. |
| Hotkeys | Keep all v1.9 behavior (keyboard + mouse bindings, modifiers, Toggle / Hold to hide / Hold to show). Registration errors are shown, never silent. |
| Languages | English, Italian, Spanish, Simplified Chinese, Russian, Japanese. |
| Kept, frozen | Display colors (gamma/contrast/brightness/vibrance, NVIDIA Digital Vibrance, AMD color) — now per game profile. NVIDIA per-app FPS cap — now per game profile. FPS overlay reduced to FPS + frametime + 1% low. |

### Out (do not build)
Dynamic "smart" contrast / screen analysis · cloud sync, community library, rankings · Performance Monitor 2.0, temperatures, CPU/GPU/RAM/VRAM, graphs, session recording · RGB / pulse / animations / transitions · different crosshairs per monitor · a "Gaming mode" toggle (minimal footprint is the default) · light theme.

### Removed from v1.9
Frame Cap Assistant · FPS overlay "Detailed" mode, frametime graphs, frame-pacing diagnostics, frame-generation heuristics, RTSS fallback · G-Sync / FreeSync and GPU color-management read-only status labels · per-profile direct-select hotkey · in-process WPF settings window.

## 3. Architecture

```
LightCrosshair.exe  (Core, folder LightCrosshair/)          always running, tray icon
  overlay window (per-pixel alpha), renderer, hotkeys + input hooks,
  foreground/game detection, store (single writer), migration, backups,
  display colors, NVIDIA cap, minimal FPS overlay (loaded only when enabled),
  named-pipe server
        ⇅  named pipe "LightCrosshair.v2.<session>"  ·  NDJSON  ·  LightCrosshair.Contracts
LightCrosshair.UI.exe  (UI, folder LightCrosshair.UI/)       runs only while the window is open
  WPF + MVVM, design system, Studio, Library, Games, Hotkeys, Settings, 6 languages
```

Decisions:
- **C# on .NET 10 LTS** for every project (.NET 8 support ended 2026-11-10). Framework-dependent, ReadyToRun, single-file per exe, x64 and ARM64. No Rust, no WinUI 3.
- **Core keeps the `LightCrosshair/` folder and the `LightCrosshair.exe` name**, so Start Menu, autostart, installer and package managers keep working. Core keeps WinForms for its overlay window and tray, and must not reference WPF.
- **Overlay rendering**: GDI+ into a premultiplied 32-bit BGRA bitmap pushed with `UpdateLayeredWindow` (per-pixel alpha). Redraw only when the design, position, monitor or visibility changes. No timers when static. Replaces the v1 `TransparencyKey` color-key.
- **One renderer**: the UI never draws crosshairs itself; it asks Core to `render` PNGs, so previews equal the in-game result.
- **Core is the single writer** of `%AppData%\LightCrosshair\v2\store.json`. The UI only sends requests and receives snapshots.
- **Foreground detection**: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, no process polling. Screen-recording detection may keep a slow poll (≥ 2 s) only while `HideDuringScreenRecording` is on.
- **UI lifecycle**: Core starts `LightCrosshair.UI.exe` on the OpenSettings hotkey / tray click; closing the window ends the UI process. Starting the UI directly starts Core first if it isn't running.
- **Allowed new dependencies**: `CommunityToolkit.Mvvm` in the UI. Nothing else without writing the reason in the handoff file.

## 4. Performance budgets

Phase 0 measures v1.9 first ([BASELINE.md](BASELINE.md)). v2 targets, to be confirmed against the baseline:

| Metric (Release build, crosshair static, FPS overlay off, UI closed) | Target |
|---|---|
| Core private memory after 10 min idle | ≤ 40 MB |
| Core average CPU over 60 s | ≤ 0.1 % of one core |
| Overlay redraws per second while static | 0 |
| Core private memory after opening and closing the UI 5 times | within +3 MB of before |
| Foreground change → game profile applied | ≤ 50 ms |
| UI cold start to interactive | ≤ 1.5 s |
| Studio slider drag → in-game preview updated | ≤ 50 ms |

## 5. Workstreams and branches

| Branch | Owner | Folder (worktree) | Owns |
|---|---|---|---|
| `v2` | Claude (review, merge) | `LightCrosshair/` (main checkout) | `docs/v2/*.md` plan files, `LightCrosshair.Contracts/`, `LightCrosshair.sln` |
| `v2-core` | Codex | `../LightCrosshair-core` | `LightCrosshair/`, `LightCrosshair.Tests/`, `scripts/`, `setup/`, `.github/`, `README.md`, `CHANGELOG.md`, `docs/SETTINGS.md`, `docs/v2/BASELINE.md`, `docs/v2/HANDOFF-core.md` |
| `v2-ui` | Cursor | `../LightCrosshair-ui` | `LightCrosshair.UI/`, `LightCrosshair.UI.Tests/`, `docs/v2/HANDOFF-ui.md` |

Rules for both agents:
- Touch only the paths you own. Read anything.
- Do not edit `LightCrosshair.Contracts/` or `LightCrosshair.sln`. Request contract changes in the "Contract change requests" section of your handoff file ([CONTRACT.md §8](CONTRACT.md#8-change-rules)) and work around them meanwhile.
- Do not merge, rebase onto the other branch, or push. Commit in small steps on your own branch with the user's existing git identity; no AI co-author trailers or "generated with" lines.
- Before every commit: build and test your projects with zero errors.
- Do not commit `graphify-out/`.
- Keep `docs/v2/HANDOFF-<core|ui>.md` current: done, deviations from the plan and why, how to run and test, open issues.

Merge order (Claude): `v2-core` → `v2`, then `v2-ui` → `v2`, add UI projects to the solution, integration test, review, then `v2` → `main` as 2.0.0.

## 6. Definition of done
- Every item in [TODO.md](TODO.md) checked or explicitly deferred in a handoff file with a reason.
- Release build of the whole solution, all tests green, CI updated to .NET 10.
- Upgrading from 1.9 keeps every user crosshair, color, hotkey and process binding (migration tests), and v1 files are left untouched so 1.9 can still be reinstalled.
- Budgets in §4 met or the gap explained with measurements.
- Every user-visible string localized in all 6 languages.
