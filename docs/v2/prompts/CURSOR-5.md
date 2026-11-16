You are continuing as the UI engineer for LightCrosshair 2.0. Lorenzo tested in game; this round fixes what he found. Work autonomously, committing as you go.

Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-ui`, branch `v2-ui`, fast-forwarded to `v2`. Never switch branches, merge, rebase or push. Same ownership as before (`LightCrosshair.UI/`, `LightCrosshair.UI.Tests/`, `docs/images/`, `HANDOFF-ui.md`).

Read `docs/v2/REVIEW-3.md` (findings G-1…G-6 and decisions 1–5), the updated `docs/v2/CONTRACT.md` §7, and the contract diff (`RuntimeState.DisplayColorsError`). The Core side (preset ids accepted everywhere, persisted cycling, below-normal launch) is being done in parallel; keep the fake core in step with the contract so you can test.

Your part:
1. **Click a card = use it** (G-2, decision 2): single click on any card (user crosshair or preset) sets `DefaultCrosshairId` to its id; the active card is clearly marked (accent border + "In use" label). Each card gets an Edit button (and double-click) that opens it in Studio; presets open as "Edit a copy". In the Games page crosshair picker, presets are selectable too.
2. **Instant open** (G-4, decision 4, UI side):
   - Paint the window first: no blocking work before the first frame; snapshot, presets and thumbnails load after it, with skeleton cards. Thumbnails render only for visible cards.
   - **Parking**: closing the window (X, Alt+L via `closeRequested`) hides it, trims the working set (`SetProcessWorkingSetSize(-1,-1)`), drops thumbnail caches and stays connected; `focusRequested` shows it again instantly. Exit after 5 minutes parked, on `quit`, or on the Exit command. The pipe connection must stay alive while parked; Core must not count a parked UI as an open window for previews (end live preview on park).
   - Start at the priority Core gave you; raise to normal when the window is activated and no game is foreground; lower again on park.
   - Measure on this machine: cold open → first frame, parked open → first frame, private and working-set memory while parked. Targets ≤ 300 ms / ≤ 50 ms. Record in the handoff.
3. **Window placement** (G-5): first ever open = centered on the monitor with the mouse, 1180×740 (smaller if it doesn't fit), never maximized. Later opens restore the last normal bounds and the maximized state only if the user maximized it last time; clamp to visible monitors.
4. **Display color errors** (G-6): show `DisplayColorsError` inline on the game profile's Display colors section (localized, 6 languages), with the reason in plain words.
5. Regenerate the README screenshots if Library or Games changed visibly.

Every commit: Release build of the solution with 0 warnings/errors, all tests green. Repository identity; no `Co-authored-by` or any AI attribution. Add a "Review 4" section to `docs/v2/HANDOFF-ui.md`: one line per item with how it was verified and the measured numbers.
