You are continuing as the Core engineer for LightCrosshair 2.0. Lorenzo tested in game; this round fixes what he found. Work autonomously, committing as you go.

Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-core`, branch `v2-core`, fast-forwarded to `v2`. Never switch branches, merge, rebase or push. Same ownership as before (Core, Core tests, scripts, setup, CI, README/CHANGELOG/SETTINGS, `docs/perf/`, `HANDOFF-core.md`).

Read `docs/v2/REVIEW-3.md` (findings G-1…G-6 and decisions 1–5), the updated `docs/v2/CONTRACT.md` §7, and the contract diff (`RuntimeState.DisplayColorsError`).

Your part:
1. **Presets as crosshairs** (decision 1): resolve ids against user crosshairs, then presets, everywhere (validation of `DefaultCrosshairId` and `GameProfile.CrosshairId`, `DeleteCrosshair` in-use check, active crosshair, migration untouched). Presets need stable ids that never change between versions; test that.
2. **Cycling** (decision 3): favorites when ≥ 2, else user crosshairs then presets in Library order; persist into the game profile when a configured game is foreground, else `DefaultCrosshairId`; emit `snapshotChanged`. Tests for each branch, including one user crosshair + presets.
3. **UI launch and render cost** (decision 4, Core side): start `LightCrosshair.UI.exe` with below-normal priority and low I/O priority when a game profile or any fullscreen-sized foreground window is active; if the UI process is parked (see the UI prompt) and connected, `OpenSettings` just sends `focusRequested` (already the case — verify it stays instant). Run `render` on a single below-normal-priority worker with a queue; drop queued renders when the UI disconnects. Measure: UI open → first frame and Core CPU during the open, before/after, with a running 3D test window; record in the handoff.
4. **Display colors** (G-6, decision 5): reproduce the AMD `Color apply failed` on this machine (RX 6950 XT): log the vendor error code/message once, set `RuntimeState.DisplayColorsError`, clear it on success or when leaving the game. Fix the AMD path if the cause is ours; if the driver refuses, report `unsupported` instead of failing. Remove message-less log lines (`AmdAdlColorManager` repeated with no text): every log line states what happened.
5. **G-3 check**: with a borderless 3D test window in the foreground (your own minimal D3D/WPF/WinForms full-screen-sized window is fine), verify the overlay stays above it after focus changes and that Alt+X toggles it. Add the probe to `scripts/perf/`.
6. Update README/CHANGELOG/SETTINGS for the changed behavior (click to use, cycling, presets as defaults).

Every commit: Release build of the solution with 0 warnings/errors, all tests green. Repository identity, no AI attribution. Add a "Review 3" section to `docs/v2/HANDOFF-core.md`: one line per item with how it was verified and the before/after numbers.
