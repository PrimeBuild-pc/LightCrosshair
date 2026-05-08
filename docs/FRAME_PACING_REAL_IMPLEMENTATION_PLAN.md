# LightCrosshair 1.4.0 - Real Frame Cap, Pacing, and Latency Feasibility

Milestone: 4A feasibility only.

LightCrosshair currently has a safe out-of-process telemetry model. It can observe frame timing through ETW and RTSS shared memory, and it can display pacing diagnostics. That model is appropriate for overlay diagnostics, but it cannot directly limit or pace a target game's render loop.

## Current LightCrosshair Baseline

Relevant files:

- `LightCrosshair/SystemFpsMonitor.cs`
- `LightCrosshair/FpsOverlayForm.cs`
- `LightCrosshair/CrosshairConfig.cs`
- `LightCrosshair/SettingsWindow.xaml`
- `LightCrosshair/SettingsWindow.xaml.cs`
- `LightCrosshair.Tests/FpsMetricsBufferTests.cs`

Current capabilities:

- Tracks foreground process/window.
- Uses ETW Present events when available.
- Falls back to RTSS shared memory when ETW is unavailable or not producing frames.
- Computes average FPS, frametime stats, 1% low, 0.1% low, hitch count, jitter, and stability score.
- Displays advanced diagnostics only when `ShowFpsDiagnostics` is enabled.

Current limits:

- No code runs inside the target game process.
- No swapchain/device handle is available.
- No target render thread scheduling point is available.
- No target present queue or Reflex marker control is available.
- Therefore, no real frame cap or latency reduction is possible from the current C# overlay alone.

## Definitions

- Overlay diagnostics: Measures and displays what happened. It does not control frame production.
- Real frame cap: Actively limits the target process by delaying or throttling frame production/presentation.
- Real frame pacing: Controls when frames are submitted or presented so intervals are stable, not just lower on average.
- Latency measurement: Measures or estimates pipeline timing such as CPU submit, present, render queue, GPU work, or scanout.
- Latency reduction: Changes queueing, synchronization, frame timing, sleep, or render submission behavior to reduce input-to-photon latency.

## Recommended Architecture

### Layer 1: Existing C# telemetry and overlay

Keep `SystemFpsMonitor` as the read-only diagnostics layer.

Responsibilities:

- ETW/RTSS telemetry.
- Foreground process tracking.
- Frame pacing stats.
- Source/status reporting.
- Validation of external/native limiter effects.
- Frame-generation status display when evidence is available.

No frame cap should be implemented here unless it delegates to a real backend.

Frame generation detection belongs in a separate detector layer. The current C# telemetry can suspect generated/interpolated frames from timing patterns, but verified DLSS-G/MFG/FSR FG/AFMF state requires a trusted external provider, app cooperation, or native in-process access.

### Layer 2: Capability and limiter abstraction

Add a C# service in a future milestone, for example:

- `LightCrosshair/FrameLimiting/IFrameLimiterBackend.cs`
- `LightCrosshair/FrameLimiting/FrameLimiterCapability.cs`
- `LightCrosshair/FrameLimiting/FrameLimiterController.cs`
- `LightCrosshair/FrameLimiting/FrameLimitValidationService.cs`

Proposed API surface:

```csharp
public interface IFrameLimiterBackend
{
    string Name { get; }
    Task<FrameLimiterCapability> DetectAsync(CancellationToken cancellationToken);
    Task<FrameLimitApplyResult> ApplyAsync(FrameLimitTarget target, double fps, CancellationToken cancellationToken);
    Task<FrameLimitApplyResult> ClearAsync(FrameLimitTarget target, CancellationToken cancellationToken);
    Task<FrameLimitStatus> GetStatusAsync(FrameLimitTarget target, CancellationToken cancellationToken);
}
```

Backends must report whether they are:

- `DiagnosticsOnly`
- `ExternalLimiter`
- `DriverProfileLimiter`
- `NativeInjectedLimiter`

The UI must not label a backend as active until `ApplyAsync` succeeds and telemetry validation confirms changed frame timing.

### Layer 3: RTSS external backend

This is the most realistic Milestone 4B candidate if RTSS configuration can be performed through a supported and stable interface.

Responsibilities:

- Detect RTSS installation and running process.
- Detect whether per-application profile configuration is supported.
- Apply a target FPS to a specific executable profile.
- Never write directly to undocumented shared memory.
- Validate through `SystemFpsMonitor` that observed FPS converges near the target.

Hard requirement:

- If no supported RTSS profile API or safe configuration path is found, this backend must remain read-only and diagnostics-only.

### Layer 4: Vendor profile/native helper backend

For NVIDIA/AMD/Intel driver-level settings, use a separate native helper or vendor-supported managed binding.

Candidate project:

- `LightCrosshair.NativeInterop` or `LightCrosshair.Native`

Responsibilities:

- Vendor API capability detection.
- Driver profile frame cap or low-latency setting where supported.
- Versioned C ABI for C# interop.
- No target process injection in this layer.

Initial API surface:

- `LcNative_GetAdapterCapabilities`
- `LcNative_GetDriverLimiterStatus`
- `LcNative_SetDriverFrameLimit`
- `LcNative_ClearDriverFrameLimit`
- `LcNative_GetLastError`

This path is real only on supported hardware and drivers. It must degrade cleanly.

### Layer 5: Optional native injected component

This is the only path for a Special K-like in-process limiter owned by LightCrosshair.

Candidate projects:

- `LightCrosshair.HookHost`
- `LightCrosshair.FramePacer.Native`
- `LightCrosshair.SampleDxgiApp` for tests

Responsibilities:

- Inject only into explicit allow-listed processes.
- Hook DXGI/D3D present boundaries in a controlled way.
- Apply a high-resolution wait at a frame boundary.
- Publish stats over named pipe or shared memory.
- Refuse anti-cheat protected or unknown processes by default.

Minimum IPC commands:

- `GetCapabilities`
- `SetFrameLimit`
- `ClearFrameLimit`
- `SetPacingMode`
- `GetPresentStats`
- `Shutdown`

This should not be enabled by default in LightCrosshair 1.4.0 without a signed binary, clear warnings, allow-listing, and integration tests against local sample apps.

## Milestone 4B Recommendation

Implement `FrameLimiterController` and an RTSS capability/validation backend only if a safe RTSS profile interface is confirmed.

Milestone 4B should do:

- Add backend abstractions.
- Add RTSS detection.
- Add RTSS profile apply/clear only through supported file/CLI/profile mechanisms.
- Add telemetry validation after applying a cap.
- Add tests for backend state transitions, config parsing, and validation logic.
- Add an integration checklist for manual verification with a local sample renderer.

Milestone 4B should not do:

- Inject into games.
- Hook Present.
- Add Reflex markers.
- Claim latency reduction.
- Add fake "software cap" UI that only shows a recommended target.

## Real Test Strategy

### Diagnostics

- Unit test frame stats and formatter behavior.
- Launch a stable sample renderer and compare ETW frame intervals with expected refresh/cap behavior.

### RTSS backend

- Precondition: RTSS installed.
- Apply cap to a known sample executable.
- Run sample uncapped and capped.
- Verify average FPS and frametime move toward target.
- Verify LightCrosshair reports backend `ExternalLimiter` and validation state.

### Driver profile backend

- Precondition: supported GPU and driver.
- Apply a profile cap or low-latency setting.
- Restart sample executable if driver requires it.
- Verify profile state through vendor API and observed ETW FPS.

### Native hook backend

- Inject only into `LightCrosshair.SampleDxgiApp`.
- Verify hook attaches to Present.
- Verify cap is applied once per presented frame.
- Verify target FPS, jitter, and cleanup.
- Run without anti-cheat or third-party game targets.

## Technical Risks

- ETW requires permissions and event semantics vary by Windows/GPU/driver.
- RTSS automation may rely on undocumented or changing profile formats unless a supported interface is identified.
- Driver APIs are vendor-specific and may require profile reload, admin rights, or native binaries.
- In-process hooks can crash the target process.
- Anti-cheat systems may flag injected DLLs, overlays, or third-party hooks.
- Precise waiting can waste CPU if implemented poorly.
- Reflex and render queue control require correct frame boundary and marker ordering.

## Feasibility Conclusion

The current safe path is:

1. Keep LightCrosshair telemetry out-of-process.
2. Add real external limiter integration first, preferably RTSS if safe profile control is available.
3. Add driver/vendor integration only behind capability checks.
4. Treat a native hook backend as a separate, explicit, high-risk project, not a minor extension of the overlay.

For frame generation, Milestone 4C should first replace the existing cadence-only generated-frame count with a state/confidence model. It should not claim vendor-specific support until a verified provider/API signal is integrated.
