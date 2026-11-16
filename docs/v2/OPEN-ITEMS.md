# LightCrosshair 2.0 — Open items before release

From Lorenzo's in-game test of build `cb1059e` (2026-11-16, Ryzen 7 5800X3D, RX 6950 XT, Fortnite).
Everything else in [TODO.md](TODO.md) and [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) is done; these are the only blockers left.

## Confirmed working in that build
- Display colors (game profile) are applied when the game starts on AMD.

## To fix

### 1. Hotkey for the FPS overlay
- Add a "Show/hide FPS overlay" hotkey, like the crosshair one (Hotkeys page, same capture control).
- Contract: `HotkeySettings.ToggleFpsOverlay` (InputBinding). Default to decide (proposal: unbound, or Alt+F if it doesn't conflict).
- It toggles the same setting as the sidebar "Show FPS overlay" switch.

### 2. Separate color for the frametime graph
- Default: the graph uses the text color.
- Option: "Use a different color for the graph" + color field.
- Contract: `FpsOverlaySettings.GraphColor` (`string?`, null = same as text).

### 3. Hide while aiming doesn't work in game
- Setup: While held → Own binding → Right click → Hold to hide. In Fortnite, holding right mouse does not hide the crosshair.
- Investigate with evidence: does the low-level mouse hook see the right button while the game has focus (raw input, EAC)? Does the binding reach the state machine? Does the game-profile override resolve correctly? Is the overlay redrawn on press/release?
- Verify in a real game, not only in tests.

### 4. "While held" setup is confusing
- The row shows a dropdown ("Own binding"), a capture field ("Right click") **and** a second "Right click" dropdown: two controls for the same thing.
- Redesign to one binding control that accepts both a key/mouse capture and a mouse-button choice (for example a capture field with a ▾ that lists mouse buttons), plus the mode (Hold to hide / Hold to show).
- In Games, the override reads as three clear choices: Use global · Off in this game · Custom (then the same single control).
- Apply the same single control to every hotkey row for consistency.

### 5. All dialogs in the app style
- No default Windows message boxes anywhere: confirmations, errors, warnings, unsaved changes, import results, etc. use an in-app dialog styled with the design tokens (DESIGN.md), keyboard accessible, localized in 6 languages.
- Check Core too (tray-related errors); Windows tray notifications can stay native.

### 6. Close button behavior setting
- Settings → Behavior: "When I close the window" → **Keep running in the tray** (default, same as Alt+L: the window parks, overlays stay) / **Exit LightCrosshair** (closes UI and Core, overlays disappear).
- Contract: `AppSettings.CloseButtonExits` (bool, default false), or an enum.
- Tray → Exit keeps exiting everything.

### 7. Display colors: live preview and immediate apply are broken
- While dragging a slider (e.g. vibrance) the screen flickers between the new value and the old one; after releasing, the value is **not** kept.
- Saved values only take effect after restarting the game. In 1.9 changes were applied in real time.
- Expected: smooth live preview while dragging (no flicker), the released value stays applied, and saving applies it immediately in the current context (game in foreground or global), without restarting the game.
- Likely areas: `previewDisplayColors` ending/restoring between throttled updates, the recovery journal capture racing with the preview, apply only on foreground change. Compare with how 1.9 applied changes from the settings window.

## After the fixes
1. Lorenzo retests these 7 items in game.
2. Rebuild packages and installer, fill SHA256 in README.
3. Align `main` with `origin/main`, PR `v2` → `main`, tag `v2.0.0`, GitHub release, winget/choco.
