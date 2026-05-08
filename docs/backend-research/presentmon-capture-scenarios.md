# PresentMon Real Capture Scenarios

Milestone 11 validates the offline parser against real local captures. Captures
are created manually outside LightCrosshair and stored under
`research/presentmon-captures/`, which is ignored by Git except for its README.

Use short captures first: 30 to 90 seconds per scenario is enough to confirm
column availability and classification behavior.

PresentMon Capture App CSV exports may contain duplicate column names, such as
multiple `MsPCLatency` headers. The offline analyzer normalizes later duplicate
headers deterministically (`MsPCLatency__2`, `MsPCLatency__3`, and so on), keeps
the first occurrence under the original name, and prints a warning listing each
mapping.

AMD real-capture observation: RX 6950 XT, driver 26.3.1, CoD DX12, 1440p
borderless, 360 Hz, VSync off, FreeSync off, and AntiLag 2 on produced native,
capped, FSR3 FG ON, and AFMF ON captures where `FrameType` was present but only
`Application` values were observed. The AFMF driver overlay may report
generated/displayed FPS that the PresentMon app/present metrics do not expose in
these captures. Classification remains `Inconclusive`, not verified, unless
PresentMon exposes explicit `Generated` or `Interpolated` frame evidence.

## Scenario Matrix

| Scenario | Expected useful columns | Useful signals | Heuristic-only signals | Verified evidence | Manual notes to record |
| --- | --- | --- | --- | --- | --- |
| Native / no frame generation | `Application`, `FPS`, `MsBetweenPresents`, `PresentMode`; `FrameType` if available | Baseline FPS/frame time, present mode, no generated `FrameType` values | Stable high FPS, low frame time, cadence regularity | `FrameType` present and consistently application/rendered, or no generated/interpolated values with known FG-off state | Game/app, settings, resolution, refresh rate, VRR/VSync, cap state, PresentMon version |
| Capped FPS | `FPS`, `MsBetweenPresents`, `PresentMode`, dropped/late indicators | Whether measured FPS converges to cap; present mode under cap | FPS near half/third refresh, alternating intervals | None for frame generation unless `FrameType` explicitly says generated/interpolated | Cap source: game, driver, RTSS, VSync, or external tool; target FPS; observed average |
| High-refresh uncapped | `FPS`, `MsBetweenPresents`, `MsBetweenDisplayChange`, `PresentMode` | Native high-refresh false-positive guard; p95/p99 stability | FPS/display ratios that resemble generated output | None unless `FrameType` explicitly reports generated/interpolated | Monitor refresh, VRR state, uncapped setting, workload variability |
| DLSS Frame Generation | `FrameType`, FPS-App/FPS-Presents/FPS-Display style columns, timing columns | Explicit generated/interpolated `FrameType`, app vs display/present FPS delta | FPS ratios, alternating cadence, vendor overlay state alone | Dedicated `FrameType` value showing generated/interpolated frames for current capture | GPU, driver, game, DLSS-G/MFG mode, FG off/on pair, Reflex/VSync/VRR settings |
| FSR Frame Generation | `FrameType` if provider supports it, FPS-App/FPS-Presents/FPS-Display style columns | Explicit generated/interpolated `FrameType` if present; app vs display/present delta as supporting evidence | FSR setting text, FPS doubling, cadence alone | Dedicated `FrameType` value showing generated/interpolated frames for current capture | Game, FSR version/mode, GPU/driver, FG off/on pair, display mode |
| AMD AFMF | `FrameType` if driver/provider supports it, FPS-App/FPS-Presents/FPS-Display style columns | Explicit generated/interpolated `FrameType` if exposed; present/display deltas as supporting evidence | Adrenalin toggle, profile state, FPS ratios, cadence alone | Dedicated `FrameType` value showing generated/interpolated frames for current capture | AFMF version, driver, HYPR-RX/profile state, fullscreen/borderless mode, FG off/on pair |
| Unsupported / unknown game | `Application`, timing columns, present mode | Parser robustness and unsupported classification | Any timing anomaly without `FrameType` | None unless dedicated generated/interpolated `FrameType` appears | Why game is unsupported/unknown, capture issues, missing columns |
| Desktop / non-game false positive | `Application`, timing columns, present mode | Verify desktop/video/browser captures do not become verified FG | Video cadence, desktop compositor behavior, refresh-rate patterns | None unless a dedicated provider column explicitly reports generated/interpolated frames | App name, monitor refresh, video playback/browser state, DWM/compositor notes |

## Capture Notes Template

```text
Scenario:
PresentMon version:
PresentMon command/capture mode:
Game/app:
Game/app version:
GPU/vendor:
Driver version:
Display mode:
Refresh rate:
VRR/VSync:
Frame cap source and target:
Frame generation setting:
Capture duration:
Columns observed:
Parser classification:
Average FPS:
P95/P99 frame time:
Present modes:
Dropped/late/tearing:
Known false positives/negatives:
Decision: usable / not usable / needs more data
Notes:
```

## Classification Rules

- `VerifiedSignalPresent` requires a dedicated recognized column/value such as
  `FrameType=Generated`, `FrameType=Interpolated`, or an equivalent documented
  PresentMon value.
- FPS ratios, high display FPS, app/display deltas, and cadence patterns are
  useful research signals, but remain heuristic without explicit frame type.
- Missing `FrameType` means frame-generation evidence is unavailable or
  heuristic-only, not verified.
- Driver UI, vendor overlay text, game settings, or RTSS frame time can support
  manual notes but must not become parser-verified evidence.

## Validation Matrix

Copy rows from this table as local captures are analyzed. Keep personal data out
of committed summaries.

| Scenario | PresentMon version | Game/app | GPU/vendor | Columns observed | Classification result | Confidence | Known false positives/negatives | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Native / no frame generation | TBD | TBD | TBD | TBD | TBD | TBD | TBD | needs more data |
| Capped FPS | TBD | TBD | TBD | TBD | TBD | TBD | TBD | needs more data |
| High-refresh uncapped | TBD | TBD | TBD | TBD | TBD | TBD | TBD | needs more data |
| DLSS Frame Generation | TBD | TBD | NVIDIA | TBD | TBD | TBD | TBD | needs more data |
| FSR Frame Generation | TBD | TBD | AMD/NVIDIA/Intel | TBD | TBD | TBD | TBD | needs more data |
| AMD AFMF | TBD | TBD | AMD | TBD | TBD | TBD | TBD | needs more data |
| Unsupported / unknown game | TBD | TBD | TBD | TBD | TBD | TBD | TBD | needs more data |
| Desktop / non-game false positive | TBD | TBD | TBD | TBD | TBD | TBD | TBD | needs more data |
