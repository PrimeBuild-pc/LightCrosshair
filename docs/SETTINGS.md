# Settings guide — LightCrosshair 2.0

## Studio

Edit up to eight layers in draw order. Arms can make crosses, T shapes, X shapes or single guides. Dot, Ring, Box and PNG Image layers add other shapes; a gap in a Box produces corner brackets. Each layer has color, opacity, position, rotation and outline. Dimensions use physical screen pixels. An odd stroke centers on the monitor center pixel; an even stroke adds its extra pixel down/right.

Preview uses the same renderer as the overlay. Live preview temporarily overrides visibility and ends when disabled, when settings disconnects, or after ten minutes without an update. Save commits the design to the library. Closing settings ends the separate settings process.

## Library and sharing

Use one of 30 original presets or edit a copy. Favorites control the next/previous hotkeys; with no favorites they cycle all designs. Cycling changes only the current context and is not saved as the default.

A `LC2:` share code contains an editable design without image layers. Use `.lcx.json` export for designs containing images. PNG import accepts files up to 2 MiB and 512 pixels per side. Imported files receive new identifiers. Core validates and persists every library change, then settings receives a fresh snapshot.

## Games

Match a profile by lower-case executable name. It can override the crosshair, display colors, visibility binding and FPS setting. An optional NVIDIA cap is written to the application's driver profile when saved and removed when that managed cap/profile is deleted. Leaving a game restores default crosshair rules and original display colors. Editing in LightCrosshair settings preserves the current game context.

Enable Show only in configured games to hide the default elsewhere. Local suggestions offer profiles for recognized games; dismissing a suggestion persists the dismissal. No game list or process information is uploaded.

## Hotkeys

Defaults are Alt+X (visibility), Alt+C/Alt+V (cycle) and Alt+L (settings). Bind keyboard keys, MiddleClick, RightClick, Mouse4, Mouse5 or WheelUp/WheelDown with optional Alt, Ctrl, Shift and Win modifiers. Visibility supports Toggle, Hold to hide and Hold to show. Other actions and wheel bindings toggle only. Hold release restores the saved visibility preference. Mouse buttons retain their normal game behavior. Unavailable registrations and conflicts are shown in settings.

## Display and behavior

Select a monitor and physical-pixel position offsets; an unavailable monitor falls back to primary. Display changes trigger a redraw. Start with Windows registers the Core for the current user. Language changes update Core tray text immediately. Hide during screen recording uses a two-second process-presence check only while enabled; running recording software may hide the crosshair even when it is not actively recording.

## FPS overlay

Optional FPS, frametime and 1% low values use ETW present events for the current foreground process. Capture stops when the effective context disables it. A dash means no recent samples; Windows permissions or the game's presentation path may prevent ETW data. This is present telemetry, not an in-game frame-generation signal.

## Storage and recovery

Core is the only writer of `%AppData%\LightCrosshair\v2\store.json`. The folder also contains `images`, automatic `backups`, and rolling `logs\core.log`. The prior store is backed up at most once per hour when changes occur; ten automatic store backups are retained. First migration preserves copies of the v1 source files and never alters them.

A corrupt store is renamed and the newest valid backup is loaded. The display-color recovery marker is removed only after original values have been restored; restart Core to retry a failed restoration. Back up the whole v2 folder to retain images as well as designs.
