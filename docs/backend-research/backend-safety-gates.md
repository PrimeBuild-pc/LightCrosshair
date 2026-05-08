# Backend Safety Gates

No future backend may become user-facing until every required gate below is
complete. This checklist applies to PresentMon runtime support, RTSS control,
vendor APIs, frame-generation detection, latency diagnostics, and real frame
limiting.

## Universal Gates

- [ ] Written design describes the provider, data source, privilege model,
      failure modes, and rollback behavior.
- [ ] License review completed for every SDK, binary, sample, schema, and
      redistributed file.
- [ ] Special K GPLv3 boundary reviewed; no Special K code, structs, comments,
      control flow, or function-level logic copied.
- [ ] Public implementation sources are cited in docs.
- [ ] Anti-cheat and compatibility risks are documented.
- [ ] Admin/elevation requirements are documented and not hidden.
- [ ] The backend is disabled by default unless there is a separate written
      approval to change defaults.
- [ ] User-facing claims reviewed for unavailable/heuristic/verified wording.
- [ ] Automated tests cover unavailable, unsupported, error, cancellation, and
      conservative wording paths.
- [ ] Manual QA plan includes at least one supported sample app and at least one
      unsupported/no-provider scenario.
- [ ] Release preflight or claim-guard tests are updated for the new claims.

## PresentMon Gates

- [ ] Exact PresentMon version selected and checksummed.
- [ ] License and third-party notices reviewed.
- [ ] Decision made: user-installed dependency, bundled binary, CLI bridge, or
      library/service integration.
- [ ] ETW/admin behavior tested as standard user and admin.
- [ ] High-refresh overhead tested with diagnostics disabled and enabled.
- [ ] `FrameType` support tested on workloads where the field is present and
      absent.
- [ ] FPS-App/FPS-Presents/FPS-Display semantics documented as derived evidence
      unless paired with verified frame type.
- [ ] Provider failure cannot block or crash crosshair rendering.

## RTSS Gates

- [ ] Scope remains read-only unless a separate approval allows control paths.
- [ ] Exact RTSS SDK/source documentation reviewed for any shared-memory fields.
- [ ] RTSS installed/not-installed/running/not-running states tested.
- [ ] Anti-cheat caveat explicitly states RTSS may inherit compatibility risk
      depending on configuration and target game.
- [ ] No writes to RTSS shared memory or profiles unless a supported API/control
      path is approved.
- [ ] Any future cap is labeled external and user-installed, not
      LightCrosshair-native.

## NVIDIA / AMD Frame-Generation Gates

- [ ] Provider proves active generated/interpolated frame evidence for the
      current target, not only hardware capability or global profile state.
- [ ] `Detected` is impossible unless evidence quality is verified.
- [ ] Heuristic cadence and FPS-ratio paths remain labeled estimate/suspicion.
- [ ] NVIDIA Streamline/NGX, AMD FSR/AFMF, and vendor-driver terms reviewed
      before any SDK or API use.
- [ ] No private API, DLL replacement, module scan, overlay scraping, or target
      memory inspection is used without explicit approval.
- [ ] Manual validation includes known off/on generated-frame workloads and a
      native high-refresh false-positive scenario.

## Real Frame Limiter Gates

- [ ] Backend can actually apply a cap through an approved external, driver, or
      native path.
- [ ] LightCrosshair reports no-op/unavailable until apply succeeds.
- [ ] Active status requires telemetry validation against the requested target.
- [ ] Non-injected overlay sleeping is not represented as real frame limiting.
- [ ] Native/in-process limiter work has explicit approval, sample-app-only
      scope, signing/debugging plan, crash isolation, and anti-cheat refusal
      rules.
- [ ] Rollback and clear behavior are tested.

## Blocked Until Explicit Approval

- Target-process injection, hooks, detours, swapchain interception, native
  backend DLLs, driver profile writes, vendor private APIs, RTSS profile writes,
  RTSS shared-memory writes, PresentMon runtime provider shipping, or any
  feature claiming verified frame-generation detection without a verified
  provider signal.
