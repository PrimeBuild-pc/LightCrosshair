# LightCrosshair 1.4.0 QA Checklist

Use this checklist for release-candidate validation only. Do not publish, tag,
submit manifests, or advertise install commands from this checklist.

## Test Environment

- Windows version:
- CPU/GPU:
- Monitor refresh rates:
- .NET 8 Windows Desktop Runtime installed: Yes / No
- RTSS installed: Yes / No
- Tested build/artifact path:
- Tester:
- Date:

## Portable ZIP

- [ ] Extract `LightCrosshair-v1.4.0-x64.zip` to a clean folder.
- [ ] Confirm `LightCrosshair.exe` launches without installer registration.
- [ ] Repeat with ARM64 package on ARM64 hardware or document as not tested.
- [ ] Confirm launch fails gracefully or runtime requirement is clear when the
      .NET 8 Windows Desktop Runtime is absent.
- [ ] Confirm no admin prompt is required for normal launch.

## Inno Installer

- [ ] Install from the locally compiled `LightCrosshair-Setup-1.4.0.exe`.
- [ ] Confirm Start Menu shortcut launches the app.
- [ ] Confirm optional desktop shortcut launches the app when selected.
- [ ] Confirm the installer text or task points users to the .NET 8 Desktop
      Runtime when needed.
- [ ] Uninstall from Windows Apps or Programs and Features.
- [ ] Confirm app files are removed from the install directory.

## First Launch And Settings

- [ ] Launch with no existing user settings.
- [ ] Confirm the default overlay is visible only when enabled.
- [ ] Change crosshair shape, color, opacity, and size.
- [ ] Close and relaunch.
- [ ] Confirm settings persist.
- [ ] Confirm clean uninstall/reinstall does not leave unexpected install files.

## Overlay And FPS Diagnostics

- [ ] Toggle overlay on and off from the app UI or tray workflow.
- [ ] Enable FPS overlay.
- [ ] Disable FPS overlay.
- [ ] Enable advanced FPS diagnostics.
- [ ] Confirm frame pacing values update when telemetry is available.
- [ ] Confirm unavailable telemetry is shown as unavailable or waiting, not as a
      successful measurement.
- [ ] Confirm high-refresh monitor behavior at 144 Hz or higher.

## Frame-Generation Estimate Wording

- [ ] Enable `Show frame-generation estimate`.
- [ ] Confirm UI wording says estimate, suspicion, or heuristic.
- [ ] Confirm the overlay does not show verified generated-frame counting unless
      a verified provider signal exists.
- [ ] Confirm any suspected frame-generation output is labeled `SUSPECT`,
      `EST`, heuristic, or equivalent conservative wording.

## RTSS And ETW-Style Telemetry

- [ ] Test with RTSS not installed or not running.
- [ ] Confirm LightCrosshair does not require RTSS for normal overlay use.
- [ ] Test with RTSS installed and running.
- [ ] Confirm RTSS is read as an optional fallback source only.
- [ ] Confirm LightCrosshair does not write RTSS profiles or claim RTSS control.
- [ ] Block or deny ETW-style telemetry if practical.
- [ ] Confirm fallback or unavailable status is clear.
- [ ] Run once as normal user and once as administrator.
- [ ] Confirm admin status is not described as anti-cheat bypass.

## Game Smoke Tests

- [ ] Borderless windowed game or sample renderer.
- [ ] Windowed game or sample renderer.
- [ ] Exclusive fullscreen game if available.
- [ ] Confirm overlay remains stable while alt-tabbing.
- [ ] Confirm no crash on game exit.
- [ ] Confirm no injection, hook, or native backend is introduced by the app.

## Frame Limiter Scaffold

- [ ] Confirm any limiter status is unavailable, inactive, or no-op.
- [ ] Confirm the app does not claim to apply a real FPS cap.
- [ ] Confirm docs or diagnostics do not imply active limiting without a real
      backend and telemetry validation.

## Anti-Cheat And Compatibility Wording

- [ ] Confirm public docs say LightCrosshair itself does not inject into games.
- [ ] Confirm RTSS fallback caveats mention RTSS compatibility and anti-cheat
      risk depending on RTSS configuration and target game.
- [ ] Confirm no screen claims describe the app as guaranteed anti-cheat safe.

## Release Decision

- [ ] Build passed.
- [ ] Tests passed.
- [ ] Release preflight passed.
- [ ] Portable ZIP dry-run artifacts were inspected.
- [ ] Installer compile was validated or documented as blocked by missing Inno
      Setup.
- [ ] Chocolatey pack was validated locally or documented as blocked by missing
      Chocolatey CLI.
- [ ] WinGet validation was run locally or documented as blocked by missing
      tooling/final manifests.
- [ ] No generated artifacts are staged for commit.
