# LightCrosshair 2.0 — Core ⇄ UI contract

The C# types in [`LightCrosshair.Contracts/`](../../LightCrosshair.Contracts) are the contract. This file adds the behavior the types can't express. If code and this file disagree, raise a contract change request (§8) instead of guessing.

## 1. Processes
- **Core** = `LightCrosshair.exe` (project `LightCrosshair/`). Single instance per user session (named mutex `Local\LightCrosshair.Core`). Pipe server.
- **UI** = `LightCrosshair.UI.exe` (project `LightCrosshair.UI/`), in the same folder as Core. Single instance (mutex `Local\LightCrosshair.UI`). Pipe client.
- Core starts the UI with optional args `--page <studio|library|games|hotkeys|settings>` and `--suggest <process.exe>`.
- If the UI is already running when Core wants to open it, Core sends `focusRequested` (with a `NavigateRequest` when a page is needed) instead of starting a second one.
- OpenSettings hotkey while the UI is connected → Core sends `closeRequested`.
- UI started without Core: it starts `LightCrosshair.exe` from its own folder and retries connecting for up to 5 s, then shows a localized error with a Retry button.

## 2. Transport
- Named pipe `ContractInfo.PipeName(sessionId)` where `sessionId` = `Process.GetCurrentProcess().SessionId`. Server created with `PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous`. One UI client at a time; a second connection is refused.
- Framing: one `IpcEnvelope` per line, compact JSON via `ContractJson.Options`, UTF-8, `\n` terminator. Lines over 8 MB are a protocol error (drop the connection).
- Handshake: the first request must be `hello`. On `ProtocolVersion` mismatch Core answers `protocol_mismatch` and closes; the UI tells the user to reinstall.
- Requests may be pipelined; each gets exactly one `res` with the same `Id`. Events (`evt`) have `Id = 0` and can arrive at any time, including between a request and its response.
- Core must never block its UI/overlay thread on the pipe. The UI must never block its dispatcher on the pipe.
- On disconnect Core ends any active preview. The UI reconnects automatically (backoff 0.25 → 2 s) and refreshes with `getSnapshot`.

## 3. Methods
Request → response payloads are documented on each constant in `IpcMethods` (Ipc.cs). Additional rules:
- **Validation is Core's job.** Save methods clamp numeric ranges documented in Model.cs, trim names (max 48 chars, empty → localized default chosen by the UI before sending), enforce `MaxLayers`, unique lower-case `ProcessName`, existing `CrosshairId`, existing `ImageFile`, valid `#RRGGBB` colors. Unfixable input → `validation` error.
- After every successful mutation Core persists, then emits `snapshotChanged` with the full snapshot. The UI treats snapshots as the only truth and does not apply optimistic updates to the stored lists (it may keep unsaved edits in its own editor state).
- `preview` is transient: never persisted, ends on `preview {design:null}`, on disconnect, or after 10 minutes without a new preview request. While a preview is active the overlay shows it even if the crosshair is hidden.
- `render` must take < 5 ms for a 128 px canvas. The UI throttles `preview` and `render` to ≤ 30 per second while dragging and always sends the final value.
- Errors: `bad_request` (malformed payload), `not_found`, `validation`, `unsupported` (e.g. NVIDIA feature on a non-NVIDIA PC, image in a share code), `internal` (logged by Core). The UI maps codes to localized messages and never shows `Message` to users.

## 4. Storage (Core only)
```
%AppData%\LightCrosshair\v2\store.json          StoreDocument, ContractJson.IndentedOptions
%AppData%\LightCrosshair\v2\images\<guid>.png    imported PNGs (validated)
%AppData%\LightCrosshair\v2\backups\store-<yyyyMMdd-HHmmss>.json
%AppData%\LightCrosshair\v2\logs\core.log
```
- Atomic writes (temp file + `File.Replace`), debounced 300 ms.
- Backup: copy of the previous store.json at most once per hour when a change happens, and before migration; keep the newest 10.
- Corrupt store.json → move it to `store.corrupt-<timestamp>.json`, load the newest valid backup, else defaults; report it once through the log and a tray notification.
- First start invariants: at least one crosshair exists and `Settings.DefaultCrosshairId` points to one.

## 5. Migration from v1.x (Core)
Runs once when `v2\store.json` doesn't exist. Reads `%AppData%\LightCrosshair\profiles.json`, `crosshair_settings.json`, `prefs.json`, `current_state.json` with the existing v1 loaders. **Never modifies or deletes v1 files.**
- Each v1 profile → one `CrosshairDesign` with equivalent layers (shape, size, thickness, edge thickness, gap, inner shape for composites, colors, outline, fill, antialias). The renderer output for a migrated design should match v1 as closely as the old geometry allows; cover each `CrosshairShape` and `CompositeShapeType` with a test.
- v1 profile with `DisplayTargetProcessName` → also a `GameProfile` for that process, using that design, with `DisplayColors` when `HasDisplayColorProfile`, and its `VisibilityBinding` as the override.
- v1 active profile → `DefaultCrosshairId`. Global hotkeys, visibility binding, monitor, offsets, recording option, FPS overlay basics → `AppSettings`.
- v1 NVIDIA FPS cap for an app → `NvidiaFpsCap` on the matching game profile if one exists; otherwise left in the driver untouched.
- Dropped silently: per-profile direct-select hotkey, Detailed FPS mode, graph and diagnostics flags, Frame Cap Assistant values.

## 6. Share code
`"LC2:" + base64url_no_padding(rawDeflate(utf8(json)))` where `json` is the `CrosshairDesign` serialized with `ContractJson.Options` after setting `Id = ""`, `Favorite = false`, `SourcePresetId = null`, `UpdatedAt = default`. Max `MaxShareCodeLength` characters. Designs containing an `Image` layer → `unsupported`. Decoding validates like `saveCrosshair` and assigns a new `Id`. Whitespace around a pasted code is ignored.

## 7. Game detection rules (Core)
- Foreground process name is resolved once per foreground change (`GetWindowThreadProcessId` + `QueryFullProcessImageName`), lower-cased file name.
- Matching enabled `GameProfile` → apply its crosshair, display colors (if set), FPS overlay override, visibility binding override. Leaving it → restore defaults and original display colors.
- Foreground = LightCrosshair.UI.exe → keep the current state (don't flicker while editing).
- No profile and `ShowOnlyInConfiguredGames` → crosshair hidden; otherwise default crosshair.
- Known-games list (embedded JSON, ~50 entries: process name + display name). Unknown-to-profiles known game in foreground, `SuggestKnownGames` on, not dismissed, not suggested yet in this session → `PendingSuggestion` + one tray notification; clicking it opens the UI with `--page games --suggest <process>`.
- Next/Previous crosshair hotkeys change the active crosshair for the current context only (not persisted).

## 8. Change rules
- `LightCrosshair.Contracts` changes only on branch `v2`, by Claude.
- Agents write requests in the "Contract change requests" section of their own `docs/v2/HANDOFF-<core|ui>.md`: title, why, proposed C# diff, workaround in use. Meanwhile they code against the current contract (adapter, `// CONTRACT-REQUEST: <title>` comment).
- Adding an optional property with a default is non-breaking. Renames, removals and semantic changes bump `ProtocolVersion`.
