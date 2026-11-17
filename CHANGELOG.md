# Changelog

## v2.0.0 — Unreleased

### Added

- Crosshair Studio with up to eight editable Arms, Dot, Ring, Box or PNG Image layers, fills for rings/boxes, outlines, opacity, rotation and position controls.
- Shared editor/overlay rendering, live in-game preview, undo/redo and layer reordering.
- A local library with 30 original presets, favorites, search and tag filters.
- Direct gallery preset use with a named saved copy, and saved-card selection for the active game or default.
- Monitor model names with an adapter-description fallback, and explicit mouse-button/wheel choices beside keyboard capture.
- `LC2:` vector share codes and `.lcx.json` import/export with embedded images.
- Executable-based game profiles for crosshairs, display colors, visibility bindings and FPS overlay overrides, with local game suggestions.
- Independent While held visibility binding, with per-game inherit/off/own policies and click pass-through.
- A dedicated FPS overlay page and sidebar switch, plus an optional bounded two-second frametime graph (off by default, at most 20 redraws/s).
- Global display colors and live slider previews in Settings and Games; game colors take precedence over globals.
- Show/hide FPS overlay hotkey (Alt+F; left unbound when a migrated 1.9 binding already uses it) and an optional separate frametime graph color.
- Settings → Behavior → When I close the window: keep running in the tray (default) or exit LightCrosshair.
- A v2 data store with atomic saves, automatic backups, corrupt-file recovery and a display-color recovery journal.

### Changed

- Move settings into a separate WPF app; the resident WinForms Core remains running when settings closes.
- Target .NET 10 Desktop Runtime, with x64 and ARM64 ZIP packages and an x64 installer.
- Render the overlay with per-pixel alpha, updating only when its appearance or placement changes.
- Keep keyboard/mouse visibility bindings and modifiers, with capture suspension and visible registration/conflict errors.
- Keep AMD color support, NVIDIA Digital Vibrance and per-app FPS caps, with global and game color settings; preserve disabled saved color values.
- Keep the optional FPS overlay focused on FPS, frametime, 1% low and the optional frametime graph; capture runs only while enabled.
- Provide Studio, Library, Games, Hotkeys, FPS overlay and Settings in English, Italian, Spanish, Simplified Chinese, Russian and Japanese.
- Bound transient rendering allocations so repeated Library sessions reach a stable Core memory footprint.
- Cycle favorites when at least two exist, otherwise all saved crosshairs in Library order; persist the selection into the active game profile or default.
- Decode DXGI/D3D9 present events without requiring registered ETW payload decoders, include MPO presents, track swapchains independently and retry failed capture sessions. Show a localized reason after two seconds without frames and stop native sessions when disabled.
- Launch settings below normal CPU priority with low I/O priority during games/fullscreen foreground windows; queue IPC thumbnails on one below-normal worker and discard obsolete queued work.
- Focus a parked connected settings process instead of closing it or launching a replacement.
- Each binding is one control: a capture field with a ▾ menu of mouse buttons, on every hotkey row and in Games. Game overrides are Use global / Off in this game / Custom and save immediately.
- Read mouse bindings from raw input instead of a low-level mouse hook, and let a hold match while extra modifiers are held (sprint/crouch while aiming).
- Switch display colors in place: slider previews no longer flash the previous colors, the released value stays, saved colors apply immediately while the game runs, and colors are re-applied on every foreground change.
- Draw the frametime graph as a continuous line: present events are flushed every 50 ms instead of arriving in one-second batches.
- Draw every crosshair shape exactly symmetric (even-width arms were one pixel closer on the right/bottom; small dots, rings and antialiased edges were lopsided). Smooth edges now rounds arm ends and outline corners.
- The Library pencil opens that crosshair in Studio; color swatches are high-contrast crosshair colors; the zoomed Studio preview opens centered.
- Confirmations and errors use the in-app dialog (keyboard accessible, localized); no Windows message boxes remain.
- Apply and recover AMD gamma through named display contexts, preserving each monitor's original ramp alongside ADL values. Report driver failures inline and keep the recovery journal until restoration succeeds.

### Removed

- Frame Cap Assistant, Detailed FPS mode, diagnostic graphs and frame-pacing analysis.
- RTSS fallback, frame-generation heuristics and G-Sync/FreeSync/color-management status panels.
- Per-profile direct-select hotkeys and the in-process settings window.

### Upgrade notes

- Exit 1.x before launching 2.0. Supported v1 profiles, active working state, preferences, hotkeys and process associations migrate automatically; original v1 files are never modified.
- V1 visibility bindings in Hold mode migrate to While held while Alt+X remains the visibility toggle. Global v1 display colors migrate when no target process was configured.
- An active v1 crosshair switched off migrates to global visibility off, keeping all editable layers visible. Disabled saved display-color values remain available.
- New data lives in `%AppData%\LightCrosshair\v2`; back up the entire folder to keep imported PNG images with the store.
- Pre-existing v1 NVIDIA driver caps are left untouched. Caps explicitly saved through 2.0 are managed on save/delete.
- NVIDIA Digital Vibrance and FPS caps are not yet verified on NVIDIA hardware. See the README for platform and overlay limitations.

## v1.9.0 — 2026-11-11

- Added crosshair visibility bindings for Middle click, Mouse4/XButton1, Mouse5/XButton2, Wheel Up, Wheel Down, and Right click.
- Added Ctrl, Alt, Shift, and Win combinations for keyboard and mouse bindings.
- Added Toggle, Hold to hide, and Hold to show; wheel bindings only support Toggle.
- Added press-to-capture settings, shortcut conflict detection, and profile persistence.
- Preserved existing keyboard settings and profiles through backward-compatible migration.
- Added read-only input hooks on a dedicated thread, lost-release recovery, automatic hook recovery, and shutdown cleanup.

Input is always passed to other applications. No input is injected or modified. Users should check their game's anti-cheat rules.
