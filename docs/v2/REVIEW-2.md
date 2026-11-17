# LightCrosshair 2.0 — Review 2

Date: 2026-11-16. Reviewed `v2-core` (37f1104) and `v2-ui` (d9e5e88), merged into `v2`.

## Verified
- Release build of the whole solution: 0 warnings, 0 errors. Tests: 224 Core + 22 UI, all green.
- **C-1 fixed**: the published Core starts on a copy of a real v1.9 profile set (`default-immutable` + `working::<guid>` current state); ids preserved, default resolved, originals untouched.
- Core memory with the real UI, 8 open/close cycles on the Library page: 15.5 → 17.2, 17.4, 17.0, 17.5, 18.0, 18.4, 18.9, 19.4 MB. +2.5 MB after 5 cycles (budget +3: pass). Idle CPU 0.000 %.
- UI against real Core: Library cards whole, search placeholder, varied preset colors, left-aligned sidebar with icon, Studio preview fills the canvas, 2×2 arm toggles.
- No AI attribution in any commit.

## Remaining findings

### Core (Codex)
| Id | Severity | Finding | Required fix |
|---|---|---|---|
| C-8 | Medium | v1 `EnableCustomCrosshair = false` is migrated by hiding every layer. In v1 that flag was the global "crosshair on/off" switch. Real case: the user's active v1.9 profile had it off → after upgrade "Show crosshair" is on, nothing is drawn, and Studio shows a design whose only layer is hidden. | Never hide layers in migration. If the **active** v1 profile (current state, else last profile) had it false → `Settings.CrosshairVisible = false`. For other profiles ignore the flag. Update tests. |
| C-9 | Low | Memory still rises ~0.45 MB per cycle from cycle 3 to 8 when each UI session opens the Library (30+ `render` calls). | Soak 50 cycles with the Library page open in each session. Either show a plateau (numbers in the handoff) or fix the retention. |

### UI (Cursor)
| Id | Severity | Finding | Required fix |
|---|---|---|---|
| U-14 | Low | Studio preview shows a permanent vertical scrollbar even at 1×/2× when the content fits. | Scrollbars only when the zoomed render exceeds the area (`Auto`). |
| U-15 | Low | When every layer is hidden the preview is blank with no explanation. | Inline hint in the preview ("All layers are hidden") in 6 languages. |
| U-16 | Low | `ui-placement.json` ignores `LIGHTCROSSHAIR_DATA_ROOT`, so test runs write to the real profile. | Store it under `LIGHTCROSSHAIR_DATA_ROOT` when set. |

## Not verifiable on the review machine (manual, before release)
- Real games: automatic profile switching, every binding mode, hold bindings in game, FPS overlay with a game.
- NVIDIA hardware: FPS cap and Digital Vibrance (review machine is AMD).
- Display colors apply/restore on AMD with a game, forced kill → restore on next start.
- Monitor hot-plug and mixed DPI.
- Clean install, upgrade from 1.9 and uninstall on the Windows 11 test VM; installer signing.
