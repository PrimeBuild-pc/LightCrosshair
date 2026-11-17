# LightCrosshair 2.0 — Open items before release

From Lorenzo's in-game test of build `cb1059e` (2026-11-16, Ryzen 7 5800X3D, RX 6950 XT, Fortnite).
Everything else in [TODO.md](TODO.md) and [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) is done; these are the only blockers left.

## Confirmed working in that build
- Display colors (game profile) are applied when the game starts on AMD.

## To fix

### 1. Hotkey for the FPS overlay
**Status (2026-11-17): fixed.** `HotkeySettings.ToggleFpsOverlay`, default Alt+F (left unbound when a migrated 1.9 binding already uses Alt+F), toggles and persists `Settings.FpsOverlay.Enabled`, the same setting as the sidebar switch; a game's FPS override still wins in that game. New "Show or hide FPS overlay" row on Hotkeys with the same control and conflict/registration errors. Verified: unit tests (default, conflict, toggle-only, migration), UI tests, and `scripts/perf/fps-hotkey-smoke.ps1` pressing a real Alt+F against the published Core (False → True → False). Lorenzo: press Alt+F in game.

- Add a "Show/hide FPS overlay" hotkey, like the crosshair one (Hotkeys page, same capture control).
- Contract: `HotkeySettings.ToggleFpsOverlay` (InputBinding). Default to decide (proposal: unbound, or Alt+F if it doesn't conflict).
- It toggles the same setting as the sidebar "Show FPS overlay" switch.

### 2. Separate color for the frametime graph
**Status: fixed.** `FpsOverlaySettings.GraphColor` (null = text color), validated like the other colors. FPS overlay page: "Use a different color for the graph" switch (shown with the graph) + color field. Verified: Core/UI tests and the published UI (switch on → store `graphColor` set). Lorenzo: check the graph color in game.

- Default: the graph uses the text color.
- Option: "Use a different color for the graph" + color field.
- Contract: `FpsOverlaySettings.GraphColor` (`string?`, null = same as text).

### 3. Hide while aiming doesn't work in game
**Status: fixed; needs Lorenzo's in-game check.** Evidence: (a) with the shipped build, a borderless DX12 window that reads the mouse through raw input with `RIDEV_NOLEGACY` (new `SwapchainProbe --raw-nolegacy`) hides/shows correctly for a global and a per-game Right click hold, with mouse movement, so the hook, binding resolution and redraw worked; (b) Lorenzo's saved store (read-only) has Fortnite `holdVisibility.key = ""`, i.e. **Off in this game**: the Games page showed "Own binding / Right click" only in its unsaved working copy, so Core never had the binding; (c) the shipped build ignores the hold while Shift is held (sprint): 40/40 samples shown. Fixes: Games overrides now save immediately (item 4); a hold matches while extra modifiers are held (toggles keep exact chords); mouse buttons and wheel are read from raw input (`RIDEV_INPUTSINK`, the stream games read) instead of a low-level mouse hook, so another app's hook or a game's raw-input mode cannot hide them. New build: hold-smoke passes (with and without raw input), and with Shift held 40/40 samples hidden. Lorenzo: in Fortnite set While held → Custom → Right click → Hold to hide (it saves at once) and aim, also while sprinting.

- Setup: While held → Own binding → Right click → Hold to hide. In Fortnite, holding right mouse does not hide the crosshair.
- Investigate with evidence: does the low-level mouse hook see the right button while the game has focus (raw input, EAC)? Does the binding reach the state machine? Does the game-profile override resolve correctly? Is the overlay redrawn on press/release?
- Verify in a real game, not only in tests.

### 4. "While held" setup is confusing
**Status: fixed.** One binding control everywhere: a capture field with a ▾ menu of mouse buttons (Alt+Down opens it; wheel entries only where toggles are allowed), plus the mode selector where it applies; the separate mouse dropdowns are gone. Games: Visibility shortcut and While held are three segments, Use global · Off in this game · Custom (with the global shortcut shown), and save immediately. Also fixed a capture bug: the field asked `CanExecute` with the wrong argument, which threw for typed commands. Verified: UI tests (menu keeps modifiers, wheel forces Toggle, immediate override saves with unrelated edits left unsaved) and the published UI via UI Automation (Custom → store `RightClick/holdToHide` without Save); screenshots in `docs/images/ui-hotkeys.png`, `ui-games.png`.

- The row shows a dropdown ("Own binding"), a capture field ("Right click") **and** a second "Right click" dropdown: two controls for the same thing.
- Redesign to one binding control that accepts both a key/mouse capture and a mouse-button choice (for example a capture field with a ▾ that lists mouse buttons), plus the mode (Hold to hide / Hold to show).
- In Games, the override reads as three clear choices: Use global · Off in this game · Custom (then the same single control).
- Apply the same single control to every hotkey row for consistency.

### 5. All dialogs in the app style
**Status: fixed.** The last Windows `MessageBox` (UI unhandled-error handler) now uses the in-app dialog. The dialog takes keyboard focus on the primary button, or on Cancel for destructive confirmations, keeps Tab inside, Esc cancels, text wraps, all strings localized. Core uses only tray balloons. A test fails if `MessageBox`/`TaskDialog` appears in Core or UI sources. Verified: UI tests (focus, Esc) and the published UI (Exit confirmation rendered in the app style with focus on Cancel).

- No default Windows message boxes anywhere: confirmations, errors, warnings, unsaved changes, import results, etc. use an in-app dialog styled with the design tokens (DESIGN.md), keyboard accessible, localized in 6 languages.
- Check Core too (tray-related errors); Windows tray notifications can stay native.

### 6. Close button behavior setting
**Status: fixed.** `AppSettings.CloseButtonExits` (default false). Settings → Behavior → "When I close the window": Keep running in the tray / Exit LightCrosshair. Tray → Exit and Settings → Exit still quit both. Verified in the published app: with Exit chosen, the window × ended both UI and Core; with the default, × parked the UI and Core kept running. Alt+L is unchanged.

- Settings → Behavior: "When I close the window" → **Keep running in the tray** (default, same as Alt+L: the window parks, overlays stay) / **Exit LightCrosshair** (closes UI and Core, overlays disappear).
- Contract: `AppSettings.CloseButtonExits` (bool, default false), or an enum.
- Tray → Exit keeps exiting everything.

### 7. Display colors: live preview and immediate apply are broken
**Status: fixed; needs Lorenzo's in-game check.** Cause: every color change ran a full transaction that first restored the original ramp/ADL values and re-captured them, so each preview step flashed the old colors, and the Games preview ended on release (reverting to the saved values). Fix: a live session writes new values straight over its kept baseline (restore only when no colors apply or NVIDIA vibrance presence changes); AMD puts the captured ramp back when gamma returns to 100; colors are re-applied on every foreground change; the Games preview stays after release until Save / another profile / page change / park; Save applies at once in the current context. Readback on the RX 6950 XT (`scripts/perf/color-live-smoke.ps1`, GDI Red[128] sampled every ~2 ms): shipped build 12–13 flashes back to the original during a 15-step drag and a profile saved while the game is foreground never reached the screen (ramp stayed 24124); new build 0 flashes, released value kept, saved game colors applied within 0.5 s (30705 → 26751), original restored exactly on exit. Through the published UI: Games gamma slider → ramp 32896 → 28660, still 28660 2 s later, not saved until Save; after Save outside the game the original 32896 returns. Lorenzo: drag vibrance/gamma with Fortnite running and Save without restarting the game.

- While dragging a slider (e.g. vibrance) the screen flickers between the new value and the old one; after releasing, the value is **not** kept.
- Saved values only take effect after restarting the game. In 1.9 changes were applied in real time.
- Expected: smooth live preview while dragging (no flicker), the released value stays applied, and saving applies it immediately in the current context (game in foreground or global), without restarting the game.
- Likely areas: `previewDisplayColors` ending/restoring between throttled updates, the recovery journal capture racing with the preview, apply only on foreground change. Compare with how 1.9 applied changes from the settings window.

## After the fixes

Build for the retest: `releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe` and `setup/Output/LightCrosshair-Setup-2.0.0.exe` (2026-11-17). Not checked here: Fortnite/EAC itself, NVIDIA hardware, HDR.

1. Lorenzo retests these 7 items in game.
2. Rebuild packages and installer, fill SHA256 in README.
3. Align `main` with `origin/main`, PR `v2` → `main`, tag `v2.0.0`, GitHub release, winget/choco.
