# LightCrosshair 2.0 — Review 3 (Lorenzo's first in-game test)

Date: 2026-11-16. Build from `v2` before round 4. Machine: Ryzen 7 5800X3D, RX 6950 XT, 2560×1440 + 1080×1920.

## Observed
| Id | Report | Diagnosis |
|---|---|---|
| G-1 | Alt+C does not cycle crosshairs. | Works as coded but useless: the store has one user crosshair, and presets are not part of the cycle. |
| G-2 | Clicking a crosshair card in the Library does nothing. | Apply is only in the card menu. Users expect click = use it. |
| G-3 | Alt+X seemed not to work in game (Alt+L did). | Alt+X verified working on the desktop through the real Core (state toggles and persists); no hotkey errors registered. Pending: whether the crosshair is visible at all over Fortnite. |
| G-4 | Opening the UI with Alt+L has a visible delay; a frametime graph in game shows a spike when the window opens. Closing is instant. | Cold start of a WPF process (~550 ms) plus Core rendering 30+ thumbnails at once on connect, all at normal priority while a game runs. |
| G-5 | First open was maximized. | Probably a stale `ui-placement.json` from earlier test runs (current file: normal 1180×740). Re-check after round 5. |
| G-6 | Display colors for Fortnite did nothing. | `core.log`: `Display color transaction: InvalidOperationException: Color apply failed` on the AMD path; the failure is not shown to the user. The log is also flooded with message-less lines (`AmdAdlColorManager` ×20). |

## Decisions
1. **Presets are first-class crosshairs** (CONTRACT §7): they can be default, assigned to games and cycled without being copied.
2. **Click a card = use it** now (sets the default, or the selected game's crosshair when opened from a game profile). Edit is a separate button on the card (and double-click).
3. **Alt+C/Alt+V always do something** and the choice persists (CONTRACT §7).
4. **Opening the UI must feel instant and not disturb the game**:
   - Core starts the UI at below-normal CPU priority and low I/O priority while a game is in the foreground; the UI raises itself to normal once its window has focus and no game is foreground.
   - The UI paints its window first and loads everything else after (snapshot, presets, thumbnails only for visible cards).
   - Core renders thumbnails on one below-normal background thread, never more than one at a time.
   - After the window closes the UI process stays parked (hidden, working set trimmed) for 5 minutes, so reopening is instant; Alt+L during that time only shows the window. After 5 minutes it exits.
   - Targets on the review machine: cold open ≤ 300 ms to first frame, warm (parked) open ≤ 50 ms, no frametime spike visible in a frametime graph (e.g. AMD Adrenalin metrics) when opening the UI in game; measure it with PresentMon/ETW frame times of a test 3D app before and after.
5. **Display-color errors are visible** (`RuntimeState.DisplayColorsError`) and the AMD apply failure is investigated.
