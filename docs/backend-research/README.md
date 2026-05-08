# LightCrosshair Milestone 9 - Backend Research Spike

Milestone 9 is a post-1.4.0 research and design spike. It does not implement a
native backend, target-process hook, PresentMon runtime provider, RTSS control
path, vendor private API integration, or real frame limiter.

## Scope

- Document backend options for future frame telemetry, frame-generation
  evidence, latency diagnostics, and frame limiting.
- Keep Special K as a behavior and architecture reference only.
- Preserve the current runtime truth: LightCrosshair remains an out-of-process
  overlay with ETW-style present telemetry, optional RTSS read fallback, a
  heuristic frame-generation suspicion path, and a no-op frame limiter scaffold.
- Define safety gates that must pass before any backend becomes user-facing.

## Documents

| Document | Purpose |
| --- | --- |
| [backend-options-matrix.md](backend-options-matrix.md) | Research matrix for PresentMon, RTSS, NVIDIA/AMD frame-generation signals, real frame limiter paths, and the Special K boundary. |
| [provider-architecture.md](provider-architecture.md) | Proposed provider model for future unavailable, heuristic, verified, unsupported, external-tool, and opt-in states. |
| [backend-safety-gates.md](backend-safety-gates.md) | Checklist that blocks future user-facing backend claims until licensing, anti-cheat, admin, packaging, tests, wording, and QA gates are complete. |
| [presentmon-offline-validation.md](presentmon-offline-validation.md) | Offline PresentMon CSV validation spike notes, supported columns, conservative inference rules, and runtime integration blockers. |
| [presentmon-capture-scenarios.md](presentmon-capture-scenarios.md) | Real local capture scenarios, manual notes template, and validation matrix for PresentMon parser research. |

## Current Recommendation

The safest Milestone 10 path is a read-only PresentMon capability and validation
spike outside runtime default behavior. Treat PresentMon CLI capture and
FrameType/FPS-App/FPS-Presents/FPS-Display fields as research inputs first, then
decide whether a packaged runtime provider is acceptable after license,
privilege, overhead, and UX review.

RTSS should remain read-only diagnostics unless a documented, supported control
path is verified. Native/in-process frame limiting, hooks, driver profile
writes, vendor private APIs, and RTSS write/control paths remain blocked behind
explicit approval.

## References

- PresentMon console documentation: https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md
- PresentMon capture application documentation: https://github.com/GameTechDev/PresentMon/blob/main/README-CaptureApplication.md
- Intel PresentMon overview: https://game.intel.com/ch/intel-presentmon/
- NVIDIA Streamline DLSS-G programming guide: https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS_G.md
- NVIDIA Streamline getting started: https://developer.nvidia.com/rtx/streamline/get-started
- NVIDIA FrameView overview: https://www.nvidia.com/en-in/geforce/technologies/frameview/
- AMD FSR 3 overview: https://gpuopen.com/fidelityfx-super-resolution-3/
- AMD FSR 3 documentation: https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-interpolation/
- Special K repository: https://github.com/SpecialKO/SpecialK
