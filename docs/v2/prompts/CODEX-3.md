You are continuing as the Core engineer for LightCrosshair 2.0. Review 2 left two Core items. Work autonomously, committing as you go.

Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-core`, branch `v2-core`, already fast-forwarded to the merged `v2`. Never switch branches, merge, rebase or push.

Read `docs/v2/REVIEW-2.md`. Your items: **C-8** (migration of v1 `EnableCustomCrosshair`) and **C-9** (memory soak with the Library page). Same ownership and rules as before (PLAN.md §5): only `LightCrosshair/`, `LightCrosshair.Tests/`, `scripts/`, `setup/`, `.github/`, `README.md`, `CHANGELOG.md`, `docs/SETTINGS.md`, `docs/v2/HANDOFF-core.md`.

- C-8: add a fixture where the active v1 working copy has `"EnableCustomCrosshair": false` and the saved profile has `true`; assert every migrated layer is visible and `Settings.CrosshairVisible` is false. Add the inverse case. Then run the published Core on a copy of that fixture and confirm the overlay shows once "Show crosshair" is turned on.
- C-9: 50 UI sessions, each opening the Library page (`LightCrosshair.UI.exe --page library`, wait until thumbnails load, close). Use a temporary `LIGHTCROSSHAIR_DATA_ROOT`; never the real `%AppData%\LightCrosshair`. Report the curve; fix the retention if it doesn't plateau.
- Every commit: Release build of the whole solution with 0 warnings/errors, all tests green. Repository git identity, no AI attribution.

Done when both items have a line in the "Review 2" section of `docs/v2/HANDOFF-core.md` with what changed and how it was verified.
