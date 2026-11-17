You are continuing as the Core engineer for LightCrosshair 2.0. Your first round was reviewed and merged; this round fixes the review findings. Work autonomously until every Core item is done, committing as you go.

## Where you work
- Folder `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-core`, branch `v2-core`. It has been fast-forwarded to the merged `v2`: it now contains the real UI (`LightCrosshair.UI/`), the solution includes all five projects, and the contract has three new additive members. Never switch branches, merge, rebase or push.

## Read first
1. `docs/v2/REVIEW-1.md` — the review. Your items are C-1 … C-7. **C-1 and C-2 come first.**
2. `docs/v2/CONTRACT.md` (updated: `setInputCapture` rule in §3) and the diff of `LightCrosshair.Contracts/` since your last commit (`git diff e575935 -- LightCrosshair.Contracts`).
3. `docs/v2/PLAN.md` §4–5 (budgets, ownership — unchanged).

## Scope and rules
- Same ownership as before: `LightCrosshair/`, `LightCrosshair.Tests/`, `scripts/`, `setup/`, `.github/`, `README.md`, `CHANGELOG.md`, `docs/SETTINGS.md`, `docs/v2/HANDOFF-core.md`. Do not edit `LightCrosshair.UI*/`, `LightCrosshair.Contracts/`, `LightCrosshair.sln` or other `docs/v2/` files.
- C-1: the fixture must reproduce the real v1.9 shape that broke (`profiles.json` with id `default-immutable`; `current_state.json` with `"Id": "working::<32 hex>"` and `"SourceProfileId": "default-immutable"`; `prefs.json` with `"LastProfileId": "default-immutable"`). Assert the migrated store: one crosshair per profile, the working copy merged into its source, the default pointing to it, no exception. Then run the published Core against a copy of such files and confirm it starts.
- C-2: after the fix, simulate a load failure (corrupt/unreadable legacy file that defeats your recovery) and show Core keeps running with defaults, notifies once, and logs the message.
- C-3: measure with the real UI now available (`pwsh scripts/perf/measure.ps1` without `-Isolated`, closing any other LightCrosshair first). 20 cycles, before/after numbers in the handoff.
- You can now test end to end: publish Core and UI into one folder and use them together; use `LIGHTCROSSHAIR_DATA_ROOT` pointing to a temp folder whose parent holds the v1 fixture files. Never use the real `%AppData%\LightCrosshair`.
- Every commit: Release build of the whole solution with 0 warnings/errors, all tests green (Core and UI). Small commits, English imperative messages, repository git identity, no AI attribution of any kind.

## Done when
C-1 … C-7 are fixed or explicitly deferred with a reason in a new "Review 1" section of `docs/v2/HANDOFF-core.md` (one line per id: what changed, how it was verified), and `docs/v2/TODO.md` stays accurate.
