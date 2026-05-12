# LightCrosshair — Settings & Options Guide

## Table of Contents

1. [Crosshair Settings](#crosshair-settings)
2. [FPS Overlay Settings](#fps-overlay-settings)
3. [Hotkeys](#hotkeys)
4. [Profiles](#profiles)
5. [Display Settings (Gamma / Vibrance)](#display-settings-gamma--vibrance)
6. [GPU Driver Integration](#gpu-driver-integration)
7. [Advanced / Troubleshooting](#advanced--troubleshooting)

---

## Crosshair Settings

The Crosshair Builder tab in the Settings window lets you customise the appearance, position, and visibility of the on-screen crosshair overlay.

### Enable Custom Crosshair

- **What it does:** Toggles the crosshair on/off globally. When disabled, the overlay is hidden regardless of other visibility settings.
- **UI control:** Checkbox labelled "Enable Custom Crosshair".
- **Default:** On (checked).
- **Performance impact:** None when disabled; negligible when enabled.

### Shape

- **What it does:** Selects the basic shape of the crosshair.
- **Available shapes (UI):** Dot, Cross, Circle.
- **Internal shapes (code):** `Dot`, `Cross`, `CrossOutlined`, `Circle`, `CircleOutlined`, `T`, `X`, `Box`, `GapCross`, `Custom` — these are available programmatically but only Dot, Cross, and Circle are exposed in the UI.
- **UI control:** Combo box (ShapeCombo).
- **Default:** Cross.
- **Recommendation:** Choose the shape that gives you the best visibility in your games. Cross is the most versatile.

### Size

- **What it does:** Controls the overall size (diameter/span) of the crosshair in pixels.
- **Range:** 2–100.
- **UI control:** Slider (SizeSlider).
- **Default:** 15 (in profile); config default is 20.
- **Performance impact:** None.

### Thickness

- **What it does:** Controls the thickness of the crosshair lines in pixels.
- **Range:** 1–20.
- **UI control:** Slider (ThicknessSlider).
- **Default:** 5 (in profile); config default is 2.
- **Performance impact:** None.

### Outline

- **What it does:** When enabled, draws a contrasting outline (edge colour) around the crosshair for better visibility against bright or complex backgrounds.
- **UI control:** Checkbox labelled "Use Secondary/Edge Color".
- **Default:** Off (unchecked).
- **Recommendation:** Enable if you play on bright or highly varied backgrounds.

### Gap

- **What it does:** Sets the size of the centre gap in cross-type shapes. A larger gap gives you a clearer view of the target behind the crosshair.
- **Range:** 0–50.
- **UI control:** Slider (GapSlider).
- **Default:** 4.
- **Recommendation:** 2–6 is typical for most games. Larger gaps suit slower, more precise aiming.

### Colors

#### Main Color (Outer)

- **What it does:** The primary colour of the crosshair. For simple shapes (Dot, Cross) this is the sole colour.
- **UI control:** Button labelled "Main Color" → opens a colour picker dialog.
- **Default:** Red (in profile).
- **Recommendation:** Choose a colour that contrasts with your typical game backgrounds. Neon colours (cyan, lime, magenta) tend to work well.

#### Secondary/Edge Color

- **What it does:** Used for the edge/outline when the Outline option is enabled. For composite shapes this is the inner shape colour.
- **UI control:** Button labelled "Secondary/Edge Color" → opens a colour picker dialog.
- **Default:** White (in profile).

### Visibility Presets

- **What it does:** Quickly applies a pre-configured high-contrast colour scheme optimised for visibility. Each preset sets:
  - Outer and inner colour to a bright neon colour with full opacity.
  - Edge colour to black.
  - Enables the outline.
- **Available presets:**
  - **Neon Cyan** — Cyan (#00FFFF) with black outline.
  - **Lime** — Green (#00FF00) with black outline.
  - **Magenta** — Magenta (#FF00FF) with black outline.
  - **Yellow** — Yellow (#FFFF00) with black outline.
- **UI control:** Combo box + "Apply Preset" button.
- **Default:** Neon Cyan selected.
- **Recommendation:** Use presets instead of manual colour picking when you need maximum contrast fast. These are the safest colour path — they don't depend on the operating system's display colour backend.

### Position & Tweaks

The crosshair can be nudged in 1-pixel increments to fine-tune its screen position. This is useful when the crosshair doesn't align perfectly with the centre of your screen.

#### Nudge Buttons

- **What they do:** Move the crosshair 1 pixel in the respective direction.
- **UI controls:** ← (left), ↑ (up), ↓ (down), → (right) buttons.
- **Recommendation:** Use if the crosshair doesn't seem centred. This can happen with unusual resolutions or display scaling settings.

#### Reset Center

- **What it does:** Returns the crosshair to the centre of the screen.
- **UI control:** Button labelled "Reset Center".

#### Current Position

- **What it does:** Shows the current X,Y coordinates of the crosshair centre.
- **UI control:** Read-only text label.
- **Example:** `Current Position: X=960, Y=540`

---

## FPS Overlay Settings

The FPS & Performance tab controls LightCrosshair's built-in frame rate and frame timing overlay. It uses non-injected ETW telemetry with optional RTSS fallback.

### Enable FPS Overlay

- **What it does:** Master toggle for the entire performance overlay.
- **UI control:** Checkbox labelled "Enable FPS Overlay".
- **Default:** Off.
- **Performance impact:** When on, LightCrosshair starts a background ETW trace session to capture present events. This has minimal overhead (<1% CPU on modern hardware), but does consume some resources.

### Display Mode

- **What it does:** Controls which information the overlay shows.
- **Available modes:**
  - **Off** — Overlay hidden (same as disabling the overlay checkbox).
  - **Minimal** — Shows only the basic FPS counter, frame time, and data source. Compact layout.
  - **Detailed** — Shows full metrics including 1% lows, frame generation, and optional pacing diagnostics.
- **UI control:** Combo box (FpsOverlayModeCombo).
- **Default:** Minimal.
- **Performance impact:** Detailed mode has higher overhead due to graph rendering and pacing calculations. Minimal mode is recommended for lower-end systems.

### Ultra-Lightweight Mode

- **What it does:** Prioritises low overhead by:
  - Reducing the update interval to 500 ms (instead of 33–100 ms).
  - Displaying simple text only (no graph).
  - Hiding pacing, diagnostics, and frame-generation details.
  - Forcing the effective display mode to Minimal (even if Detailed is set).
- **UI control:** Checkbox labelled "Ultra-lightweight mode".
- **Default:** Off.
- **Performance impact:** Significantly lower CPU/GPU usage. Recommended for low-end systems or when every frame matters.
- **Tooltip note:** "Prioritizes low overhead: slower updates, simple text, no graph, and no detailed diagnostics."

### Position X / Y

- **What it does:** Sets the screen position of the overlay's top-left corner (in screen coordinates).
- **Range:** X: 0–3840, Y: 0–2160.
- **UI control:** Sliders (FpsXSlider, FpsYSlider).
- **Default:** X=10, Y=10.
- **Recommendation:** Keep the overlay in a corner where it doesn't obstruct gameplay elements. The values automatically clamp to keep the overlay fully on-screen.

### Overlay Scale

- **What it does:** Scales the entire overlay (text, graph, spacing) as a percentage of normal size.
- **Range:** 50%–300%.
- **UI control:** Slider (FpsScaleSlider).
- **Default:** 100%.
- **Performance impact:** None. Larger sizes may increase pixel fill, but the difference is negligible.

### Displayed Metrics

Each metric in the Detailed mode can be individually toggled:

#### Show FPS

- **Label on overlay:** `FPS: 144`
- **What it shows:** Instantaneous frames per second, calculated as a rolling average of frame times over the last ~500 ms.
- **UI control:** Checkbox labelled "Show FPS".
- **Default:** On.
- **Recommendation:** Keep this on for general performance monitoring.

#### Show Frametime Text

- **Label on overlay:** `FT: 6.9 ms`
- **What it shows:** The most recent single frame time in milliseconds.
- **UI control:** Checkbox labelled "Show frametime text".
- **Default:** On.
- **Recommendation:** Useful for spotting individual frame spikes.

#### Show Frametime Graph

- **What it does:** Renders a small live-scrolling graph of recent frame times, with a horizontal target line at the ideal frame time for the current refresh rate.
- **UI control:** Checkbox labelled "Show frametime graph".
- **Controls available when enabled:**
  - **Graph Refresh Rate:** How often the graph is redrawn.
    - `Ultra (33 ms)` — Smoother animation, higher CPU/GPU overhead.
    - `Smooth (66 ms)` — Good balance (default).
    - `Balanced (100 ms)` — Lower overhead, less frequent updates.
  - **Graph Time Window:** How much history is visible on the X axis.
    - `Reactive (1500 ms)` — More responsive to recent changes.
    - `Standard (2000 ms)` — Default, good stability.
    - `Stable (3000 ms)` — Better for spotting long-term trends.
- **Default:** On.
- **Performance impact:** Graph rendering adds overhead. Disable if you don't need it. Ultra (33 ms) refresh is the most expensive option.
- **Tooltip notes:**
  - Refresh rate: "Lower values feel smoother but use more CPU/GPU. Higher values reduce overhead but update less frequently."
  - Time window: "Shorter windows are more reactive. Longer windows are more stable and better for spotting trends."

#### Show 1% Lows

- **Label on overlay:** `1% LOW: 120`
- **What it shows:** The average FPS of the worst 1% of frames over the last ~1000 ms. Lower values indicate stuttering or inconsistency.
- **UI control:** Checkbox labelled "Show 1% Lows".
- **Default:** On (in Detailed mode).
- **Recommendation:** Keep this on if you care about frame time consistency. A large gap between FPS and 1% lows indicates uneven frame pacing.

#### Show Pacing/Stutter Details

- **Label on overlay (when enabled):**
  - `FT AVG: 6.9 ms` — Average frame time.
  - `0.1% LOW: 90` — Average FPS of the worst 0.1% of frames (more sensitive than 1% low).
  - `JIT: 0.5 ms SD: 0.3` — Jitter (average absolute difference between consecutive frame times) and standard deviation.
  - `HITCH: 0` — Number of frames exceeding the hitch threshold (50 ms by default).
  - `PACE: 95` — Stability score (0–100). Higher is better.
- **UI control:** Checkbox labelled "Show pacing/stutter details".
- **Default:** Off.
- **Performance impact:** Enables additional frame pacing calculations. Low overhead.
- **Tooltip note:** "Adds frame pacing, jitter, hitch, and stability details to the FPS overlay."

#### Show Frame-Generation Estimate

- **Labels on overlay:**
  - `GEN: 0` — Generated frame count (when frame generation is detected via verified provider signal).
  - `GEN EST: 5` — Estimated generated frame count (heuristic suspicion based on present cadence only).
  - `FG: SUSPECT 75%` — Frame generation suspected with confidence percentage.
  - `FG: UNKNOWN` — Unable to determine.
  - `FG: N/A` — Frame generation data not available / unsupported.
  - `FG: VERIFIED <technology> x1.5` — Verified frame generation with multiplier ratio.
- **What it does:** Attempts to detect whether the tracked game/application is using frame generation technology (e.g., DLSS 3 Frame Gen, FSR 3 Fluid Motion Frames). Without a verified provider signal, this is heuristic suspicion only.
- **UI control:** Checkbox labelled "Show frame-generation estimate".
- **Default:** On.
- **Tooltip note:** "Without a verified provider signal, this is heuristic suspicion from present cadence only. Only verified provider signals may be labeled as detected frame generation."

### Overlay Colors

#### Text Color

- **What it does:** Sets the colour of all overlay text and graph lines.
- **UI control:** Button labelled "Text Color" → opens a colour picker.
- **Default:** White (255,255,255).

#### Background Color

- **What it does:** Sets the background colour of the overlay panel.
- **UI control:** Button labelled "Background Color" → opens a colour picker.
- **Default:** Black (0,0,0).
- **Recommendation:** A semi-transparent or dark background improves readability against bright game scenes.

### FPS Overlay Labels Reference

When the overlay is active, it displays several lines of text. Here is what each abbreviation means:

| Label  | Meaning                              | Description |
|--------|--------------------------------------|-------------|
| `FPS`  | Frames Per Second (Instant)          | Rolling average over ~500 ms window. |
| `AVG`  | Average FPS                          | Average over the last ~1000 ms. |
| `FT`   | Frame Time                           | Most recent single frame time in milliseconds. |
| `1% LOW` | 1st Percentile FPS Low             | Average FPS of the worst 1% of frames. |
| `0.1% LOW` | 0.1st Percentile FPS Low        | Average FPS of the worst 0.1% of frames (more sensitive). |
| `FT AVG` | Average Frame Time                | Mean frame time over the sample window. |
| `JIT`  | Jitter                               | Average absolute difference between consecutive frame times. |
| `SD`   | Standard Deviation                   | Standard deviation of frame times — higher means more variance. |
| `HITCH` | Hitch Count                         | Number of frames exceeding 50 ms (stutter events). |
| `PACE` | Stability / Pacing Score             | 0–100 score derived from variance, jitter, and hitch density. |
| `GEN`  | Generated Frames                     | Count of frames detected as generated (via heuristic or verified signal). |
| `GEN EST` | Generated Frame Estimate         | Estimated count when frame generation is suspected but not verified. |
| `FG`   | Frame Generation Status              | UNKNOWN / N/A / SUSPECT / VERIFIED with confidence or technology name. |
| `SRC`  | Data Source                          | ETW, RTSS, or None depending on the active telemetry source. |

---

## Hotkeys

The Hotkeys tab lets you configure global keyboard shortcuts. LightCrosshair uses Win32 `RegisterHotKey` for global hotkey registration, which works even when the app is minimised or in the background.

**Important:** Hotkey conflicts with other apps or games may prevent registration. If a hotkey doesn't work, try a different combination.

### Available Hotkeys

| Action                     | Default Combination | Config Properties (CrosshairConfig)           |
|----------------------------|---------------------|-----------------------------------------------|
| Toggle Crosshair Visibility | `Alt + X`          | `HotkeyKey`, `HotkeyUseAlt/Ctrl/Shift/Win`    |
| Cycle Profiles Forward     | `Alt + C`           | `CycleProfileHotkeyKey`, `CycleProfileHotkeyUse*` |
| Cycle Profiles Backward    | `Alt + V`           | `CycleProfilePrevHotkeyKey`, `CycleProfilePrevHotkeyUse*` |
| Toggle Settings Window     | `Alt + L`           | `SettingsWindowHotkeyKey`, `SettingsWindowHotkeyUse*` |

### Customising a Hotkey

For each hotkey, you can set:
- **Modifier keys:** Alt, Ctrl, Shift, Win (any combination).
- **Base key:** Select from a dropdown of all `System.Windows.Forms.Keys` values.

**Recommendation:** Use combinations with Alt or Ctrl to avoid interfering with in-game keybinds. Avoid single-key hotkeys as they may conflict with typing or gameplay inputs.

### Hotkey Registration Errors

If a hotkey combination is already registered by another application, LightCrosshair will silently skip registration. The Settings window does not currently display registration failures, but the action simply won't trigger. Try a different key combination if a hotkey seems unresponsive.

---

## Profiles

The Profiles tab lets you save, load, rename, and delete named profiles. Each profile stores the complete crosshair configuration including shape, size, thickness, gap, colours, outline, and display settings.

### Profile Slots

- Up to **10 profile slots** are available.
- Slot 1 is always the **Default** profile, which is **immutable** — it cannot be overwritten, renamed, or deleted.
- Empty slots display as "Slot N: (Empty)" with a "Save Here" button.

### Profile Actions

| Action   | UI Control                          | Description |
|----------|-------------------------------------|-------------|
| **Save** | "Save" button on a profile slot     | Overwrites the selected profile with the current settings. Not allowed on the Default profile. |
| **Load** | "Load" button on a profile slot     | Applies the profile's settings immediately. |
| **Rename** | "Rename" button                   | Opens a dialog to change the profile name. Names must be unique. |
| **Delete** | "Delete" button                   | Removes the profile. Not allowed on the Default profile. |
| **Save to Selected** | "Save Current to Selected" button | Saves the current state to whichever profile is highlighted in the list. Useful for quick overwrites. |
| **Save Here** (empty slot) | "Save Here" button | Creates a new profile at the empty slot position. |

### What a Profile Stores

Each profile captures:
- Shape, size, thickness, gap, inner shape, edge thickness.
- All colour values (inner, outer, edge, fill, inner shape colours).
- Outline enabled/disabled.
- Anti-alias setting.
- Hide-during-screen-recording flag.
- Display colour settings (gamma, contrast, brightness, vibrance, target process).
- Hotkey (legacy per-profile — the global hotkey system in CrosshairConfig takes precedence).

### Profile Data Storage

Profiles are stored as JSON files in:
```
%APPDATA%\LightCrosshair\Profiles\
```

---

## Display Settings (Gamma / Vibrance)

The Display Settings tab provides adjustments to the display's colour characteristics via hardware-level GPU LUT (Look-Up Table) operations. These settings are **experimental** and depend on your display hardware and driver support.

### Color Backend Info

- **What it does:** Displays the active colour management backend and GPU information.
- **UI control:** Read-only info box.
- **Example:** `GPU: NVIDIA GeForce RTX 3080 | Hardware path active`

### Enable Gamma/Contrast/Brightness/Vibrance Override

- **What it does:** Master toggle for display colour adjustments. When disabled, no gamma/vibrance changes are applied.
- **UI control:** Checkbox.
- **Default:** Off.
- **Warning:** These settings depend on legacy Win32 API paths. They may have **no effect** if:
  - Windows HDR is enabled.
  - You are using a laptop with hybrid graphics (Optimus/MUX switch).
  - Active colour profiles or Night Light are blocking the display LUT.

### Target Process

- **What it does:** Restricts display colour adjustments to a specific game/application process. Leave empty to apply globally.
- **UI controls:**
  - Text box — type the process name (e.g., `valorant.exe`).
  - Dropdown — pick from currently running processes.
  - "Refresh List" — refreshes the running process list.
  - "Browse .exe" — opens a file picker to select an executable.
  - "Clear" — clears the target process (applies globally).
- **Default:** Empty (global).
- **Note:** The process name is matched to the foreground window. When the target process is in the foreground, display adjustments are applied. When it's not, they are reverted.

### Gamma

- **What it does:** Adjusts the gamma ramp of the display (mid-tone brightness).
- **Range:** 50–150 (100 = default/normal).
- **UI control:** Slider + reset button (↻).
- **Default:** 100.
- **Recommendation:** Start with small adjustments (±5–10). Large gamma shifts can wash out colours or crush blacks.

### Contrast

- **What it does:** Adjusts the contrast of the display.
- **Range:** 50–150 (100 = default/normal).
- **UI control:** Slider + reset button.
- **Default:** 100.

### Brightness

- **What it does:** Adjusts the overall brightness of the display.
- **Range:** 50–150 (100 = default/normal).
- **UI control:** Slider + reset button.
- **Default:** 100.

### Vibrance

- **What it does:** Increases colour saturation. A higher value makes colours more vivid.
- **Range:** 0–100 (50 = default/normal).
- **UI control:** Slider + reset button.
- **Default:** 50.
- **Recommendation:** Values above 70 may oversaturate some colours. Values below 30 may look washed out.

---

## GPU Driver Integration

LightCrosshair 1.5.0 introduces a GPU driver integration layer that enables direct communication with NVIDIA and AMD GPU drivers for advanced features.

### GPU Detection

LightCrosshair automatically detects the primary GPU vendor on startup:
- **NVIDIA**: Full driver integration via NvAPIWrapper.Net
- **AMD**: Color management via ADL2 API
- **Intel / Unknown**: No driver integration (unsupported for now)

The detected GPU and driver API status are shown in the **GPU Driver** settings tab.

### Feature Support Matrix

| Feature | NVIDIA | AMD | Intel |
|---------|--------|-----|-------|
| Driver FPS Cap | ✓ Supported | ✗ Not Supported | ✗ Not Supported |
| Color/Vibrance | ✓ Supported | ✓ Supported (ADL2) | ✗ Not Supported |
| Radeon Chill | N/A | ✗ Not Supported (future) | N/A |
| G-Sync | ✗ Not Supported | N/A | N/A |
| FreeSync | N/A | ✗ Not Supported (future) | N/A |

### NVIDIA Driver FPS Cap

LightCrosshair can apply a frame rate limit via the NVIDIA driver profile system (DRS — Driver Registry Settings).

**How it works:**
- Uses the NVIDIA DRS API to set `PerformanceStateFrameRateLimiter` in an application-specific driver profile
- Applies only to the **target process** configured in Display Settings → Target Process
- Global profile fallback is **not available** — a target process must be set before applying

**Behavior:**
- Setting a cap writes to the NVIDIA driver profile for the specified application and persists until cleared
- Clearing the cap sets the limiter to 0 (disabled) for the specified application
- The current cap value can be read back from the driver for the specified application
- Reading a cap never creates or modifies a driver profile (read-only)

**Limitations:**
- Requires NVIDIA GPU with driver installed
- Requires the target process executable path (e.g., `valorant.exe`)
- Does not require administrator rights (user-mode DRS API)

**UI Controls (GPU Driver tab → NVIDIA Driver FPS Cap):**
- Target FPS slider: 15–300 FPS
- Apply / Clear buttons
- Status display showing current operation result

### NVIDIA Digital Vibrance

LightCrosshair can adjust digital vibrance (color saturation) via the NVIDIA driver's Digital Vibrance Control (DVC) API.

**How it works:**
- Uses `NvAPIWrapper.Native.DisplayApi.GetDVCInfo()` and `SetDVCLevel()`
- Applies to the primary NVIDIA display
- Range: 0–100 (mapped to driver's internal min/max range)
- Default/neutral value: 50

**Limitations:**
- Applies to primary display only
- Requires NVIDIA GPU with driver installed
- Does not require administrator rights

**UI Controls (GPU Driver tab → NVIDIA Digital Vibrance):**
- Vibrance slider: 0–100
- Apply / Reset to Default buttons

### AMD Color Management

AMD color management (brightness, contrast, saturation, vibrance) uses the existing ADL2 API integration, which is unchanged from previous versions. The GPU driver abstraction layer wraps this existing functionality.

See the **Display Settings** tab for AMD color controls (gamma, contrast, brightness, vibrance sliders).

### AMD Radeon Chill (Not Supported)

AMD Radeon Chill control requires the AMD ADLX SDK C++/CLI wrapper (`ADLXCSharpBind.dll`), which is not bundled in this release. This feature is planned for a future update.

### AMD FreeSync (Not Supported)

AMD FreeSync status and control requires the AMD ADLX SDK C++/CLI wrapper, which is not bundled in this release. This feature is planned for a future update.

### NVIDIA G-Sync (Not Supported)

NVIDIA G-Sync control is not exposed by the NvAPIWrapper.Net library. G-Sync is managed by the NVIDIA driver and monitor; use the NVIDIA Control Panel to configure G-Sync.

### Intel GPU (Not Supported)

Intel GPU driver integration is not implemented. Intel GPUs are detected but no driver API is used.

### Graceful Fallback

If no supported GPU driver API is available (e.g., running on Intel, or NVIDIA/AMD driver not installed), LightCrosshair falls back to a null driver service. All GPU driver features are disabled with explanatory tooltips. The app will not crash.

---

## Frame Cap Assistant

Located in the FPS & Performance tab, the Frame Cap Assistant provides **guidance only** — it does not enforce or apply an actual frame rate cap.

### Refresh Rate

- **What it does:** Sets the display refresh rate used for FPS target recommendations.
- **Range:** 30–500 Hz.
- **UI control:** Slider.
- **Default:** 60 Hz (or whatever `FrameCapAssistant.DefaultRefreshRateHz` evaluates to).
- **Recommendation:** Set this to your monitor's actual refresh rate.

### Target FPS

- **What it does:** The FPS value you want to target. The assistant uses this to recommend a cap.
- **Range:** 15–1000 FPS.
- **UI control:** Slider.
- **Default:** Based on the refresh rate recommendation.

### Use Recommendation

- **What it does:** Automatically sets the target FPS to a recommended value based on the current refresh rate (e.g., for 60 Hz it recommends 60 FPS, for 144 Hz it may recommend 141 FPS to stay below the tear line with V-Sync off).
- **UI control:** Button labelled "Use Recommendation".

### Status Text

- **What it shows:** The assistant's current status, recommended target FPS, and help text.
- **UI control:** Read-only text label.
- **Example:** `Assistant only: No active limiter backend. Suggested target: 141 FPS.`

---

## App Preferences (Not in UI)

These settings are stored in `%APPDATA%\LightCrosshair\prefs.json` and are not directly exposed in the Settings window, but affect the application behaviour:

| Setting               | Description                                      | Default |
|-----------------------|--------------------------------------------------|---------|
| Theme                 | Dark or Light theme for the Settings window.     | Dark    |
| Window X/Y            | Last position of the Settings window.            | Auto    |
| Window Width/Height   | Last size of the Settings window.                | 1020×500 |
| First Run Done        | Whether the first-run setup has been completed.  | false   |
| Overlay Visible       | Whether the crosshair overlay is currently shown. | true   |
| Last Profile ID       | The last active profile ID for restoration.      | (empty) |

Toggle the theme using the 🌙/☀ button in the top-left corner of the Settings window.

---

## Advanced / Troubleshooting

### Crosshair Not Visible

1. **Check the enable checkbox:** Make sure "Enable Custom Crosshair" is checked in the Crosshair Builder tab.
2. **Check the tray icon:** Right-click the LightCrosshair tray icon and verify "Show Crosshair" is checked.
3. **Check the default hotkey:** Press `Alt + X` to toggle visibility.
4. **Fullscreen mode:** Exclusive fullscreen may hide the overlay. Switch to borderless windowed or windowed mode.
5. **Admin/elevation conflict:** See "App Requires Elevation (Error 740)" below.
6. **Antivirus/firewall:** Some security software may block overlay windows. Add an exception for LightCrosshair.

### FPS Overlay Not Showing

1. **Enable the overlay:** Check "Enable FPS Overlay" in the FPS & Performance tab.
2. **Check Display Mode:** Make sure it's set to "Minimal" or "Detailed" (not "Off").
3. **Check position:** Reset X/Y sliders to 10,10 to ensure the overlay isn't off-screen.
4. **Launch as administrator:** ETW trace sessions (the primary telemetry source) require administrative privileges. If LightCrosshair is not elevated, the overlay will show `SRC: None` or `SRC: RTSS` (if RTSS is available) and may not have data.
5. **RTSS fallback:** If ETW is unavailable, LightCrosshair attempts to read RTSS shared memory. Install RivaTuner Statistics Server (RTSS) for an alternative data source.
6. **No data / `FPS: --`:** If the overlay shows but has no data, the telemetry source may not be capturing present events for the current foreground process. Try switching to a different game or application.
7. **Ultra-lightweight mode:** If enabled, the overlay updates less frequently (every 500 ms) and hides many details.

### App Requires Elevation (Error 740)

When launching LightCrosshair, if you see an error about elevation (Error 740), the application needs to be run as administrator for certain features:

- **ETW-based FPS telemetry** requires administrative privileges to start an Event Tracing session.
- **Gamma/vibrance adjustments** via hardware API may require elevation depending on the display driver.

**Solutions:**
1. Right-click LightCrosshair.exe → "Run as administrator".
2. If you always want elevation, set the executable's compatibility setting: Right-click → Properties → Compatibility → "Run this program as an administrator".
3. If you don't need FPS telemetry or display adjustments, LightCrosshair works fine without elevation for the basic crosshair overlay.

### Settings Not Saving

1. **Check file permissions:** LightCrosshair stores settings in `%APPDATA%\LightCrosshair\`. Make sure this directory is writable.
2. **Reset settings:** Close LightCrosshair, then delete or rename the files in:
   - `%APPDATA%\LightCrosshair\crosshair_settings.json`
   - `%APPDATA%\LightCrosshair\prefs.json`
   
   The app will create fresh defaults on next launch.
3. **Antivirus:** Some antivirus software may prevent writing to the AppData folder. Add an exception for LightCrosshair.
4. **Multiple instances:** Running multiple instances of LightCrosshair may cause settings conflicts. Ensure only one instance is running.

### Display Settings Have No Effect

1. **Hardware compatibility:** See the note under "Enable Gamma/Contrast/Brightness/Vibrance Override" — these settings are experimental and may not work on all hardware configurations.
2. **HDR:** Windows HDR typically blocks per-application LUT adjustments. Disable HDR for display settings to work.
3. **Hybrid graphics:** Laptops with Optimus or MUX switches route the display through the integrated GPU, which may not support LUT modifications.
4. **Night Light / colour filters:** Active colour filters can override gamma adjustments.
5. **Check the backend info:** The Color Backend info box in the Display Settings tab shows whether the hardware path is active.

### Hotkey Issues

1. **Conflict with another app:** If a hotkey combination is already registered globally, LightCrosshair's registration will fail silently. Try a different combination.
2. **Hotkeys not working after sleep/resume:** Windows may not re-register hotkeys correctly after system resume. Restart LightCrosshair.
3. **Games block Win32 hotkeys:** Some games in exclusive fullscreen intercept hotkeys. Use borderless windowed mode, or try different modifier combinations.

### Profiles

- **Max 10 profiles:** LightCrosshair supports up to 10 profile slots. Attempting to add more will show an error.
- **Default profile is immutable:** The first profile slot is a "Default" profile that cannot be overwritten, renamed, or deleted.
- **Profile names must be unique:** Renaming a profile to an existing name will show a warning.

### Performance Overlay Labels Legend

For a quick reference of every label that can appear in the performance overlay, see the [FPS Overlay Labels Reference](#fps-overlay-labels-reference) section above.

---

## Storage Locations

| Data                  | Path                                                |
|-----------------------|-----------------------------------------------------|
| Crosshair settings    | `%APPDATA%\LightCrosshair\crosshair_settings.json` |
| App preferences       | `%APPDATA%\LightCrosshair\prefs.json`              |
| Profiles              | `%APPDATA%\LightCrosshair\Profiles\*.json`         |
| Error logs (if any)   | Application event log or debug output              |

---

## Related Files

| File | Purpose |
|------|---------|
| [`CrosshairConfig.cs`](../LightCrosshair/CrosshairConfig.cs) | Config model: crosshair properties, hotkeys, overlay, display, frame cap assistant |
| [`PreferencesStore.cs`](../LightCrosshair/PreferencesStore.cs) | App preferences (theme, window position, overlay state) |
| [`SettingsWindow.xaml`](../LightCrosshair/SettingsWindow.xaml) | WPF settings UI layout |
| [`SettingsWindow.xaml.cs`](../LightCrosshair/SettingsWindow.xaml.cs) | Settings window code-behind: wiring, save/load, event handlers |
| [`FpsOverlayForm.cs`](../LightCrosshair/FpsOverlayForm.cs) | FPS overlay rendering and text formatting |
| [`FpsOverlayRuntimePolicy.cs`](../LightCrosshair/FpsOverlayRuntimePolicy.cs) | Runtime policy logic for overlay display decisions |
| [`CrosshairProfile.cs`](../LightCrosshair/CrosshairProfile.cs) | Profile data model (shape, colours, display settings) |
| [`HotkeyManager.cs`](../LightCrosshair/HotkeyManager.cs) | Win32 global hotkey registration |
| [`CrosshairVisibilityPreset.cs`](../LightCrosshair/CrosshairVisibilityPreset.cs) | High-contrast visibility preset logic |
| [`SystemFpsMonitor.cs`](../LightCrosshair/SystemFpsMonitor.cs) | FPS telemetry: ETW capture, RTSS fallback, metrics buffer |
