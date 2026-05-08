# LightCrosshair 1.4.0 - Frame Limiter Backend Architecture

Milestone: 5.

Milestone 5 adds managed backend scaffolding only. The current implementation
does not apply a real frame cap, does not inject into games, does not hook
Present, does not alter scheduler or timer APIs, does not inspect target process
memory, and does not implement a Special K-derived limiter.

## Current State

LightCrosshair remains an out-of-process overlay and telemetry application.
The existing FPS monitor can observe frame timing through ETW and RTSS shared
memory, but it has no target render-loop control point. Without an external,
driver, or native in-process backend, LightCrosshair cannot delay or pace a
game's frame production.

The Milestone 5 runtime backend is therefore intentionally unavailable:

- `IFrameLimiterBackend` defines the backend boundary.
- `FrameLimiterController` validates requests before delegating to a backend.
- `NoOpFrameLimiterBackend` always reports unavailable/inactive status.
- No UI is wired to claim frame limiting works.
- No configuration field changes runtime behavior.

## Managed Model

The backend model lives under `LightCrosshair/FrameLimiting`.

| Type | Purpose |
| --- | --- |
| `IFrameLimiterBackend` | Capability, apply, clear, and status boundary for future backends. |
| `FrameLimiterBackendKind` | Identifies none, RTSS/external, PresentMon validation, vendor profile, or native in-process paths. |
| `FrameLimiterCapability` | Reports whether a backend is available and whether it can apply a limit. |
| `FrameLimiterRequest` | Explicit target plus finite FPS request. |
| `FrameLimiterStatus` | Conservative runtime state. Active requires a real backend and telemetry validation. |
| `FrameLimiterResult` | Apply/clear outcome with rejected, unavailable, unsupported, or success state. |
| `NoOpFrameLimiterBackend` | Safe default backend. It never reports an active or validated cap. |

Request validation is deliberately strict. A request must have an explicit
target process or executable and a finite FPS target between 15 and 1000. Future
controllers can add display-refresh-aware policy, but invalid requests should be
rejected before any backend is called.

## Backend Matrix

| Backend path | Current Milestone 5 state | Can apply a cap now? | Notes |
| --- | --- | --- | --- |
| No-op/unavailable | Implemented | No | Default safe behavior. |
| PresentMon/read-only validation | Model only | No | Useful for observing FPS and validating another backend, not applying caps. |
| RTSS external limiter | Model/docs only | No | Feasible only if a supported safe profile/configuration path is verified. RTSS itself performs any limiting. |
| Vendor/driver profile | Model/docs only | No | Requires verified vendor-supported API and license review. |
| Native/in-process backend | Explicit future checkpoint only | No | Requires separate approval, clean-room design, sample-renderer testing, and anti-cheat policy. |

## Real Backend Requirements

A real target-process cap requires one of these control paths:

- A supported external limiter such as user-installed RTSS where LightCrosshair
  can safely configure a per-app profile and then validate the effect.
- A vendor or driver profile API that explicitly supports frame limit settings.
- A native/in-process component that reaches the target present or render
  boundary. This is high risk and out of scope for Milestone 5.

Overlay telemetry alone is not a frame limiter. A backend must only report active
after an apply operation succeeds and telemetry validation shows observed frame
timing converging toward the target.

## RTSS External Option

RTSS is the most plausible non-LightCrosshair-native path because RTSS can
perform the actual in-process limiting. LightCrosshair may only automate it
after a supported, stable control path is confirmed. If no supported RTSS
profile API, CLI, or documented configuration mechanism is verified, an RTSS
backend must stay diagnostics-only.

LightCrosshair must not write undocumented shared memory or imply that reading
RTSS telemetry means it controls RTSS.

## Native/Hook Option And Risks

A Special K-like limiter needs native code inside or immediately adjacent to the
target rendering path. That implies swapchain/present access, high-resolution
wait placement, IPC, cleanup, crash isolation, and default refusal for
anti-cheat-protected, multiplayer, unknown, or arbitrary third-party game
processes.

This path requires a separate milestone and explicit approval before any code is
written. Initial testing must be limited to an owned local sample renderer.

## Licensing Boundary

Special K is GPLv3 reference material. Milestone 5 does not copy, translate, or
mechanically port Special K code, structs, algorithms, comments, or control
flow. Any real backend must be implemented from public Windows/.NET/ETW,
RTSS/PresentMon, or vendor documentation with license review before
redistribution.

## Anti-Cheat Boundary

Milestone 5 does not introduce injection, hooks, detours, native DLL loading,
process memory inspection, driver modification, or Reflex/Streamline/NGX
interception. Future native work must include an explicit anti-cheat policy and
must not attach to protected or unknown processes by default.
