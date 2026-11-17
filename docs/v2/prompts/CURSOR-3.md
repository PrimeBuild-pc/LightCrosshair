You are continuing as the UI engineer for LightCrosshair 2.0. Review 2 left three small UI items. Work autonomously, committing as you go.

Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-ui`, branch `v2-ui`, already fast-forwarded to the merged `v2`. Never switch branches, merge, rebase or push.

Read `docs/v2/REVIEW-2.md`. Your items: **U-14** (preview scrollbar only when needed), **U-15** (hint when all layers are hidden, 6 languages), **U-16** (`ui-placement.json` honors `LIGHTCROSSHAIR_DATA_ROOT`). Same ownership and rules as before (PLAN.md §5): only `LightCrosshair.UI/`, `LightCrosshair.UI.Tests/`, `docs/v2/HANDOFF-ui.md`.

- Verify against the real Core (publish Core and UI into one temp folder, temporary `LIGHTCROSSHAIR_DATA_ROOT`), at 1×, 2× and 4× zoom, and with every layer hidden. Add a test for U-16.
- Every commit: Release build of the whole solution with 0 warnings/errors, all tests green. Repository git identity; no `Co-authored-by` or any AI attribution.

Done when the three items have a line in the "Review 2" section of `docs/v2/HANDOFF-ui.md` with what changed and how it was verified.
