# LightCrosshair 1.4.0 Agentic Development Plan

## Completed

- Milestone 1: packaging baseline 1.4.0
- Milestone 2: frame pacing statistics
- Milestone 3A: diagnostic FPS overlay
- Milestone 3B: settings toggle for diagnostics
- Milestone 4A: real frame pacing feasibility
- Milestone 4B: frame generation detection feasibility
- Milestone 4C: conservative frame generation detection fallback
- Milestone 4D: real backend path for Special K-like frame generation detection
- Milestone 5: frame limiter backend architecture

## Milestone 4D Result

Milestone 4D is design/scaffolding only. It did not implement native hooks,
injection, in-process DLLs, process memory inspection, Streamline/NGX
interception, Reflex overrides, DLL redirection, or timing detours.

Completed scope:

- Re-checked the Special K DLSS-G/MFG method and confirmed it depends on
  in-process NGX/Streamline/plugin state.
- Added safe C# abstractions for future verified frame-generation signal
  providers.
- Added a no-op provider that always reports unavailable/unverified.
- Kept the existing conservative `FrameGenerationDetector` as the runtime
  fallback.
- Documented the verified backend options, anti-cheat gates, and GPL/vendor
  licensing limits.

## Milestone 5 Result

Milestone 5 is managed architecture/scaffolding only. It did not implement
native hooks, injection, in-process DLLs, Present/swapchain interception, driver
profile writes, RTSS profile writes, anti-cheat risky behavior, packaging, or
release work.

Completed scope:

- Added `LightCrosshair/FrameLimiting` managed interfaces and models for future
  frame limiter backends.
- Added `NoOpFrameLimiterBackend`, which always reports unavailable/inactive and
  never claims an active cap.
- Added `FrameLimiterController` request validation so invalid target/FPS
  requests are rejected before a backend is called.
- Added xUnit coverage for unavailable/no-op behavior, conservative status,
  cancellation, invalid requests, and diagnostics-only capability semantics.
- Documented the backend matrix, RTSS/external path, native/hook risk boundary,
  licensing boundary, and anti-cheat considerations.

## Next

### Milestone 6

Manual validation tools and diagnostics:

- test profiles
- telemetry logging
- debug overlay
- reproducible validation checklist

Safety gate: Milestone 6 may add diagnostics, telemetry export, validation
checklists, and sample-runner documentation only. Stop before implementing
native present hooks, injection, detours, target-process DLL loading, RTSS
profile writes, driver writes, packaging publication, or release work.

### Milestone 7

Packaging validation:

- Inno Setup
- portable ZIP
- Chocolatey metadata
- WinGet manifest prep
- PowerShell installer
- no release publication without approval
