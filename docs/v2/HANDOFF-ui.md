# LightCrosshair.UI handoff

Branch `v2-ui`. The settings app is `LightCrosshair.UI` (`LightCrosshair.UI.exe`). It talks to Core only through `LightCrosshair.Contracts`. This branch now contains the v2 Core, so the app can be published beside `LightCrosshair.exe` or run with `--fake-core`. The named-pipe client is implemented against CONTRACT.md and covered by tests.

## Status per milestone

| Milestone | Status |
| --- | --- |
| U0 Foundation | Done. WPF `net10.0-windows` app, xUnit test project, theme dictionaries, six-language resx with a runtime switch. |
| U1 Core client | Done. `PipeCoreClient` plus `FakeCoreClient` behind `ICoreClient`. CLI `--page`, `--suggest`, `--fake-core`, `--screenshots`. Single instance. |
| U2 Shell | Done. Custom chrome, sidebar, visibility switch, connection status, placement restore. |
| U3 Studio | Done. Rendered preview, layers, inspector, undo, live preview, share codes, PNG import. |
| U4 Library | Done. Virtualized card grid, thumbnail cache, tags, search, card actions. |
| U5 Games | Done. Profiles, suggestion banner, process picker, display colors, NVIDIA cap, visibility override. |
| U6 Hotkeys | Done. Capture, modes, wheel-forces-toggle, conflict and registration errors. |
| U7 Settings | Done. Display, behavior, FPS overlay, language, GitHub update check, about, exit. |
| U8 Finish | Done. Automation names, view-model tests, measurements, screenshots, this file. |

Nothing in U0–U8 is deferred.

## Deviations from PLAN / DESIGN / CONTRACT

- Settings, hotkeys, and the sidebar visibility switch save immediately. Settings changes are debounced by 200 ms. Studio and the game detail panel keep a working copy until Save. The game enable switch writes the stored profile only and does not flush unsaved detail edits.
- The Games page has a Save button. DESIGN draws the detail fields without one; the profile is still a working copy, so the button is how it gets committed.
- A null crosshair monitor is shown as the primary monitor and stored as null (CONTRACT: null means primary). A null FPS-overlay monitor is shown as "Same as the crosshair".
- Display-color sliders and the NVIDIA cap value stay hidden until that switch is on. Turning colors off sets `DisplayColorsEnabled` to false and keeps the stored values. Unsupported GPU features stay visible as disabled controls with the reason beside them.
- The fake renderer draws image layers as a colored square. It does not decode the PNG. The real client always calls `render`.
- Share-code encode/decode exists only inside the fake, so paste/copy works without Core. The pipe client calls `encodeShareCode` / `decodeShareCode`.
- Window placement is `%AppData%\LightCrosshair\v2\ui-placement.json`, or `ui-placement.json` inside `LIGHTCROSSHAIR_DATA_ROOT` when that variable is set. It is not part of `store.json`.
- The NVIDIA cap editor uses 30..480 and defaults to 144 when the switch is turned on. The contract does not document that range.
- The left mouse button is not a bindable trigger, matching v1.9. Wheel bindings are Toggle only.
- The layer list shows the topmost layer first (the last entry in `Layers`).
- Studio preview asks Core for a transparent 256 px render and draws it centered. The chosen background fills the whole preview; the checker brush is used only for Checker. Default zoom is Fit: the largest integer zoom whose render fits the preview, recomputed when the preview area changes. 1×, 2×, and 4× stay available. Zoom scales the bitmap with nearest-neighbor. The pixel grid appears only at 4×.
- The window and taskbar use `LightCrosshair/assets/icon.ico`, copied into the UI project. The sidebar mark draws the frame that matches the current DPI, with nearest-neighbor.
- "Not now" on a suggestion is remembered for this UI session only. "Never" calls `dismissSuggestion`.

## How to build, run, test, screenshot

```powershell
dotnet build LightCrosshair.UI/LightCrosshair.UI.csproj -c Release
dotnet test LightCrosshair.UI.Tests/LightCrosshair.UI.Tests.csproj -c Release
```

Release is framework-dependent, ReadyToRun, single-file, `win-x64` and `win-arm64`. Publish with:

```powershell
dotnet publish LightCrosshair.UI/LightCrosshair.UI.csproj -c Release -r win-x64
```

Run against the fake core:

```powershell
dotnet run --project LightCrosshair.UI/LightCrosshair.UI.csproj -c Release -- --fake-core
```

Other flags: `--page studio|library|games|hotkeys|settings`, `--suggest "Name|game.exe"`. A second instance exits. Close the window and the process exits. Core owns the tray.

Readme screenshots, committed at `docs/images/`:

```powershell
dotnet run --project LightCrosshair.UI/LightCrosshair.UI.csproj -c Release -- --fake-core --screenshots docs/images
```

That writes `ui-studio.png`, `ui-library.png`, `ui-games.png`, `ui-hotkeys.png`, and `ui-settings.png` at 1180×740 and exits. `--screenshots` loads a separate fake seed and leaves the default fake store unchanged.

Release build of `LightCrosshair.sln`: 0 warnings, 0 errors. Tests: 215 Core and 22 UI passed, 0 failed.

## Measurements

Fake core, Release exe, one warmup run discarded, then three cold starts. The clock stops when `MainWindowHandle` becomes non-zero. Private memory and working set are read 600 ms later.

| Run | To window | Private | Working set |
| --- | ---: | ---: | ---: |
| 1 | 536 ms | 116.4 MB | 158.8 MB |
| 2 | 577 ms | 117.1 MB | 159.2 MB |
| 3 | 581 ms | 117.1 MB | 159.2 MB |

UI cold start is under the 1.5 s budget. The private-memory number is the WPF process, not Core. PLAN's 40 MB budget applies to Core.

## Known issues and risks

- The pipe client is tested against a local stub server (hello first, out-of-order responses, timeout, oversized line). A published Core and UI in one folder, with `LIGHTCROSSHAIR_DATA_ROOT` pointed at a temp directory, connect and render Studio and Library.
- Library card menus are opened from code (right click, or the menu key / Shift+F10 on the selected card) so the virtualizing panel does not rebuild a menu during measure.
- Thumbnail automation names on tag chips do not refresh when the language changes. The visible chip text does.
- Screenshot runs still write `ui-placement.json` when the window closes. Set `LIGHTCROSSHAIR_DATA_ROOT` so that write stays out of the real profile.
- Fake image-layer previews are placeholders. A real PNG shows up only after Core renders it.

## Review 1

- U-1: Library cards use a 136×172 slot and an 8 px margin, with list-item padding removed, so every card is whole and the gap is 16 px. Checked on the fake and real Core library screenshots.
- U-2: The preview background fills the canvas, the render is transparent and centered, checker is only the Checker choice, and the default zoom is 2×. Checked on the fake Classic shot and the real Core Classic Cross shot.
- U-3: The four arm switches are a 2×2 grid. The real Core studio shot shows Top, Right, Bottom, and Left complete; that inspector is the same width at the 1000 px minimum.
- U-4: Layer, language, monitor, library card, and hotkey items implement `ToString`, and the profile, process, crosshair, language, and monitor containers set `AutomationProperties.Name`. `ItemTypesExposeAReadableName` asserts the view-model text is not a type name.
- U-5: Sidebar entries are left-aligned icon plus label, with `icon.ico` and the app name at the top. The same icon is the window and taskbar icon. Visible on every screenshot.
- U-6: The not-running message is a centered panel on a `Bg` backdrop that covers the page. The connected screenshots show the page with that panel hidden.
- U-7: Import uses `File_Import` in all six languages. The open-dialog title is the text before the filter.
- U-8: The library search box has a search glyph and the "Search crosshairs" placeholder. Visible on both library screenshots.
- U-9: Deferred. The client and Core both use `LightCrosshair.v2.{sessionId}` with `CurrentUserOnly`. A Git Bash launch that stays on "Not connected" is not a supported path; PowerShell and the published folder connect.
- U-10: Thumbnails decode at 96 px and the cache keeps the newest 48. Three cold starts of the Release UI measured 116.4, 117.1, and 117.1 MB private.
- U-11: Ring and Box have a fill color, the display-color switch keeps its values through `DisplayColorsEnabled`, and binding capture calls `setInputCapture` true, renews it every 30 s, and sends false on finish, Esc, page leave, and window deactivation. Covered by the fill, games, and input-capture tests.
- U-12: Added tests for the unsaved navigation guard, settings debounce, input capture, fill, and item names. Release `LightCrosshair.sln` builds with 0 warnings; 215 Core and 22 UI tests passed.
- U-13: `383c1db` was committed with the repository identity and has no `Co-authored-by` trailer.

## Review 2

- U-14: The preview scrollbar stays `Auto`, and the zoomed image is the scroll extent, centered while it fits. Against the real Core at 1180×900, 1× and 2× had no preview scrollbar and 4× had both. `PreviewScrollsOnlyWhenZoomExceedsTheArea` also checks a short window, where 2× shows only the vertical bar.
- U-15: The preview shows "All layers are hidden" when every layer is hidden, with the same string in all six languages. Confirmed on the real Core after hiding the only layer of Classic Cross.
- U-16: `ui-placement.json` is stored under `LIGHTCROSSHAIR_DATA_ROOT` when that variable is set. `PlacementFileFollowsTheDataRoot` writes a temp root and does not touch the roaming file. The real Core run saved the window rectangle in the temp root, and the roaming file's timestamp did not change.

## Review 3

- U-17: Fit is the default zoom. It is the largest integer zoom whose 256 px render fits the preview, and it is recomputed when the preview area changes. 1×, 2×, and 4× still pin the zoom. `FitStaysInsideThePreviewAtTheReviewSizes` opens the window at 1000×640, 1180×740, and maximized: Fit stays selected, the zoom matches the area, and the preview scrollbars stay off.
- U-18: The sidebar mark uses the hard-edged icon frame closest to 20 px at the current DPI (20, 24, 32, or 40) and draws it one bitmap pixel per device pixel with nearest-neighbor. `SidebarMarkUsesTheDpiFrameAndHardEdges` checks 100% and 150%: every pixel is transparent or the icon red.
- Readme shots: `docs/images/ui-studio.png`, `ui-library.png`, `ui-games.png`, `ui-hotkeys.png`, and `ui-settings.png` are 1180×740 captures from `--screenshots` against the separate fake seed. Studio shows the outlined cross, green dot, and ring at Fit; Library shows four crosshairs with two favorites above the presets; Games has Counter-Strike 2 selected and VALORANT, with no suggestion; Hotkeys adds Hold to show on Mouse 5; Settings shows the top of the page in English. Each file is under 50 KB. Release `LightCrosshair.sln`: 0 warnings, 230 Core tests and 28 UI tests passed.

## Review 4

- Presets: a click opens "Use this crosshair" on the card, with the name selected. Use saves a new id and `SourcePresetId`, then makes it the default, or that game's crosshair when the pick starts on Games. A preset that was already saved offers the existing copy or another one. Enter confirms and Esc cancels. Right-click is Use… and Edit a copy. `UsingAPresetSavesItAndASecondUseKeepsTheCopy` and `APresetChosenForAGameDoesNotChangeTheDefault`.
- Saved cards: a click makes that crosshair active immediately. The active card has an accent border and "In use". Edit and a double-click open Studio. The menu is Use, Edit, Duplicate, Favorite, Export, Delete. Enter uses it, F2 renames it, Delete asks first. `ClickingASavedCrosshairMakesItActiveAndRenameKeepsTheDesign`. Picking a saved crosshair on a game profile saves that profile. `ChoosingASavedCrosshairForAGameSavesThatProfile`.
- Mouse bindings: the capture field ignores the click that starts capture, then takes Middle, Right, Mouse4, Mouse5, WheelUp, and WheelDown. Each row also has a Mouse button menu; the wheel stays Toggle and other modifiers stay. The hint under the title is "Click a shortcut, then press a key or a mouse button" in all six languages. `CaptureIgnoresTheOpeningClickThenTakesMouseButtonsAndTheWheel` and `TheMouseMenuKeepsModifiersAndForcesTheWheelToToggle`. The published Hotkeys page shows Mouse 5, Hold to show, and the menu. A physical Mouse4 press was not sent.
- Monitors: every monitor picker shows "{FriendlyName} · Display {n}", with Primary on the primary. The settings shot shows "DELL S2721DGF · Display 1 · Primary". `MonitorLabelsAndColorErrorsUsePlainWords`.
- Display colors: `apply_failed`, `restore_failed`, and `unsupported` are a plain sentence on the game profile, in all six languages. Same test.
- Open: the window is shown before `StartAsync`, so the first frame is not waiting on the snapshot, presets, or thumbnails. Closing hides it, ends a live preview, drops the thumbnail cache, trims the working set with `SetProcessWorkingSetSize(-1,-1)`, and stays connected. It exits after 5 minutes, on `quit`, or on Exit. Focus shows it again. Priority is below normal while parked or while a game is foreground, and normal otherwise. `ClosingTheMainWindowParksAndExitsAfterTheTimer`, `ParkLowersPriorityUntilTheWindowIsActiveWithNoGame`.
- Timing, published win-x64, fake core, `LIGHTCROSSHAIR_TIMING=1`, one warmup discarded. Cold is `OnStartup` to the first `ContentRendered`. Parked open is the show after a close to the next render. Parked memory is the working set just after the trim. Runs: cold 531 / 562 / 578 ms, parked open 16 / 16 / 16 ms, parked working set 4.77 / 4.81 / 4.88 MB. Parked open is under 50 ms. Cold is over 300 ms: about 550 ms is WPF creating and laying out the window before that first frame. Process start to the same frame was 717 / 751 / 757 ms.
- Placement: the first open is 1180×740 centered on the monitor under the mouse, smaller if the work area is smaller, and never maximized. Later opens restore the normal bounds, clamp them onto a visible monitor, and maximize only if the window was left maximized on a monitor. `FirstOpenCentersOnTheMonitorUnderTheMouse`, `SavedBoundsAreClampedAndMaximizedOnlyOnAVisibleMonitor`.
- Readme shots were recaptured at 1180×740. Library shows the accent "In use" card, Hotkeys shows the mouse menu and the new hint, Games shows "Use a preset", Settings shows the monitor model. Each file is under 50 KB. The bottom row of content sits 24 px or more inside the frame. Release `LightCrosshair.sln`: 0 warnings, 230 Core tests and 40 UI tests passed.

## Contract change requests

None.
