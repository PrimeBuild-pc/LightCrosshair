# LightCrosshair 1.4.0 - SpecialK Components Mapping

Milestone: 4A feasibility, updated through 5 frame limiter backend scaffolding.

This document maps observed behavior in `SpecialK-components` to what LightCrosshair can implement safely. It does not copy Special K code. The goal is to separate real capabilities from diagnostics, advisory UI, and invasive techniques.

## Files Analyzed

- `SpecialK-components/frame-cap-SK.md`
- `SpecialK-components/overlay-latency-sync-SK.md`
- `SpecialK-components/framerate.cpp`
- `SpecialK-components/framerate.h`
- `SpecialK-components/frame_pacing.cpp`
- `SpecialK-components/latency.cpp`
- `SpecialK-components/reflex.cpp`
- `SpecialK-components/dxgi.cpp`
- `SpecialK-components/d3d9.cpp`
- `SpecialK-components/render_backend.cpp`
- `SpecialK-components/scheduler.cpp`
- `SpecialK-components/text.cpp`
- `SpecialK-components/widget.cpp`
- `SpecialK-components/cfg_osd.cpp`
- `SpecialK-components/config.cpp`
- `SpecialK-components/MainThread.cpp`
- `SpecialK-components/limit_reset.inl`

## Capability Table

| Capability | Category | Special K reference | What it really does | Technical prerequisites | Native code | Injection/hook | Anti-cheat/stability risk | License risk | Real test |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FPS and frametime overlay diagnostics | Direct C# out-of-process | `text.cpp`, `frame_pacing.cpp`, `framerate.h` stats | Displays measured FPS, frametime, percentiles, hitches, and pacing stats. | ETW Present events, RTSS shared memory, or local frame samples. | No | No | Low | Low if reimplemented originally | Run known rendering workload and compare overlay with ETW/PresentMon/RTSS. |
| Present telemetry via ETW/PresentMon-style events | Direct C# out-of-process | `MainThread.cpp`, `frame_pacing.cpp`, `config.cpp` `EnableETWTracing` | Observes present events and present mode/latency metadata where available. | Admin or sufficient ETW privileges; Microsoft-Windows-DxgKrnl provider. | No | No | Low | Low if based on Windows ETW docs and original code | Launch a sample renderer; verify present intervals and source status. |
| RTSS shared-memory read diagnostics | Direct C# out-of-process | `latency.cpp` mentions RTSS detection; LightCrosshair already reads RTSS memory | Reads RTSS-reported frame time when RTSS is present. | RTSS running and shared memory initialized. | No | No | Low | Low | Compare RTSS OSD frame time with LightCrosshair source `RTSS`. |
| Real frame cap through RTSS | External integration | Special K references RTSS as external hook context in `scheduler.cpp`; no direct LightCrosshair writer exists | Lets RTSS perform the actual in-process limiting. LightCrosshair would configure a per-app RTSS profile if supported safely. | RTSS installed/running; supported RTSS profile interface, CLI, or config file update. | No for LightCrosshair, RTSS itself uses native hooks | Yes, by RTSS not LightCrosshair | Medium: third-party hook and anti-cheat exposure depends on target game | Low for LightCrosshair if no GPL code copied | Apply RTSS cap to a controlled sample app and verify ETW FPS converges to target. |
| Driver profile frame cap via vendor API | External/native integration | `reflex.cpp`, `config.cpp` NVIDIA settings | Uses GPU driver/vendor APIs to configure max frame rate or low latency policy. | Vendor GPU, vendor SDK/API, supported profile path. | Usually yes through native binding | No LightCrosshair injection | Medium: vendor-specific, may require profile reload/admin | Low if using vendor SDK license correctly | Apply cap to sample app, restart if required, verify ETW FPS and driver profile state. |
| In-process Present limiter | Native/hook | `framerate.cpp`, `framerate.h`, `core.cpp`, `dxgi.cpp`, `d3d9.cpp` | Waits at a render/present boundary using high-resolution timers and precise spin/sleep scheduling. | Native DLL inside target process, graphics API interception, swapchain tracking, IPC. | Yes | Yes | High, especially with anti-cheat and overlays | High if adapting GPL implementation; clean-room needed | Inject only into a local sample DXGI app; verify Present intervals and no double-wait. |
| Frame pacing tied to swapchain/present mode | Native/hook | `framerate.cpp`, `dxgi.cpp`, `render_backend.cpp` | Chooses where to delay frames and reacts to present mode, vblank, queue depth, and missed frames. | In-process access to swapchain/backend state. | Yes | Yes | High | High if porting logic | Controlled DXGI/D3D9/Vulkan sample with cap on/off and ETW comparison. |
| Latent Sync / tearline control | Native/hook | `framerate.cpp`, `widget.cpp`, `config.cpp` `FrameRate.LatentSync` | Coordinates frame timing with scanline/vblank and may skip frames for tearline placement. | In-process present path, D3DKMT scanline/vblank APIs, display mode knowledge. | Yes | Yes | High | High | Dedicated sample app on known display; high-speed/FCAT/PresentMon validation. |
| NVIDIA Reflex markers and sleep | Native/hook/vendor API | `reflex.cpp`, `latency.cpp`, `config.cpp` `NVIDIA.Reflex` | Places latency markers, controls Reflex sleep mode, fetches pipeline latency reports. | NVIDIA GPU, NVAPI, render device pointer, correct per-frame marker order. | Yes | Usually yes unless the target app explicitly exposes integration points | High if injected into games | High if adapting Special K behavior; clean-room/vendor docs required | Instrumented local D3D sample with NVAPI; verify latency reports and marker validity. |
| DLSS Frame Generation status and multi-frame count | Native/hook/vendor API | `framerate.h`, `text.cpp`, `cfg_osd.cpp`, `core.cpp`, `control_panel.cpp`, `reflex.cpp`; fallback implementation reference: `SpecialK-main/src/render/ngx/ngx.cpp`, `ngx_d3d11.cpp`, `ngx_d3d12.cpp`, `ngx_vulkan.cpp` | Reports DLSS/FG only when in-process NGX/Streamline/plugin state says DLSS-G is active, then uses `DLSSG.MultiFrameCount + 1` for OSD/pacing decisions. It detects active FG state, not a generic per-present generated-frame classifier. | NGX feature creation/evaluation state, `Enable.OFA`, `DLSSG.EnableInterp`, `DLSSG.NumFrames`, `DLSSG.MultiFrameCount`, recent frame-generation feature use, render backend context. | Yes for LightCrosshair unless using external app cooperation | Usually yes | High if injected into games | High if adapting Special K probing; clean-room/vendor docs required | Known DLSS-G title or sample app; compare app state, PresentMon/FrameView display FPS, and reported FG multiplier. |
| AMD AFMF / FSR FG direct state | External/native integration | `render_backend.cpp` has display/VRR capability handling, but no out-of-process AFMF truth signal in selected components | No direct AFMF/FSR FG per-frame signal was found in selected components. Detection must come from driver/provider instrumentation, app cooperation, or heuristics. | PresentMon frame type/provider data, AMD/vendor API if available, or game/plugin signal. | Possibly | Possibly | Medium to High depending path | Low if using public provider/API; high if adapting GPL or injecting | Known AFMF/FSR FG title; compare PresentMon FPS-App/FPS-Display/frame type and AMD overlay/profile state. |
| Render queue / prerender limit manipulation | Native/vendor integration | `config.cpp`, `framerate.cpp`, `dxgi.cpp` | Changes CPU/GPU queueing behavior, waitable swapchain usage, or driver low latency policy. | Swapchain/device access or vendor driver API. | Yes for reliable implementation | Often yes | Medium to High | Medium to High | Sample renderer with queue depth telemetry before/after. |
| Sleep/QPC/timer detours | Not recommended for LightCrosshair default | `scheduler.cpp`, `framerate.cpp` | Detours process scheduling APIs to influence limiter precision and timing behavior. | Native code in target process and function detours. | Yes | Yes | High | High | Only in isolated sample process if ever researched. |
| Reflex native override or disabling game Reflex | Not recommended | `reflex.cpp`, `latency.cpp`, `config.cpp` | Overrides or suppresses a game's native Reflex behavior. | Deep in-process NVAPI interception. | Yes | Yes | Very high | High | Not suitable for LightCrosshair 1.4.0. |
| DLSS/DLSS-G pacing or DLL redirection | Not recommended | `reflex.cpp`, `render_backend.cpp`, `config.cpp` DLSS/Streamline keys | Interacts with Streamline/DLSS frame generation and native pacing. | Vendor SDK, in-process hooks, DLL redirection in some modes. | Yes | Yes | Very high | High | Out of scope for LightCrosshair. |

## Category Summary

### A. Direct C# out-of-process

LightCrosshair can safely extend diagnostics and verification:

- ETW present telemetry.
- RTSS shared-memory read-only source.
- Display refresh and foreground process correlation.
- Frame pacing statistics, stability scores, hitches, percentiles.
- Validation that an external/native limiter actually changed observed FPS.
- Conservative frame-generation suspicion from timing patterns, clearly labeled as heuristic.

This category cannot implement a real frame cap for a game process by itself because LightCrosshair does not control the target render loop.

### B. External integration

The most practical first real limiter path is an RTSS integration where RTSS performs the actual limiting. LightCrosshair would only manage capability detection, profile configuration, and telemetry validation.

Driver/vendor profile integration is also possible, but it is hardware-specific and should be behind capability checks.

PresentMon 2.x style frame-type or FPS-App/FPS-Presents/FPS-Display data is the safest external route for verified or near-verified frame-generation evidence. LightCrosshair should not treat cadence-only ETW heuristics as a verified NVIDIA/AMD signal.

### C. Native/hook component

Special K's strongest functionality sits here: swapchain-aware limiter placement, precise present pacing, Latent Sync, Reflex markers, render queue control, and sleep/timer detours. A real LightCrosshair equivalent would need a separate native component and a deliberate security/anti-cheat policy.

Milestones 4D and 5 did not implement this category. They only added managed
provider/backend boundaries and no-op/unavailable implementations for future
verified frame-generation evidence and frame-limiter backends.

Milestone 5 frame limiter scaffolding remains non-invasive: interfaces,
capability models, no-op/unavailable backend behavior, and documentation only.
It does not implement a Special K-like limiter or any target-process control.

### D. Not recommended

LightCrosshair should not claim or ship broad game injection, Reflex override, DLSS redirection, or scheduler detours as a casual 1.4.0 feature. These are high-risk technically and legally.

Hard stop gates for future native research:

- no injection, present hooks, timer/scheduler detours, DLL redirection, Reflex
  overrides, Streamline/NGX interception, or process memory inspection without a
  separate approved design;
- no attachment to anti-cheat protected, multiplayer, unknown, or arbitrary
  third-party game processes;
- native experiments must be limited to an owned local sample renderer until a
  later milestone explicitly approves broader testing;
- no process module scraping, overlay text scraping, window-title collection, or
  command-line/path inventory for frame-generation detection.

## What Not To Promise

- Do not call overlay-only recommendations a frame cap.
- Do not call ETW/RTSS observations latency reduction.
- Do not claim Reflex support without NVAPI markers/sleep/report integration.
- Do not claim frame pacing control unless LightCrosshair controls the target frame boundary through RTSS, driver API, or native in-process code.
- Do not claim DLSS-G, MFG, FSR FG, or AFMF detection unless the source is a verified provider/API signal. Cadence-only analysis must be labeled `Suspected`.
