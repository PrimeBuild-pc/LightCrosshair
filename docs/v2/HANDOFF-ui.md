# LightCrosshair.UI handoff

Branch `v2-ui`. The settings app is `LightCrosshair.UI` (`LightCrosshair.UI.exe`). It talks to Core only through `LightCrosshair.Contracts`. On this branch Core is still v1.9, so day-to-day running uses `--fake-core`. The named-pipe client is implemented against CONTRACT.md and covered by tests.

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
- Display-color sliders and the NVIDIA cap value stay hidden until that switch is on. Unsupported GPU features stay visible as disabled controls with the reason beside them.
- The fake renderer draws image layers as a colored square. It does not decode the PNG. The real client always calls `render`.
- Share-code encode/decode exists only inside the fake, so paste/copy works without Core. The pipe client calls `encodeShareCode` / `decodeShareCode`.
- Window placement is `%AppData%\LightCrosshair\v2\ui-placement.json`. It is not part of `store.json`.
- The NVIDIA cap editor uses 30..480 and defaults to 144 when the switch is turned on. The contract does not document that range.
- The left mouse button is not a bindable trigger, matching v1.9. Wheel bindings are Toggle only.
- The layer list shows the topmost layer first (the last entry in `Layers`).
- Studio preview is a 256 px render. Zoom scales that bitmap with nearest-neighbor. Checker is a transparent render over a checker brush. The pixel grid appears only at 4×.
- There is no application icon. The repo does not contain one.
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

Screenshots (not committed; write them outside the repo):

```powershell
dotnet run --project LightCrosshair.UI/LightCrosshair.UI.csproj -c Release -- --fake-core --screenshots $env:TEMP\lc-ui-shots
```

That writes `studio.png`, `library.png`, `games.png`, `hotkeys.png`, and `settings.png` at 1180×740 and exits. Studio is the Classic design (cross, dot, ring). The games page includes a sample suggestion banner.

Release build: 0 warnings, 0 errors. Tests: 17 passed, 0 failed, 0 warnings.

## Measurements

Fake core, Release exe, one warmup run discarded, then three cold starts. The clock stops when `MainWindowHandle` becomes non-zero. Private memory and working set are read 600 ms later.

| Run | To window | Private | Working set |
| --- | ---: | ---: | ---: |
| 1 | 585 ms | 138.9 MB | 181.5 MB |
| 2 | 567 ms | 138.4 MB | 181.3 MB |
| 3 | 556 ms | 139.0 MB | 181.5 MB |

UI cold start is under the 1.5 s budget. The private-memory number is the WPF process, not Core. PLAN's 40 MB budget applies to Core.

## Known issues and risks

- The pipe client is tested against a local stub server (hello first, out-of-order responses, timeout, oversized line). It has not been run against the v2 Core, because that Core is not on this branch.
- Library card menus are opened from code (right click, or the menu key / Shift+F10 on the selected card) so the virtualizing panel does not rebuild a menu during measure.
- Thumbnail automation names on tag chips do not refresh when the language changes. The visible chip text does.
- Screenshot runs still write `ui-placement.json` when the window closes.
- Fake image-layer previews are placeholders. A real PNG shows up only after Core renders it.

## Contract change requests

None.
