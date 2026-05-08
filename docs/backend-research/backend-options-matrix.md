# Backend Options Matrix

This matrix covers future backend options for LightCrosshair after the 1.4.0
release. It is research documentation only. It must not be read as a promise
that any backend is implemented or approved.

## PresentMon

| Question | Assessment |
| --- | --- |
| CLI integration possibility | Feasible for a research spike. The console app can capture per-process CSV data and supports command-line options. A CLI bridge would need process selection, lifecycle management, output parsing, timeout handling, version detection, and clear user consent. |
| Library/provider integration possibility | Plausible but not approved. PresentMon has a service/capture application architecture and public repository, but LightCrosshair needs a separate packaging and license review before redistributing binaries or linking against provider APIs. |
| ETW session requirements | PresentMon is ETW-based and can capture active sessions or analyze ETL files. Active capture may require ETW session permissions and process elevation depending on provider access and system policy. |
| Admin requirements | The console documentation includes a restart-as-admin option. LightCrosshair must treat admin as environment-dependent, document it, and avoid silently elevating. |
| Packaging/licensing concerns | Confirm current PresentMon license and third-party notices from the exact version used. Do not bundle PresentMon until redistribution terms, binary provenance, checksums, and notices are reviewed. |
| Runtime overhead | Expected overhead depends on enabled providers and metrics. Display, GPU, input, and frame-type tracking add more collection work than basic present timing. Any runtime provider needs a low-overhead profile and manual QA on high-refresh systems. |
| Frame time / present mode | Supported by PresentMon outputs such as frame timing, present runtime, present mode, displayed time, and related metrics. Useful for validation and richer diagnostics. |
| Latency-like data | PresentMon exposes GPU/display/click-to-photon style metrics where supported, but LightCrosshair must label them as measured diagnostics, not latency reduction. |
| Generated-frame hints | `FrameType` can distinguish rendered vs driver/SDK-generated frames only when frame-type tracking and instrumentation/provider support are available. FPS-App/FPS-Presents/FPS-Display can support derived evidence but should not be called verified provider state by itself. |
| Feasible without injection | Yes for read-only CLI/capture/provider research. No target-process hook is required for capture, but provider availability, permissions, and packaging remain open. |

### PresentMon Decision

Recommended first backend research path:

1. Build a non-runtime sample parser around captured CSV files.
2. Validate fields on known native, capped, DLSS-G, FSR FG, and AFMF workloads.
3. Record what fields are present by version and GPU/driver stack.
4. Only after that, design an opt-in runtime provider with explicit external
   tool/admin/overhead labels.

Do not claim PresentMon runtime support until LightCrosshair has an implemented,
tested provider or CLI bridge.

## RTSS

| Question | Assessment |
| --- | --- |
| Read-only overlay/statistics possibility | Already used in LightCrosshair as a legacy/fallback frame-time source through `RTSSSharedMemoryV2`. This is read-only diagnostics, not native telemetry ownership. |
| API/shared memory availability | RTSS exposes shared memory structures and SDK samples, but field coverage and version compatibility need exact SDK review. Current LightCrosshair only relies on basic frame-time style data. |
| Anti-cheat and compatibility caveats | LightCrosshair itself does not inject into games. RTSS may use hooks/OSD mechanisms and can inherit compatibility or anti-cheat risk depending on RTSS configuration and target game. |
| RTSS fallback vs LightCrosshair-native telemetry | RTSS fallback is third-party observed data. It is not ETW-style telemetry, not a verified frame-generation provider, and not proof that LightCrosshair controls the game. |
| Write/control paths | Forbidden for now. Do not write shared memory, modify RTSS profiles, automate caps, or expose RTSS control UI until a supported control path, license review, anti-cheat review, and rollback plan exist. |

### RTSS Decision

Keep RTSS read-only. Treat any future write/control integration as a separate
explicitly approved milestone. If RTSS becomes a limiter path later, RTSS must
perform the actual limiting and LightCrosshair must report it as external,
user-installed, opt-in, and telemetry-validated.

## NVIDIA And AMD Frame-Generation Detection

| Signal class | NVIDIA DLSS-G / MFG | AMD FSR FG / AFMF | User-facing status |
| --- | --- | --- | --- |
| PresentMon `FrameType` with provider support | Potential verified external frame-type evidence when the field is present and documented for the active stack. | Potential verified external frame-type evidence when the field is present and documented for the active stack. | May be `Detected` only if the provider evidence is explicit and version/capability checked. |
| PresentMon FPS-App/FPS-Presents/FPS-Display deltas | Strong derived evidence for generated/interpolated display output, but not necessarily vendor truth. | Strong derived evidence for generated/interpolated display output, but not necessarily vendor truth. | `Estimated` or `Suspected`, unless paired with explicit frame type. |
| Streamline/NGX state | Verified if read from app cooperation or an approved in-process provider using public NVIDIA SDK contracts. | Not applicable. | `Detected` only with verified provider evidence. |
| AMD FSR game/plugin state | Not applicable. | Verified if exposed by game/plugin cooperation or supported AMD SDK/provider path. | `Detected` only with trusted app/provider evidence. |
| Driver profile setting or vendor overlay state | Capability or configuration evidence, not proof that generated frames are currently presented. | Capability or configuration evidence, not proof that generated frames are currently presented. | `Available`, `Configured`, or `Suspected`; not `Detected` alone. |
| ETW cadence / FPS ratio | Heuristic only. | Heuristic only. | `Suspicion`, `Estimate`, or `Heuristic`; never `Detected`. |
| Process module scan, DLL names, overlay text scraping | Unreliable and privacy/anti-cheat sensitive. | Unreliable and privacy/anti-cheat sensitive. | Do not use for user-facing detection. |

### Verified Provider Signal Definition

A verified frame-generation signal must include all of the following:

- A documented provider or cooperative app source.
- A positive capability check for the exact provider and version.
- Evidence that generated/interpolated frames are active or inactive for the
  current target, not only globally supported by hardware or driver settings.
- A timestamp or sample window tied to the current telemetry target.
- Tests proving unavailable and heuristic paths cannot become verified.

Without that, LightCrosshair wording must use `estimate`, `suspicion`,
`heuristic`, `unavailable`, or `unsupported`.

## Real Frame Limiter Architecture

| Path | Can LightCrosshair do it out-of-process today? | Notes |
| --- | --- | --- |
| Current no-op scaffold | Yes | It deliberately reports unavailable/inactive and does not apply a cap. |
| External limiter delegation | Possible later | RTSS, driver control panel, or another supported tool may perform the cap. LightCrosshair would only request/configure through documented APIs and validate observed telemetry. |
| PresentMon validation | Read-only only | PresentMon can help verify resulting FPS/pacing but cannot apply a cap. |
| Vendor driver profile | Possible only with public supported API | Requires license review, vendor support, rollback, and proof that a setting applies to the intended target. |
| Native/in-process limiter | Not approved | True per-game frame limiting usually needs a render/present boundary, high-resolution wait placement, driver/vendor support, or a tool already inside the target path. |

Sleeping or pacing from a non-injected overlay process cannot reliably limit a
different game's render loop. It can only observe. Any future active limiter
must pass a safety gate proving it controls a real backend and telemetry shows
the cap converging to the target.

## Special K Reference Boundary

Useful concepts from Special K:

- Separate capability detection from active state.
- Distinguish diagnostic display from runtime control.
- Treat frame pacing, frame generation, latency, and limiter behavior as
  separate capabilities.
- Require explicit backend evidence before user-facing claims.

What must not be copied:

- GPLv3 source code, structs, comments, control flow, function-level logic,
  detours, scheduler logic, NGX/Streamline probing, Reflex integration, or OSD
  implementation.
- Any Special K-derived native backend without a GPL compatibility decision or
  clean-room design.

Attribution and documentation requirements:

- Cite Special K files reviewed when they shape architecture.
- Cite public Windows, PresentMon, RTSS, NVIDIA, AMD, or vendor docs as the
  implementation source for any future code.
- Keep release notes conservative: "inspired by behavior" and "technical
  reference" are acceptable; "Special K-compatible" is not.
