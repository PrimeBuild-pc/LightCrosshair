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

## Next

### Milestone 5

Frame limiter backend architecture:

- `IFrameLimiterBackend`
- NoOp/Unavailable backend
- RTSS or external backend if feasible
- native backend plan if required

Safety gate: Milestone 5 may design non-invasive interfaces and no-op/external
adapters only. Stop before implementing native present hooks, injection,
detours, target-process DLL loading, or packaging/release work.

### Milestone 6

Manual validation tools and diagnostics:

- test profiles
- telemetry logging
- debug overlay
- reproducible validation checklist

### Milestone 7

Packaging validation:

- Inno Setup
- portable ZIP
- Chocolatey metadata
- WinGet manifest prep
- PowerShell installer
- no release publication without approval
