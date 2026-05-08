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
