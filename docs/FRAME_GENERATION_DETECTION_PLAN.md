# LightCrosshair 1.4.0 - Frame Generation Detection Feasibility

Milestone: 4B feasibility only.

This document defines a real, testable path for improving generated/interpolated frame detection in LightCrosshair without hooks, injection, or GPL code reuse.

## Current LightCrosshair State

Files analyzed:

- `LightCrosshair/SystemFpsMonitor.cs`
- `LightCrosshair/FpsOverlayForm.cs`
- `LightCrosshair/CrosshairConfig.cs`
- `LightCrosshair/SettingsWindow.xaml`
- `LightCrosshair/SettingsWindow.xaml.cs`
- `LightCrosshair/ProfileService.cs`
- `LightCrosshair.Tests/FpsMetricsBufferTests.cs`

Current behavior:

- `ShowGenFrames` defaults to `true` and the settings UI labels it experimental.
- `SystemFpsMonitor` stores generated-frame flags in `FpsMetricsBuffer`.
- ETW frames are marked by `InferGeneratedFrameEtw`.
- RTSS frames are always passed as `isGeneratedFrame: false`.
- The overlay prints `GEN: N/A`, `GEN: OFF`, or `GEN: <count>`.

Current detection weakness:

- The ETW heuristic only checks stable sub-16 ms frame cadence and alternates generated-frame tagging.
- It does not know whether DLSS-G, MFG, FSR FG, or AFMF is active.
- It cannot distinguish a real generated frame from a high-refresh native game, limiter behavior, duplicate presents, VRR pacing, menu/cutscene cadence, or CPU-bound timing changes.
- It should be treated as `Suspected` at best, not `Detected`.

## SpecialK-components Findings

Files analyzed:

- `SpecialK-components/text.cpp`
- `SpecialK-components/cfg_osd.cpp`
- `SpecialK-components/core.cpp`
- `SpecialK-components/control_panel.cpp`
- `SpecialK-components/reflex.cpp`
- `SpecialK-components/render_backend.cpp`
- `SpecialK-components/frame_pacing.cpp`
- `SpecialK-components/latency.cpp`
- `SpecialK-components/framerate.cpp`
- `SpecialK-components/framerate.h`
- `SpecialK-components/config.cpp`
- `SpecialK-components/overlay-latency-sync-SK.md`
- `SpecialK-components/frame-cap-SK.md`

Special K does not infer DLSS Frame Generation from frame cadence alone. Relevant behavior is in-process:

- `text.cpp` shows DLSS/FG only when NGX/DLSS-G state reports it.
- `core.cpp` updates DLSS-G status inside D3D11/D3D12 frame handling.
- `control_panel.cpp` adjusts VRR/LFC and VSYNC UI behavior when `__SK_IsDLSSGActive` changes.
- `reflex.cpp` alters Reflex sleep intervals when DLSS-G is active and accounts for multi-frame count.
- `render_backend.cpp` contains render backend and display capability logic, but not an out-of-process AMD AFMF truth signal.

SpecialK-main was not used.

## External References

- NVIDIA Streamline DLSS-G documentation says DLSS-G can present additional frames and exposes runtime status through `slDLSSGGetState`; it also warns that `MsBetweenPresents` is insufficient for pacing quality and recommends display-change timing.
- NVIDIA Streamline notes that DLSS Frame Generation adds extra Present calls and exposes `DLSSGState::numFramesActuallyPresented` for engine-side FPS reporting.
- PresentMon console supports `--track_frame_type`, but it requires application and/or driver instrumentation through the Intel-PresentMon provider.
- PresentMon releases added FPS-App/FPS-Presents/FPS-Display indicators for frame-generation scenarios.
- AMD documents AFMF as driver-integrated frame generation in AMD Software: Adrenalin Edition; LightCrosshair cannot assume a public out-of-process per-frame AFMF truth signal from current local code.

## Detection Strategy Categories

| Strategy | Category | NVIDIA DLSS-G/MFG | AMD FSR FG/AFMF | Confidence | Requirements | Risk |
| --- | --- | --- | --- | --- | --- | --- |
| Streamline/NGX in-process state | D. Native/in-process | Directly verifies DLSS-G status and multi-frame count | Not applicable | High | Hook/in-process SDK access or app cooperation | High anti-cheat, native complexity |
| PresentMon `FrameType` / Intel-PresentMon provider | A/B. Verified external signal when available | Possible if provider reports frame type | Possible for driver/app-instrumented stacks | High if frame type present | PresentMon API/service/CSV with frame type tracking | Medium dependency/version risk |
| PresentMon FPS-App vs FPS-Presents vs FPS-Display | B. Derived external signal | Strong indicator when FPS-Display > FPS-App | Strong indicator for driver/app FG when exposed | Medium to High | PresentMon 2.x API/service or process capture | Medium: not a per-frame vendor truth signal |
| RTSS shared memory extended fields | B/E. Depends on verified structure | Unknown from current LightCrosshair parser | Unknown | Unknown | Documented RTSS fields beyond frame time | Risky if undocumented |
| ETW present cadence heuristic | C. Probabilistic | Can suspect generated frames | Can suspect generated frames | Low to Medium | Existing ETW timestamps, display refresh data, confidence model | False positives likely |
| GPU busy vs display cadence heuristic | C. Probabilistic | Can support suspicion | Can support suspicion | Medium with PresentMon metrics | PresentMon GPU busy/display metrics | Misleading under HWS/driver quirks |
| Vendor driver profile/status API | B/D. External/native | May detect Smooth Motion or app profile state, not per-frame DLSS-G | May detect AFMF/HYPR-RX profile state, not per-frame output | Medium | Vendor SDK/API, driver support | Vendor-specific, may not prove active frames |
| Overlay text / process module scan | E. Not recommended | Can find DLLs but not active FG | Can find DLLs but not active FG | Low | Process inspection | False positives, privacy/anti-cheat concern |

## NVIDIA Detection Map

### What LightCrosshair can detect today

- ETW present cadence only.
- A suspicious 2x-ish or MFG-ish presentation ratio relative to base cadence only if enough samples exist.
- No verified DLSS-G/MFG active state.

### What needs external tools/APIs

- PresentMon frame type or FPS-App/FPS-Presents/FPS-Display metrics.
- NVIDIA FrameView can be used for manual validation, especially display-change timing.
- Vendor driver/app profile state may support capability detection, not per-frame confirmation.

### What needs native/in-process

- Streamline `slDLSSGGetState`.
- `numFramesToGenerate`, dynamic MFG support, runtime status, and actual frames presented.
- Reflex/latency marker correlation.

### Likely false positives

- Native 120/144/240/360 Hz rendering.
- FPS caps near half or quarter refresh.
- VRR or VSync pacing.
- Menu frame duplication.
- CPU-bound cases where Present timing changes when FG is enabled.
- DLSS-G active but interpolated frames dropped or not producing the expected ratio.

### Manual validation

- Use a known DLSS-G game with FG off/on at the same settings.
- Capture with PresentMon 2.x or FrameView.
- Compare FPS-App, FPS-Presents, FPS-Display, `FrameType` if available, and LightCrosshair suspicion/confidence.
- Validate MFG separately by testing x2/x3/x4 modes where supported.

## AMD Detection Map

### What LightCrosshair can detect today

- ETW/RTSS presented cadence only.
- No direct AFMF/FSR FG state.
- No verified AMD generated-frame count.

### What needs external tools/APIs

- PresentMon frame type or FPS-App/FPS-Presents/FPS-Display metrics if available for the driver/app stack.
- AMD Software/driver state could indicate AFMF enabled for the profile, but that is not enough to prove generated frames are currently delivered.

### What needs native/in-process

- FSR FG integration state from the game/plugin if exposed.
- Driver-level AFMF state only if AMD provides a suitable API or instrumentation path.

### Likely false positives

- AFMF enabled globally but disabled by motion, unsupported API mode, VSync, or driver policy.
- FSR FG in-game vs AFMF driver FG both affecting cadence differently.
- Borderless/fullscreen and presentation model changes.
- RTSS or driver caps changing observed cadence.

### Manual validation

- Use a known AMD-supported AFMF game/profile and a known FSR FG game.
- Capture off/on runs with PresentMon 2.x.
- Compare FPS-App/FPS-Presents/FPS-Display and any frame type output.
- Confirm AMD overlay or Adrenalin state separately, but do not treat it as per-frame proof.

## Proposed Architecture

Add a dedicated detector layer instead of embedding generated-frame logic in `SystemFpsMonitor`.

Suggested files for Milestone 4C:

- `LightCrosshair/FrameGeneration/FrameGenerationStatus.cs`
- `LightCrosshair/FrameGeneration/FrameGenerationDetector.cs`
- `LightCrosshair/FrameGeneration/FrameGenerationEvidence.cs`
- `LightCrosshair/FrameGeneration/FrameGenerationTelemetryWindow.cs`
- `LightCrosshair.Tests/FrameGenerationDetectorTests.cs`

Suggested status model:

```csharp
public enum FrameGenerationState
{
    Unknown,
    Unsupported,
    NotDetected,
    Suspected,
    Detected
}
```

Suggested output:

```csharp
public readonly record struct FrameGenerationStatus(
    FrameGenerationState State,
    double Confidence,
    string Vendor,
    string Technology,
    double PresentedFps,
    double? AppFps,
    double? DisplayFps,
    double? EstimatedGeneratedRatio,
    int GeneratedFrameCount,
    string Evidence,
    bool IsVerifiedSignal);
```

Detection rules:

- `Detected` requires a verified external or in-process signal such as frame type, Streamline state, or a trusted PresentMon provider field.
- `Suspected` can use heuristics from ETW/RTSS/PresentMon timing.
- `Unknown` is used when telemetry is insufficient.
- `Unsupported` is used when the active source cannot produce useful evidence.
- `NotDetected` requires enough samples and no verified or heuristic evidence.

Overlay changes for a later milestone:

- Replace `GEN: <count>` with state-aware output:
  - `FG: N/A`
  - `FG: OFF`
  - `FG: SUSPECT 62%`
  - `FG: DLSS-G x2`
  - `FG: AFMF?`
- Keep experimental labeling unless `IsVerifiedSignal` is true.

## Milestone 4C Implementable Without Hook/Injection

Milestone 4C can safely improve the current state without claiming full NVIDIA/AMD support:

1. Extract current ETW cadence logic into `FrameGenerationDetector`.
2. Add `FrameGenerationState`, confidence, evidence text, and verified/suspected distinction.
3. Rename behavior from generated-frame count to frame-generation status in telemetry internals.
4. Keep overlay conservative: show `SUSPECT` for heuristic results, not `Detected`.
5. Add unit tests for:
   - no data / insufficient samples
   - stable native high-FPS false positive guard
   - clear 2:1 cadence suspicion
   - MFG-like 3:1 or 4:1 cadence suspicion
   - jitter/outlier rejection
   - RTSS-only unsupported/unknown behavior unless richer source exists
6. Optionally add a `PresentMonIntegration` design stub only as documentation or interface, not a fake implementation.

## Requires Native/In-process

Reliable NVIDIA DLSS-G/MFG detection requires at least one of:

- Streamline/NGX state inside the target app.
- App cooperation exposing DLSS-G state.
- A native injected component that can query the render/Streamline context.

Reliable AMD FSR FG/AFMF detection requires at least one of:

- A game/plugin signal exposed to LightCrosshair.
- Driver/provider instrumentation surfaced through PresentMon or a supported AMD API.
- A native component that can observe the relevant render/driver integration.

These paths carry anti-cheat, stability, signing, testing, and licensing risks and should not be implemented in 4C.

## Licensing Notes

Special K can remain a behavioral reference only:

- Do not copy NGX/Streamline probing code.
- Do not copy OSD or control-panel logic.
- Do not copy frame pacing or Reflex integration logic.
- Implement detectors from public documentation and original telemetry models.

If a future native implementation follows Special K internals closely, it needs a GPL compatibility decision or a clean-room rewrite.

## References

- NVIDIA Streamline DLSS-G programming guide: https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS_G.md
- NVIDIA Streamline getting started FAQ: https://developer.nvidia.com/rtx/streamline/get-started
- PresentMon console documentation: https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md
- PresentMon releases: https://github.com/GameTechDev/PresentMon/releases
- AMD Fluid Motion Frames: https://www.amd.com/en/products/software/adrenalin/afmf.html
