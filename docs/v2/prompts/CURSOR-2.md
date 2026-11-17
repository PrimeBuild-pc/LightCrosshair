You are continuing as the UI engineer for LightCrosshair 2.0. Your first round was reviewed and merged; this round fixes the review findings. Work autonomously until every UI item is done, committing as you go.

## Where you work
- Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-ui`, branch `v2-ui`. It has been fast-forwarded to the merged `v2`: it now contains the real v2 Core (`LightCrosshair/`), the solution includes all five projects, and the contract has three new additive members. Never switch branches, merge, rebase or push.

## Read first
1. `docs/v2/REVIEW-1.md` — the review. Your items are U-1 … U-13. Start with U-1, U-2, U-4.
2. `docs/v2/CONTRACT.md` (updated: `setInputCapture` rule in §3) and the diff of `LightCrosshair.Contracts/` since your last commit (`git diff 246604c -- LightCrosshair.Contracts`).
3. `docs/v2/DESIGN.md` §3 for the sidebar (U-5) and the Studio preview (U-2).

## Scope and rules
- Same ownership as before: `LightCrosshair.UI/`, `LightCrosshair.UI.Tests/`, `docs/v2/HANDOFF-ui.md`. Do not edit `LightCrosshair/`, `LightCrosshair.Tests/`, `LightCrosshair.Contracts/`, `LightCrosshair.sln`, CI, scripts, setup or other `docs/v2/` files. You may copy `LightCrosshair/assets/icon.ico` into the UI project for U-5.
- Test against the **real Core** now, not only `--fake-core`: publish Core and UI into one folder (`dotnet publish LightCrosshair/LightCrosshair.csproj -c Release -r win-x64 -o <dir>` and the same for the UI), set `LIGHTCROSSHAIR_DATA_ROOT` to a temp folder, start `LightCrosshair.exe`, then open the UI. Never use the real `%AppData%\LightCrosshair`. Keep `--fake-core` working for tests and screenshots.
- Regenerate the five screenshots after the fixes (`--fake-core --screenshots %TEMP%\lc-ui-shots`) and also capture Studio and Library against the real Core; check them yourself against REVIEW-1 before finishing.
- Every commit: Release build of the whole solution with 0 warnings/errors, all tests green (Core and UI). Small commits, English imperative messages, repository git identity. **No `Co-authored-by` or any AI attribution in commit messages**; if your environment adds it automatically, turn that setting off before committing.

## Done when
U-1 … U-13 are fixed or explicitly deferred with a reason in a new "Review 1" section of `docs/v2/HANDOFF-ui.md` (one line per id: what changed, how it was verified), and `docs/v2/TODO.md` stays accurate.
