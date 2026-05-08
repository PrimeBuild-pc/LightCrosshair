# LightCrosshair 1.4.0 - Manual Telemetry Validation Checklist

Milestone: 6.

This checklist validates LightCrosshair diagnostics only. It does not require
RTSS profile writes, driver profile writes, native hooks, injection, target
process DLLs, packaging publication, or release artifacts.

## Safety Rules

- Test only games or local sample apps you are allowed to inspect.
- Prefer single-player, offline, or developer-owned targets.
- Do not attach native tools to anti-cheat protected, multiplayer, unknown, or
  arbitrary third-party processes.
- Use RTSS, PresentMon, FrameView, or vendor overlays only in read-only
  observation modes unless a later milestone explicitly approves writes.
- Do not call LightCrosshair telemetry a frame cap or latency reduction.

## Baseline Setup

1. Start LightCrosshair normally.
2. Enable the FPS overlay.
3. Choose a known target process if validating target filtering.
4. Keep frame generation display enabled only when testing its labels.
5. Record the game/app, renderer API if known, display refresh rate, VRR/VSync
   state, graphics preset, and whether RTSS or external tools are running.

## Normal FPS Overlay

Expected:

- `FPS` updates while the target is presenting frames.
- `AVG` and `1% LOW` remain plausible for the current workload.
- `SRC` is `ETW` when ETW telemetry is available.
- `SRC` may be `RTSS` only as a fallback source.
- When no frames are available, the overlay stays conservative with idle or
  waiting status instead of inventing values.

Compare, if available:

- PresentMon read-only FPS output.
- RTSS OSD FPS/frametime readout.
- Vendor overlay FPS as a coarse sanity check.

## Advanced FPS Diagnostics

Enable advanced diagnostics and validate:

- `FT AVG` tracks average frame time.
- `0.1% LOW` drops during real stutter or heavy scenes.
- `JIT` and `SD` rise when frame intervals are unstable.
- `HITCH` increments only for long frame intervals above the configured
  threshold.
- `PACE` is higher for stable pacing and lower for jittery workloads.

Known limitations:

- ETW permissions and driver behavior can change source availability.
- Menus, loading screens, background focus changes, and overlays can produce
  misleading frame intervals.
- RTSS frame-time-only telemetry is not enough to verify generated frames.

## Frame Generation Labels

Expected states:

- `FG: UNKNOWN`: not enough useful evidence.
- `FG: OFF`: enough telemetry exists and no plausible generated-frame pattern
  was found.
- `FG: SUSPECT <n>%`: cadence or app/display FPS ratio suggests generated
  frames, but the signal is heuristic.
- `FG: VERIFIED ...`: only allowed when a verified provider signal exists.

Non-diagnostic generated-frame counts:

- `GEN EST` is allowed for heuristic estimates.
- `GEN` without `EST` is allowed only for verified generated-frame data.

False positives to watch:

- Native high-refresh rendering.
- FPS caps near half, third, or quarter refresh.
- VRR/VSync pacing changes.
- Menus, duplicated presents, loading screens, or CPU-bound scenes.
- External limiters changing cadence.

False negatives to watch:

- Frame generation active but generated frames are dropped.
- Provider/tool does not expose frame type or app/display FPS split.
- RTSS fallback is the only available source.

## Frame Limiter Backend State

Milestone 6 should report the frame limiter backend as unavailable/no-op unless
a future real backend exists.

Expected:

- Backend kind is `None` for the current safe default.
- Status is `Unavailable` or inactive.
- Active is `No`.
- Telemetry validated is `No`.
- Evidence says LightCrosshair is not applying a frame cap.

Do not treat a stable FPS value as proof that LightCrosshair capped the target.
The cap may come from the game, driver, RTSS, VSync, VRR policy, or another
external component.

## Diagnostic Report Export

The Milestone 6 diagnostic report helper can format an existing telemetry
snapshot for logs or manual export. A report should include:

- capture timestamp;
- telemetry source and status;
- FPS, average FPS, 1% low, and 0.1% low;
- latest and average frame time;
- jitter, standard deviation, hitch count, and stability score;
- frame generation state, confidence, verification flag, and evidence;
- frame limiter backend kind, status, active flag, validation flag, and
  evidence.

Reports are local diagnostic data. Review them before sharing because process
names, tool names, timestamps, and local test notes can still reveal context.

## Suggested Read-Only Comparison Runs

1. Uncapped native rendering:
   - Use a stable scene.
   - Confirm LightCrosshair, PresentMon, and RTSS are broadly aligned.
2. Game-internal cap:
   - Apply a cap inside the game settings only.
   - Confirm LightCrosshair reports changed telemetry but not an active
     LightCrosshair frame limiter.
3. RTSS read-only observation:
   - If RTSS is already configured by the user, observe its OSD and shared
     memory telemetry.
   - Do not let LightCrosshair write RTSS profiles in this milestone.
4. Frame generation off/on:
   - Use a known title or sample where frame generation can be toggled safely.
   - Compare LightCrosshair `Suspected`/`Verified` labels with read-only
     PresentMon/FrameView/vendor overlay data.

## Pass Criteria

- Overlay values are plausible and source/status labels match the active
  telemetry path.
- Advanced diagnostics respond to stable vs jittery workloads.
- Heuristic frame generation is never labeled verified.
- Current frame limiter backend is never reported active.
- No external profile, driver, native, hook, injection, packaging, release, tag,
  or push action is performed.
