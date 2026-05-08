# LightCrosshair 1.4.0 - Special K Licensing Notes

Milestone: 4A feasibility only.

Special K is GPLv3. The files in `SpecialK-components` are technical references only. This repository must not copy, translate, or mechanically port Special K implementation code unless the resulting distribution is license-compatible and attribution is handled correctly.

## Allowed In This Milestone

- Read Special K components.
- Describe high-level behavior.
- Document technical prerequisites and risks.
- Plan an original LightCrosshair architecture.
- Implement no Special K-derived code.

## Clean-Room Boundary

For future implementation, keep these boundaries:

- Use Windows, .NET, ETW, RTSS, and vendor documentation as primary implementation sources.
- Use Special K only to understand feature categories and real-world constraints.
- Avoid porting algorithms, control flow, names, comments, structs, or code organization from Special K.
- If a feature needs similar behavior, write an original spec first and implement from that spec.
- Keep attribution notes for files reviewed, even when no code is copied.

## Feature License Risk

| Feature area | Risk | Notes |
| --- | --- | --- |
| ETW/RTSS diagnostics reimplemented in C# | Low | Use public Windows/TraceEvent APIs and original LightCrosshair code. |
| Frame statistics and overlay formatting | Low | Existing LightCrosshair implementation is original. Keep calculations and names independent. |
| RTSS external profile integration | Low to Medium | Depends on RTSS license/API/profile format. Do not copy Special K code. |
| Vendor API integration | Low to Medium | Follow vendor SDK licenses and redistribution terms. |
| In-process frame limiter | High | Special K implementation is GPL and complex. A clean-room design or GPL-compatible licensing decision is required. |
| Latent Sync / scanline pacing | High | Requires native graphics knowledge and risks substantial similarity if based too closely on Special K. |
| Reflex marker/sleep override | High | Requires NVAPI, in-process integration, and careful clean-room design. |
| DLSS-G/MFG status detection | Medium to High | Low if implemented from NVIDIA Streamline documentation or external provider data; high if adapting Special K NGX/Streamline probing. |
| AMD AFMF/FSR FG detection | Medium | Low if based on public PresentMon/vendor provider data; high if using injection or copied detection logic. |
| Scheduler/timer detours | High | Do not port Special K detour implementation. |
| DLSS/Streamline/DLL redirection | Very High | Out of scope and high contamination/compatibility risk. |

## Attribution Trigger

Attribution and license review are required if any future change:

- Copies Special K code.
- Translates Special K code into C# or C++.
- Reuses Special K data structures or function-level logic.
- Adapts non-trivial algorithms from Special K files.
- Ships a component derived from Special K behavior beyond general ideas.

## Recommended Implementation Policy

- Milestone 4B should prefer RTSS external integration or capability detection.
- Native/injected work should be a separate design checkpoint.
- Any native limiter should be developed against a local sample renderer first.
- Frame-generation detection should expose confidence and source quality; cadence-only logic must be labeled heuristic.
- If implementation substantially follows Special K internals, decide explicitly whether LightCrosshair or the relevant component will be GPL-compatible before coding.
- `SpecialK-components` contains GPLv3 source files for reference. Do not include that directory in installers, portable packages, or release assets unless GPLv3 license text and third-party notices are deliberately shipped with it.
- If the repository itself is redistributed with `SpecialK-components`, add or keep clear third-party attribution identifying Special K as GPLv3 reference material, not MIT-licensed LightCrosshair code.

## Do Not Promise

- "Special K-compatible limiter" unless there is a deliberate compatibility implementation and licensing review.
- "Reflex support" unless NVAPI marker/sleep/report integration exists.
- "Frame cap" for overlay-only telemetry.
- "Latency reduction" for diagnostics-only measurements.
- "DLSS-G", "MFG", "FSR FG", or "AFMF detected" from timing heuristics alone.
