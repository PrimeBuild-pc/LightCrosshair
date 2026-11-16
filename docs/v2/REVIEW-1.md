# LightCrosshair 2.0 — Review 1

Date: 2026-11-16. Reviewed `v2-core` (e575935) and `v2-ui` (246604c), merged into `v2`.

## What was verified
- Release build of the whole solution: 0 warnings, 0 errors. Tests: 215 Core + 17 UI, all green.
- Real Core + real UI, published together (x64), fresh data root:
  - Handshake, snapshot, Studio render and live preview (overlay drawn on screen with per-pixel alpha), save persisted to store.json, Library thumbnails from Core, 30 presets, Settings, runtime language switch (ru, ja) driven by `snapshotChanged`.
  - UI close → Core alive, WPF never loaded in Core, CPU 0.000 % over 30 s idle, `quit` exits cleanly.
- Migration against a real v1.9 user profile set (copy, originals untouched).

## Contract changes accepted (now in `LightCrosshair.Contracts`, ProtocolVersion unchanged — all additive)
1. `CrosshairLayer.FillColor` (Box and Ring; null = unfilled).
2. `GameProfile.DisplayColorsEnabled` (default true; values kept while false).
3. `IpcMethods.SetInputCapture` + `InputCaptureRequest` (suspend all hotkey/hook actions while the UI captures a binding; reset on disconnect and after 60 s without renewal).

## Core findings (Codex)
| Id | Severity | Finding | Required fix |
|---|---|---|---|
| C-1 | **Critical** | Migration aborts on real v1.9 data. `current_state.json` stores the working copy with `"Id": "working::<guid>"`; `Validation.Id` rejects `:` inside `LegacyMigration.Convert`, the whole migration throws, Core exits. Every upgrading user with a current state is affected. | Never reject legacy ids: map any id that fails `Validation.Id` to a new GUID and keep an old→new map so `SourceProfileId`, `LastProfileId` and game-profile links still resolve. One bad profile must be skipped and logged, never abort the migration. Add a regression fixture with exactly this shape (`default-immutable` profile, `working::<guid>` current state with `SourceProfileId`, prefs `LastProfileId`). |
| C-2 | High | When loading/migration fails Core exits silently; the log only says `Load store: CoreException` (no message). | Start with in-memory defaults (do not write store.json, so migration retries next launch), show one localized tray notification, log exception type + message + stack. A user must never be left with nothing running and no explanation. |
| C-3 | Medium | Core private memory grows per UI session: 14.2 MB at start → 19.8 MB after one UI session → 29.4 MB after ~6 sessions plus a few probe connections. Matches your own +5.9 MB finding. | Find the retention with `dotnet-gcdump` / `dotnet-counters` across 20 open/close cycles. Suspects: per-connection buffers and channels, `JsonElement` payload retention, PNG encoding streams, GDI+ objects in `render`, undisposed CTS in `PipeServer`. Target: PLAN budget (+3 MB after 5 cycles) and a flat trend from cycle 5 to 20. Record before/after in the handoff. |
| C-4 | Low | The test suite writes to the real `%AppData%\LightCrosshair\v2\logs\core.log`. | Tests must never touch the real profile: route every log/store path through a per-test data root. Add a guard test. |
| C-5 | Low | Perf probe window messages (0x8064/0x8065/0x8069) ship in the production window procedure. | Compile them only in Debug or only when `LIGHTCROSSHAIR_TEST_INSTANCE` is set. |
| C-6 | Low | All 30 presets render in the same cyan. | Give presets a sensible spread of high-visibility colors (white, green, yellow, cyan, magenta, red), some with outlines, so the Library is useful at a glance. |
| C-7 | — | Implement the three accepted contract changes: `FillColor` in renderer, validation, share code, migration (replace the nested-box workaround); `DisplayColorsEnabled` in migration and game rules; `setInputCapture` handling in input actions. | |

## UI findings (Cursor)
| Id | Severity | Finding | Required fix |
|---|---|---|---|
| U-1 | High | Library cards are clipped on the right and bottom (the hover border shows the real card is wider than what is drawn). Visible in every screenshot. | Fix the wrap panel measure/arrange so every card renders whole with consistent 12–16 px gaps at any window width. |
| U-2 | High | Studio preview: the selected background is drawn as a 256 px square floating on a checkerboard that fills the whole canvas. | The chosen background (dark/mid/light/checker/custom) fills the whole preview area; render with a transparent background and center it; checkerboard only for "Checker"; default zoom 2×. |
| U-3 | Medium | Arm toggles row in the inspector is clipped: the 4th label (Left / Лево) is cut off in every language. | 2×2 grid, or wrap. Check every inspector row at 1000 px window width in Russian. |
| U-4 | Medium | Accessibility: list and combo items expose type names to screen readers (`LightCrosshair.UI.ViewModels.LayerRow`, `…LanguageOpt`). Automation tools can't tell options apart. | Set `AutomationProperties.Name` (or `ToString`) for items in every ItemsControl: layers, languages, monitors, crosshair pickers, processes, FPS mode, binding modes, library cards. Add a test that walks the view models' item types and asserts a non-type-name display text. |
| U-5 | Medium | Sidebar items are centered; DESIGN asks for left-aligned icon + label with the 2 px accent bar, and the app mark + name at the top of the sidebar. No window icon. | Follow DESIGN §3. `LightCrosshair/assets/icon.ico` exists in the repo: use it for the window and taskbar. |
| U-6 | Medium | The "LightCrosshair is not running" message and Retry button are drawn on top of the page content with no backdrop; controls behind stay visible and confusing. | Proper centered state panel over a `Bg` backdrop that hides/blocks the page while disconnected. |
| U-7 | Low | Import dialog title uses the `File_Export` string. | Use an import string in all 6 languages. |
| U-8 | Low | Library search box has no placeholder or search glyph. | Add both. |
| U-9 | Low | Started from Git Bash the UI never connects (stays "Not connected" with Core running); started from PowerShell, Explorer or Core it connects. Not a user path. | Investigate only if cheap (environment-dependent pipe/session lookup?); otherwise document. |
| U-10 | Low | UI private memory 140–210 MB. | Freeze bitmaps, decode thumbnails at display size, bound the thumbnail cache. Record the new number; no hard budget. |
| U-11 | — | Implement the accepted contract changes: Fill color for Box/Ring in the inspector; Display colors switch keeps values (`DisplayColorsEnabled`); call `setInputCapture` true/false around hotkey capture (and on cancel/page leave). | |
| U-12 | — | 17 tests for ~6.5k lines is thin. | Add view-model tests: Studio undo coalescing and unsaved guard, layer limit, Games save/enable/override, hotkey conflicts and wheel rule, settings debounce. |
| U-13 | Process | Commits carried `Co-authored-by: Cursor`. They were rewritten before merge. | No AI attribution in commits. |

## Next round
Both branches have been fast-forwarded to the merged `v2`, so each agent now has the real Core and UI side by side and can test end-to-end. Same ownership rules as PLAN §5. Update your handoff file with a "Review 1" section listing each finding id and what you did.
