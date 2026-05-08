# Future Backend Provider Architecture

This document proposes backend boundaries for Milestone 10+ without changing
runtime behavior. Existing 1.4.0 code already includes
`IFrameGenerationSignalProvider` and `IFrameLimiterBackend` no-op scaffolds.
Milestone 9 does not add or wire a real backend.

## Design Goals

- Keep the default runtime safe, unavailable, and no-op unless a provider is
  explicitly enabled.
- Make source quality visible: unavailable, unsupported, heuristic, estimated,
  verified, external-tool-required, admin-required, and user-opt-in-required.
- Prevent heuristic frame-generation evidence from becoming `Detected`.
- Prevent diagnostics-only telemetry from becoming a frame limiter.
- Keep native/in-process work behind explicit approval.

## Provider Roles

| Provider | Purpose | Current state |
| --- | --- | --- |
| `IFrameTelemetryProvider` | Reads present/frame timing, source status, present mode, and display timing where available. | Design only. Current `SystemFpsMonitor` directly owns ETW-style and RTSS fallback collection. |
| `IFrameGenerationSignalProvider` | Reports verified or unverified frame-generation evidence. | Exists with `NoOpFrameGenerationSignalProvider`; no real provider is wired. |
| `IFrameLimiterBackend` | Applies or clears a real cap through an external, driver, or native backend. | Exists with `NoOpFrameLimiterBackend`; no real backend is wired. |
| `ILatencyDiagnosticsProvider` | Reports latency-like diagnostic measurements and source quality. | Design only. Must not claim latency reduction. |
| `BackendCapabilityReport` | Aggregates provider capabilities for UI, diagnostics export, and safety gates. | Design only. |

## Capability Model

Every provider should report a capability object with these fields or
equivalent:

| Field | Meaning |
| --- | --- |
| `ProviderId` | Stable provider identifier, such as `none`, `etw-present`, `presentmon-cli`, `rtss-shared-memory`, `native-ngx`, or `vendor-driver-profile`. |
| `DisplayName` | User-facing provider name. |
| `State` | `Unavailable`, `Unsupported`, `Available`, `Active`, `Error`, or `BlockedByPolicy`. |
| `EvidenceQuality` | `None`, `Heuristic`, `EstimatedExternal`, `VerifiedExternal`, `VerifiedInProcess`, or `VerifiedCooperative`. |
| `RequiresExternalTool` | True for PresentMon CLI/service, RTSS, FrameView, or another separately installed tool. |
| `RequiresAdmin` | True when ETW/provider/admin conditions require elevation. |
| `RequiresUserOptIn` | True for any non-default provider, external tool, write/control path, or higher-overhead collection. |
| `RequiresNativeComponent` | True for native DLLs, driver APIs, or in-process code. |
| `RequiresInjectionOrHook` | True for any target-process injection, swapchain hook, detour, or in-process attach. |
| `CanVerifyFrameGeneration` | True only when the provider exposes documented active generated-frame evidence. |
| `CanApplyFrameLimit` | True only when the backend can actually control a target limiter. |
| `CanValidateFrameLimit` | True when telemetry can independently observe convergence toward a requested cap. |
| `LicenseStatus` | `Reviewed`, `NeedsReview`, or `Blocked`. |
| `AntiCheatRisk` | `None`, `Low`, `Medium`, `High`, or `Blocked`. |
| `EvidenceText` | Short diagnostic explanation suitable for export. |

## Suggested Interfaces

These are design sketches only. They should be implemented only when a future
milestone needs code and tests.

```csharp
internal interface IFrameTelemetryProvider
{
    string ProviderId { get; }
    ValueTask<BackendCapabilityReport> DetectAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<FrameTelemetrySample> ReadSamplesAsync(
        FrameTelemetryTarget target,
        CancellationToken cancellationToken);
}
```

```csharp
internal interface ILatencyDiagnosticsProvider
{
    string ProviderId { get; }
    ValueTask<BackendCapabilityReport> DetectAsync(CancellationToken cancellationToken);
    ValueTask<LatencyDiagnosticsSnapshot> TryReadAsync(
        FrameTelemetryTarget target,
        CancellationToken cancellationToken);
}
```

`IFrameGenerationSignalProvider` and `IFrameLimiterBackend` already exist and
should stay conservative:

- `NoOpFrameGenerationSignalProvider` remains disabled and unavailable.
- `NoOpFrameLimiterBackend` remains unavailable/inactive and cannot apply a cap.
- `Detected` requires `IsVerified == true`.
- `ActiveValidated` requires a real backend plus telemetry validation.

## Runtime Policy

Default:

- ETW-style present telemetry can run as the existing diagnostics source.
- RTSS remains an optional read fallback only.
- Frame-generation detection remains heuristic/suspected only.
- Frame limiter remains no-op/unavailable.

Opt-in provider:

- Must show provider name, external tool/admin/native requirements, and source
  quality before enabling.
- Must be disabled by default after upgrade.
- Must fail closed to unavailable or unsupported.
- Must not silently elevate, install tools, write profiles, inject, hook, or
  inspect target memory.

User-facing wording:

| Evidence | Allowed wording |
| --- | --- |
| No provider | `Unavailable`, `No provider`, `Diagnostics unavailable` |
| ETW/RTSS cadence only | `Heuristic`, `Estimate`, `Suspicion`, `Suspected` |
| PresentMon FPS deltas only | `External estimate`, `Derived estimate`, `Needs validation` |
| PresentMon frame type | `Verified external signal` if capability checked |
| Game/app cooperation | `Verified app signal` if trusted and current |
| Native NGX/Streamline state | `Verified native signal` only after explicit native approval |
| No-op limiter | `Unavailable`, `No-op`, `Not applying a cap` |
| External limiter configured but not validated | `Configured`, `Validation pending` |
| External/native limiter validated | `Active, telemetry validated` |

## Test Requirements For Any Code

- No-op providers report unavailable and disabled by default.
- Cancellation is honored before any external process/session starts.
- Provider errors degrade to unavailable/error states without crashing overlay
  rendering.
- Frame-generation `Detected` cannot be produced without verified provider
  evidence.
- Frame limiter active status cannot be produced without a real backend result
  and telemetry validation.
- Diagnostics export includes provider name and evidence quality.
- Preflight or release-claim guard tests reject overclaims in README/docs/setup
  when runtime support is not implemented.
