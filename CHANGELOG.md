# Changelog

## v1.9.0 — 2026-11-11

- Added crosshair visibility bindings for Middle click, Mouse4/XButton1, Mouse5/XButton2, Wheel Up, Wheel Down, and Right click.
- Added Ctrl, Alt, Shift, and Win combinations for keyboard and mouse bindings.
- Added Toggle, Hold to hide, and Hold to show; wheel bindings only support Toggle.
- Added press-to-capture settings, shortcut conflict detection, and profile persistence.
- Preserved existing keyboard settings and profiles through backward-compatible migration.
- Added read-only input hooks on a dedicated thread, lost-release recovery, automatic hook recovery, and shutdown cleanup.

Input is always passed to other applications. No input is injected or modified. Users should check their game's anti-cheat rules.
