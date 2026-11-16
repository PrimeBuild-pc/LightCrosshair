# Settings guide — LightCrosshair 2.0

Open settings from the tray or with Alt+L. Closing this window leaves the Core and overlay running. **Exit LightCrosshair** on the Settings page stops both processes and restores display colors.

## Visibility and saving

**Show crosshair** in the sidebar is the global visibility switch. Layer visibility is independent: a design with every layer hidden draws nothing even when the global switch is on. Hold bindings and configured-game rules can also affect whether the overlay is shown.

Studio and game-profile edits have explicit **Save** actions. Settings and global hotkey changes save automatically; errors are shown in the UI. Using a saved crosshair or cycling saves the selection into the active configured game profile. With no configured game active, it replaces the default crosshair.

## Studio

Choose a design name, edit its layers and use **Save** or **Save as new**. Undo/redo is available through the buttons and Ctrl+Z/Ctrl+Y. Leaving Studio with unsaved edits offers Save, Discard or Cancel.

The layer list supports adding, selecting, hiding, duplicating, deleting and reordering up to eight layers. Drag a layer, use the move buttons, or press Ctrl+Up/Ctrl+Down while the list is focused. Draw order matters when layers overlap.

### Layer controls

| Control | Effect |
|---|---|
| Kind | Arms, Dot, Ring, Box or PNG Image |
| Length / diameter | Size in physical pixels, 1–256; arm length for Arms, diameter/side for other vector shapes |
| Thickness | Stroke width for Arms, Ring and Box, 1–64 px |
| Gap | Empty center for Arms; open sections of a Box create corner brackets, 0–128 px |
| Arms | Enable Top, Right, Bottom and Left independently; rotation can produce an X |
| Color | Vector fill/stroke color in hexadecimal RGB; images retain their PNG colors |
| Fill | Optional independent interior color for Ring and Box |
| Opacity | Whole layer opacity, 0–100%, including its outline and fill |
| Offset X / Y | Layer position relative to the crosshair center; editor range −128 to +128 px |
| Rotation | Layer rotation; editor range −180° to +180° |
| Outline | Enable an outline, choose its color and thickness, 1–8 px |
| Image scale / Import image | Scale a PNG from 0.1× to 4×, or choose its source image |
| Anti-alias | Smooth edges for the whole design; disable for crisp integer-pixel geometry |

The controls shown depend on the selected kind. An odd-width stroke centers on the center pixel; an even-width stroke uses its extra pixel down/right. Imported designs are validated by Core; the table describes the editor's controls.

### Preview

Zoom at 1×, 2× or 4× and choose dark, mid, light, checkerboard or a custom background. Background and zoom are preview aids; they do not change the in-game background or crosshair size. Preview uses the same renderer as the overlay.

**Live in game** sends the current editable design to the overlay, temporarily overriding global visibility. It does not save the design. Turning it off, disconnecting settings or leaving it without an update for ten minutes ends the temporary preview. Use Save to retain edits.

PNG import accepts a PNG file up to 2 MiB and 512 pixels on either side. It is copied into the app's image store; the original file is not edited.

## Library and sharing

**My crosshairs** contains saved designs; **Presets** is a gallery of 30 bundled designs. Search by name and use tag filters to narrow the lists. Click a preset to **Use** it: accept or change the suggested name and press Enter to save and activate your copy; Esc cancels. The bundled original stays unchanged. If a copy of that preset already exists, you can reuse it.

Click a saved crosshair to use it in the current context; the active design is marked **In use**. Choose Edit, or double-click, to open Studio. The saved-card menu offers Use, Edit, Duplicate, Favorite, Export and Delete. A crosshair used as the default or assigned to a game cannot be deleted until its assignments are changed.

Next/previous hotkeys cycle favorites only when at least two exist. With zero or one favorite, they cycle all saved crosshairs. The order matches Library (most recently updated first); gallery presets are included only after a copy is saved. Cycling persists into the active enabled game profile or, outside a configured game, the default. Leaving a game restores the default selection without losing that game's saved choice.

- Copy/paste an `LC2:` share code for a vector design. Image layers cannot be included in share codes.
- Export `.lcx.json` files to include PNG images with the selected designs; import them from Library.
- Imported or duplicated designs get new identifiers. Import does not replace the original designs.

## Games

Use **Add game** to choose a running windowed process or browse to an `.exe`. A profile matches the executable file name, not a particular window title or full path. One profile is allowed per executable name.

Select a profile to edit its name and crosshair, then Save. The list's enable switch controls whether its foreground overrides are used. Deleting a profile removes its game association, leaving its saved crosshair in Library.

| Control | Behavior |
|---|---|
| Crosshair | Saved design to use while the configured executable is in the foreground |
| Visibility override | Use a game-specific visibility binding and mode, or inherit the global binding |
| FPS overlay | Follow the global setting, force On or force Off |
| Use display colors | Apply saved values while this game is active; switching off retains the values |
| Gamma / Contrast / Brightness | Software display adjustments, 50–150; 100 is neutral |
| Vibrance | Software vibrance, 0–100; 50 is neutral |
| NVIDIA Digital Vibrance | Optional driver adjustment, 0–100, when supported |
| NVIDIA FPS cap | Optional per-app driver cap; slider range 30–480 FPS |

Display colors affect the display, not just the crosshair. Leaving a game restores default crosshair rules and the original display colors; working in LightCrosshair settings preserves the current game context. Driver-specific controls depend on the available hardware and display mode. NVIDIA features are **not yet verified on NVIDIA hardware**.

AMD brightness, contrast and vibrance can work even when the display's gamma-ramp API is unavailable. Gamma 100 is neutral and preserves the existing calibration; a non-neutral gamma can be unsupported on that driver/output. Apply failures are reported through the current game context and logged with the vendor's reason. A later successful apply or leaving the game clears the apply error. A failed restoration keeps its recovery journal so it can be retried.

A driver cap is written when the profile is saved and persists in the driver's application profile. Turning off the profile's foreground enable switch does not clear that cap: switch off the cap and Save, or delete the managed profile, to remove it. Old driver caps that were never managed by 2.0 are left untouched.

Known-game suggestions offer **Create**, **Not now** and **Never for this game**. Not now suppresses the current suggestion; Never persists the dismissal. No game list or process information is uploaded.

## Hotkeys

| Default | Action |
|---|---|
| Alt+X | Toggle crosshair visibility |
| Alt+C | Next favorite when at least two exist; otherwise next saved crosshair |
| Alt+V | Previous favorite when at least two exist; otherwise previous saved crosshair |
| Alt+L | Open or close settings |

Click a capture field and press the desired keyboard key; Esc cancels. For a mouse binding, use the explicit picker for Middle click, Right click, Mouse4, Mouse5, Wheel up or Wheel down, then choose the Alt, Ctrl, Shift or Win modifiers alongside it. Mouse input can also be captured while the capture field is active. **Clear** removes a binding.

Visibility supports Toggle, Hold to hide and Hold to show. Other actions and all wheel bindings use Toggle. Releasing a hold restores the underlying visibility state. Core actions are paused during binding capture and resume when it ends. Input still reaches other applications; a bound mouse button keeps its normal game behavior.

Conflicts and Core registration failures appear under the affected action. Choose another binding if Windows or another application already owns it.

## Settings

### Display

Choose the overlay monitor by model name, for example **DELL S2721DGF · Display1**. The model comes from Windows display configuration; an adapter description is used when the model is unavailable. The primary monitor is the default and fallback when the selected display is unavailable. **Offset X / Y** moves the whole crosshair from that monitor's center in physical pixels; the editor exposes −200 to +200 px. Positive X moves right and positive Y moves down. The position diagram and numeric fields describe the same offset.

### Behavior

| Setting | Effect |
|---|---|
| Show only in configured games | Hide the crosshair elsewhere; the global Show crosshair switch still applies |
| Hide during screen recording | Check for recorder processes only while enabled; an open recorder can hide the crosshair even when it is not recording |
| Start with Windows | Start the Core for the current user at sign-in |
| Suggest known games | Offer local profile suggestions for recognized foreground executables |

### FPS overlay

Enable the optional FPS overlay globally, or override it per game. Capture starts/stops with the effective setting. It shows FPS with optional frametime and 1% low values for the foreground process, keeping separate swapchains independent. After two seconds without frames, “–” appears with a localized reason, such as no recent frames or unavailable capture permissions. Failed capture sessions retry while enabled. These are external ETW present-event measurements, not a frame-generation detector; no RTSS or game hook is required.

| Control | Effect |
|---|---|
| Monitor | Use the crosshair's monitor, or choose another display |
| Offset X / Y | Position from that monitor's top-left corner; editor range 0–2000 px |
| Scale | Text/overlay scale, 0.5×–3× |
| Frametime / 1% low | Show or hide the additional values |
| Text color / Background color | Hexadecimal RGB colors |
| Background opacity | Background transparency, 0–100% |

### Language, updates and exit

Choose Automatic or English, Italian, Spanish, Simplified Chinese, Russian or Japanese. The UI and Core tray text update without a restart.

**Check now** queries the public GitHub release feed and opens the releases page when a newer version is found; it does not download or install an update. The automatic-check preference is saved, but the current UI performs checks through the explicit Check now action.

About shows the version, website/source links and license information. **Exit LightCrosshair** asks for confirmation, closes settings and stops Core, restoring display colors.

## Storage and recovery

The default data folder is `%AppData%\LightCrosshair\v2`. It contains `store.json`, imported `images`, `backups`, rolling `logs\core.log`, and the UI's `ui-placement.json`. Back up the whole folder to retain images with your designs. Core is the only writer of the main store.

Store backups are made at most once per hour when changes occur; the newest ten are retained. A corrupt store is renamed and the newest valid backup is loaded. If loading cannot recover, Core stays running with temporary defaults, shows a notification and logs the failure; it leaves the store marker unwritten so the next launch can retry. Changes in that temporary session are not durable.

The display-color recovery journal is cleared only after original values are restored. If restoration fails, restart Core to retry; keep the journal rather than deleting it.

Migration imports supported v1 data without writing the old files, preserving source copies under `backups/migration`. The active v1 master visibility switch becomes the global v2 switch; it does not hide the individual migrated layers. See [Upgrading from 1.9](../README.md#upgrading-from-19).
