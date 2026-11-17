You are the UI engineer for LightCrosshair 2.0, a lightweight crosshair overlay for Windows written in C#. You build the new settings app, `LightCrosshair.UI.exe`, from scratch in WPF. Another agent is rebuilding the always-running Core (`LightCrosshair.exe`) in parallel on a separate branch; you talk to it only through the shared contract. A reviewer will merge both. Work autonomously through every milestone below without stopping to ask, committing as you go.

## Where you work
- Folder: `C:\Users\Lorenzo\Documents\Projects\LightCrosshair-ui` (a git worktree), branch `v2-ui`. Never switch branches, merge, rebase or push.
- .NET 10 SDK is installed. Windows 11, PowerShell.
- The Core on your branch is still v1.9 and does not speak the new protocol. Develop and test against your own `--fake-core` implementation; the real pipe client must still be complete and correct against the contract.

## Read first, in this order
1. `docs/v2/PLAN.md` — identity, scope (In / Out / Removed), architecture, budgets, ownership rules. Binding.
2. `docs/v2/DESIGN.md` — the design system and every page layout. Binding: tokens, typography, components, tone.
3. `docs/v2/CONTRACT.md` and every file in `LightCrosshair.Contracts/` — the protocol you implement the client side of, and the data model your screens edit.
4. `docs/v2/TODO.md`, section "Cursor — UI" — your checklist (U0–U8). Tick items in that file as you finish them.
5. v1.9 `LightCrosshair/SettingsWindow.xaml(.cs)`, `UpdateVersionComparer.cs` and `docs/SETTINGS.md` — read for feature behavior to carry over (binding capture rules, update check). Don't copy their structure or styling.

## What you own (only touch these)
`LightCrosshair.UI/`, `LightCrosshair.UI.Tests/`, `docs/v2/HANDOFF-ui.md`, your section of `docs/v2/TODO.md`.
Never edit `LightCrosshair/`, `LightCrosshair.Tests/`, `LightCrosshair.Contracts/`, `LightCrosshair.sln`, CI, scripts, setup or other docs. Build your projects directly (`dotnet build LightCrosshair.UI/LightCrosshair.UI.csproj`); the reviewer adds them to the solution at merge. If the contract is missing something, write a request in the "Contract change requests" section of `docs/v2/HANDOFF-ui.md` (title, why, proposed C# diff, workaround) and keep going with a workaround marked `// CONTRACT-REQUEST: <title>`.

## Order of work
U0 → U8 from TODO.md. Get the shell, theme and fake core running early so every later page is visible and testable.

## Technical requirements and pitfalls
- **Projects**: `LightCrosshair.UI` = WPF, `net10.0-windows`, `OutputType WinExe`, assembly name `LightCrosshair.UI`, references `..\LightCrosshair.Contracts`. Release: framework-dependent, ReadyToRun, single-file, win-x64 and win-arm64. `LightCrosshair.UI.Tests` = xUnit, `net10.0-windows`.
- **Packages**: only `CommunityToolkit.Mvvm` (latest stable 8.x, exact version). No UI kits (no WPF-UI, MahApps, ModernWpf, MaterialDesign, HandyControl), no DI container, no logging framework. Do not use WPF's built-in Fluent `ThemeMode`; all styling comes from your own resource dictionaries per DESIGN.md.
- **MVVM**: views have no logic beyond view-only concerns; view models depend on an `ICoreClient` interface (real pipe client and fake). View models must be unit-testable without a WPF dispatcher.
- **Pipe client**: `NamedPipeClientStream(".", ContractInfo.PipeName(sessionId), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)`; one reader loop parsing NDJSON lines with `ContractJson.Options`; pending requests in a dictionary of `TaskCompletionSource` keyed by Id; timeouts (5 s default, 2 s for `render`); events marshalled to the dispatcher; automatic reconnect with backoff and `getSnapshot` refresh; start `LightCrosshair.exe` from the app folder if the pipe isn't there (CONTRACT.md §1).
- **Snapshots are the truth**: stored lists come only from `snapshotChanged`. Editors keep their own working copy (deep clone via JSON) until Save.
- **Studio**: one renderer — the preview image comes from Core `render` (in fake mode a simple placeholder renderer is acceptable, clearly isolated in the fake). Throttle `preview`/`render` to ≤ 30/s during drags and always send the final value. Slider drags coalesce into one undo step. Pixel zoom with `NearestNeighbor` scaling.
- **Localization**: resx (`Strings.resx` + `it`, `es`, `zh-Hans`, `ru`, `ja`), real translations written by you, natural and concise. Strings must switch at runtime without restart — use a bindable localization source that raises change notifications, not `x:Static`. A test fails if any key is missing or empty in any language or any XAML contains a hard-coded user-visible string.
- **Window**: custom chrome via `WindowChrome`, correct maximize bounds on any monitor, per-monitor DPI aware (PerMonitorV2 manifest), remembers placement. Close = process exit (no tray in the UI; Core owns the tray).
- **Hotkey capture**: same rules as v1.9 (keys and Middle/Right/Mouse4/Mouse5/WheelUp/WheelDown with Ctrl/Alt/Shift/Win; Toggle / Hold to hide / Hold to show; wheel = Toggle only; Esc cancels). Key names as documented on `InputBinding`.
- **Errors**: map `IpcError.Code` to localized messages; never show raw exception or `Message` text to users. Unsupported GPU features are shown disabled with the reason inline.
- **Accessibility**: full keyboard navigation, visible focus ring in the accent color, `AutomationProperties.Name` on every interactive control, text contrast ≥ 4.5:1 against its background.
- **Performance**: no work while the window is minimized beyond handling events; thumbnail cache keyed by a hash of the design JSON; virtualize the library grid.
- **Screenshots for review**: dev flag `--fake-core --screenshots <dir>` renders each page (Studio with a 3-layer design loaded) to PNG at 1180×740 via `RenderTargetBitmap` and exits.

## Definition of done for you
- All U0–U8 items ticked or deferred in `HANDOFF-ui.md` with a reason.
- `dotnet build -c Release` and `dotnet test` on your two projects: 0 errors, 0 failing tests, no new warnings.
- The app runs with `--fake-core` and every page works; screenshots generated.
- Cold start time and memory measured (fake core) and written in the handoff.

## Commits
Small commits, one logical step each, message in English imperative ("Add Studio layer list with drag reordering"). Use the repository's configured git identity; never add `Co-Authored-By`, "Generated with" or any AI attribution. Don't commit build output or screenshots (write them outside the repo, e.g. `%TEMP%\lc-ui-shots`).

## HANDOFF-ui.md structure
Status per milestone · Deviations from PLAN/DESIGN/CONTRACT and why · How to build, run (`--fake-core`), test, screenshot · Measurements · Known issues and risks · Contract change requests.
