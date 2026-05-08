# LightCrosshair 1.4.0 - SpecialK Components Mapping

Milestone: 4A feasibility only.

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

This category cannot implement a real frame cap for a game process by itself because LightCrosshair does not control the target render loop.

### B. External integration

The most practical first real limiter path is an RTSS integration where RTSS performs the actual limiting. LightCrosshair would only manage capability detection, profile configuration, and telemetry validation.

Driver/vendor profile integration is also possible, but it is hardware-specific and should be behind capability checks.

### C. Native/hook component

Special K's strongest functionality sits here: swapchain-aware limiter placement, precise present pacing, Latent Sync, Reflex markers, render queue control, and sleep/timer detours. A real LightCrosshair equivalent would need a separate native component and a deliberate security/anti-cheat policy.

### D. Not recommended

LightCrosshair should not claim or ship broad game injection, Reflex override, DLSS redirection, or scheduler detours as a casual 1.4.0 feature. These are high-risk technically and legally.

## What Not To Promise

- Do not call overlay-only recommendations a frame cap.
- Do not call ETW/RTSS observations latency reduction.
- Do not claim Reflex support without NVAPI markers/sleep/report integration.
- Do not claim frame pacing control unless LightCrosshair controls the target frame boundary through RTSS, driver API, or native in-process code.
