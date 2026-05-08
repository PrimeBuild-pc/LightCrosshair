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
- Milestone 6: diagnostics and manual validation tooling
- Milestone 7: safe diagnostics polish and release-claim cleanup

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

## Milestone 6 Result

Milestone 6 is diagnostics/manual validation only. It did not implement RTSS
profile writes, driver profile writes, native hooks, injection, in-process
runtime behavior, target-process DLL loading, packaging publication, release
work, or Special K-derived code.

Completed scope:

- Added a pure diagnostic report builder that formats existing telemetry
  snapshots and frame limiter status into stable text/export fields.
- Added CSV export formatting for manual diagnostics without autonomous file
  writes or background sampling.
- Added xUnit coverage for empty snapshots, pacing stats, invariant numeric
  formatting, CSV escaping, conservative frame-generation labels, RTSS
  unsupported semantics, and no-op limiter status.
- Added a manual validation checklist covering normal FPS overlay, advanced FPS
  diagnostics, frame-generation states, no-op limiter status, read-only external
  comparison tools, false positives/negatives, and anti-cheat-safe guidance.

## Next

## Milestone 7 Result

Milestone 7 is safe diagnostics polish and release-claim cleanup only. It did
not implement PresentMon, RTSS profile writes, frame limiting, verified
frame-generation providers, native hooks, injection, in-process DLLs, package
publication, tags, releases, or MSIX.

Completed scope:

- Cleaned public README/install wording so Chocolatey, WinGet, PowerShell
  install script, GitHub Releases, and Inno Setup are described as prepared or
  future channels until final artifacts and checksums exist.
- Replaced runtime ETW-plus-PresentMon claims with ETW-style present telemetry plus
  optional RTSS fallback, and documented RTSS compatibility/anti-cheat caveats.
- Aligned portable and Chocolatey runtime wording around the current
  framework-dependent default and .NET 8 Windows Desktop Runtime requirement.
- Renamed the settings UI from generated-frame wording to a conservative
  frame-generation estimate/suspicion label.
- Reduced synchronous FPS monitor worker wait time during stop/shutdown.
- Added release-claim guard tests covering live install commands, PresentMon
  runtime claims, framework-dependent packaging defaults, and frame-generation
  UI wording.

## Next

### Milestone 8

Packaging validation:

- Inno Setup
- portable ZIP
- Chocolatey metadata
- WinGet manifest prep
- PowerShell installer
- no release publication without approval

Safety gate: Milestone 7 may validate packaging inputs and documentation only.
Do not create an official release, push, tag, publish packages, submit WinGet,
push Chocolatey, create MSIX, or continue into RTSS writes, driver writes,
native hooks, injection, or in-process runtime behavior without explicit
approval.

## Milestone 9 Result

Milestone 9 is a post-1.4.0 backend research/design spike. It does not implement
PresentMon runtime support, RTSS write/control, native hooks, injection, vendor
private APIs, or a real frame limiter.

Completed scope:

- Added `docs/backend-research/` as the research area for future backend
  architecture.
- Documented PresentMon, RTSS, NVIDIA/AMD frame-generation signal, real limiter,
  and Special K reference boundaries.
- Proposed provider capability reporting for unavailable, unsupported,
  heuristic, estimated, verified, external-tool, admin, native, and opt-in
  states.
- Added safety gates that must pass before any backend becomes user-facing.

## Next

Milestone 10 should start with read-only PresentMon capability validation using
captured data and documented provider fields. Runtime provider shipping, RTSS
control, driver writes, native/in-process work, and verified frame-generation
claims remain blocked until explicitly approved.

## Milestone 10 Result

Milestone 10 is a PresentMon offline validation spike. It does not launch
PresentMon, start ETW sessions, add a runtime provider, implement RTSS control,
add native hooks, or change user-facing runtime claims.

Completed scope:

- Added an offline PresentMon CSV parser and summary model under
  `LightCrosshair.Diagnostics.PresentMon`.
- Added conservative frame-generation classification where
  `VerifiedSignalPresent` requires a dedicated recognized column/value such as
  `FrameType=Generated` or `FrameType=Interpolated`.
- Added synthetic xUnit coverage for minimal captures, missing optional columns,
  unknown columns, p95/p99 frame time, present mode distribution, explicit
  generated-frame signals, heuristic-only cadence, malformed rows, and quoted
  CSV fields.
- Documented manual PresentMon capture expectations and why runtime integration
  remains blocked.

## Next

Milestone 11 should validate real local PresentMon CSV captures from an ignored
research fixture folder and compare known native, capped, DLSS-G, FSR FG, AFMF,
and unsupported scenarios. It should remain offline/read-only unless runtime
provider approval is granted separately.

## Milestone 11 Result

Milestone 11 is a real PresentMon capture validation workflow. It does not
launch PresentMon from LightCrosshair, start ETW sessions from the app, add a
runtime provider, implement RTSS control, add native hooks, or change
user-facing runtime claims.

Completed scope:

- Added ignored local capture workspace documentation under
  `research/presentmon-captures/`.
- Added `.gitignore` rules so real local PresentMon CSV/ETL captures are not
  committed.
- Added `scripts/research/analyze-presentmon-capture.ps1`, a research-only CSV
  analyzer that reads existing capture files and prints conservative summary
  fields without launching PresentMon.
- Added real capture scenario documentation and a validation matrix for native,
  capped, high-refresh, DLSS-G, FSR FG, AFMF, unsupported, and desktop/non-game
  cases.

## Milestone 12 Result

Milestone 12 resets the post-1.4.0 roadmap back to product direction. Backend
research from Milestones 9-11 remains useful decision support, but it is not the
product roadmap and should not pull LightCrosshair toward becoming a PresentMon
clone, Special K clone, or enterprise telemetry/profiling tool.

Product direction:

- Crosshair overlay remains the primary feature: lightweight, visible,
  customizable, profile-friendly, and honest about borderless/windowed versus
  exclusive fullscreen behavior.
- Vibrance and color visibility becomes the second major feature direction:
  crosshair visibility improvements first, safe Windows/display color API
  feasibility next, and vendor/backend paths only after explicit review.
- Minimal performance overlay remains the third pillar: compact FPS/app-present
  FPS, frametime, pacing/stutter hints, and conservative frame-generation
  wording without false verified claims.
- Performance and compatibility stay product requirements: ultra-lightweight
  mode, lower diagnostic update rates, no unnecessary invalidation, no hooks or
  injection, and clear anti-cheat/overlay caveats.
- Frame cap work becomes a future Frame Cap Assistant/backend decision, not a
  fake limiter.

See [PRODUCT_DIRECTION_POST_1.4.md](PRODUCT_DIRECTION_POST_1.4.md).

## Next

### Milestone 13

Performance overlay polish and ultra-lightweight mode:

- Add a clear minimal versus detailed performance overlay mode.
- Reduce diagnostics overhead where possible.
- Keep diagnostic overlay off by default or low frequency.
- Measure CPU, memory, handle, GDI, and GPU impact for crosshair-only and
  diagnostics-on scenarios.
- Preserve conservative frame-generation labels: verified only with explicit
  generated/interpolated provider evidence.

### Milestone 14

Crosshair customization plus vibrance/color feasibility pass:

- Improve crosshair shapes, outline/glow, precision controls, presets, hotkeys,
  and profile ergonomics.
- Research safe Windows/display color APIs for vibrance, saturation, contrast,
  brightness, gamma, and color presets.
- Clearly separate feasible now from backend/vendor/API-blocked items.
- Do not claim game post-processing control without a reviewed backend.

### Milestone 15

Frame cap assistant and backend decision:

- Build a product plan for helping users configure in-game caps, driver caps, or
  external limiter tools.
- Decide whether RTSS delegation, driver profile APIs, or native approaches are
  acceptable future backends.
- Keep native/hook-based limiters blocked unless explicitly reviewed.
- Do not claim LightCrosshair enforces a real cap until an approved backend does
  so and telemetry validates the result.
