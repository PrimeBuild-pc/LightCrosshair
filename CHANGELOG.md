# Changelog

## v2.0.0 — Unreleased

- Split the event-driven WinForms Core from the separate settings application on .NET 10.
- Add layered per-pixel alpha crosshairs, 30 original presets, favorites, local share codes and image-aware JSON export.
- Add executable-based game contexts with visibility, colors and optional NVIDIA FPS cap overrides.
- Preserve v1 files during migration and add atomic v2 storage, backups and crash recovery for display colors.
- Keep FPS, frametime and 1% low telemetry on demand; remove detailed diagnostics, frame-cap guidance, RTSS and frame-generation heuristics.
- Localize Core tray and notification strings in six languages.
- Final UI integration, GPU/ARM64 hardware checks and release validation remain required before distribution.

## v1.9.0 — 2026-11-11

- Added crosshair visibility bindings for Middle click, Mouse4/XButton1, Mouse5/XButton2, Wheel Up, Wheel Down, and Right click.
- Added Ctrl, Alt, Shift, and Win combinations for keyboard and mouse bindings.
- Added Toggle, Hold to hide, and Hold to show; wheel bindings only support Toggle.
- Added press-to-capture settings, shortcut conflict detection, and profile persistence.
- Preserved existing keyboard settings and profiles through backward-compatible migration.
- Added read-only input hooks on a dedicated thread, lost-release recovery, automatic hook recovery, and shutdown cleanup.

Input is always passed to other applications. No input is injected or modified. Users should check their game's anti-cheat rules.
