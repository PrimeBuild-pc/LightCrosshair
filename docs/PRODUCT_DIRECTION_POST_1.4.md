# LightCrosshair Product Direction After 1.4.0

Milestone 12 resets the roadmap back to product scope. LightCrosshair should
remain a lightweight gamer-focused crosshair overlay, not a PresentMon clone, a
Special K clone, or an enterprise telemetry/profiling tool.

## Product Boundaries

LightCrosshair is:

- A lightweight crosshair overlay for competitive gaming.
- Optimized for low overhead and simple day-to-day use.
- Best suited for borderless/windowed games where normal desktop overlays can
  appear reliably.
- Anti-cheat-conscious by avoiding hooks, injection, native game backends, and
  private vendor APIs unless a future milestone explicitly reviews and approves
  them.
- Honest about limitations, especially exclusive fullscreen visibility,
  anti-cheat compatibility, and telemetry uncertainty.

LightCrosshair is not:

- A full PresentMon replacement.
- A Special K replacement.
- A capture/profiling suite.
- A driver control panel.
- A guaranteed frame-generation detector.
- A real frame limiter until an approved backend can enforce a cap.

## Pillar 1: Crosshair Overlay

The crosshair remains the main feature. Future work should improve what players
touch most often:

- More shapes, outlines, glow, center dot, and layered reticle options.
- More precise size, gap, thickness, opacity, and per-layer color controls.
- Presets and profile handling that make common competitive setups fast to
  switch.
- Hotkeys for visibility, profile cycling, position nudging, and quick editing.
- Clear visibility guidance: borderless/windowed mode is the expected path;
  exclusive fullscreen may hide normal overlays.
- Clear compatibility guidance: LightCrosshair avoids injection/hooks, but some
  games and anti-cheat systems may still block or interfere with overlays.

Implementation direction:

- Keep rendering cheap and predictable.
- Prefer config-driven redraws over constant invalidation.
- Keep settings understandable for players, not diagnostics specialists.

## Pillar 2: Vibrance And Color Visibility GUI

Color visibility is the second major product direction. The goal is competitive
visibility control without pretending LightCrosshair can safely post-process a
game frame unless a real backend exists.

Feasible now:

- Stronger crosshair color, contrast, outline, glow, and background-aware preset
  guidance.
- User presets and checklists for common maps, games, display modes, and monitor
  settings.
- Research and documentation for safe Windows/display color APIs.
- UI planning for vibrance, saturation, contrast, brightness, gamma, and color
  presets, with honest capability labels.

Requires research or explicit backend approval:

- Per-game display color changes beyond existing safe OS/display paths.
- Vendor control APIs for NVIDIA or AMD settings.
- Any claim that LightCrosshair changes in-game post-processing.
- Any private API, hook, injection, or native backend.

Implementation direction:

- Separate "can configure crosshair visibility" from "can alter game/display
  output."
- Label backend capability as available, unavailable, research-only, or external
  rather than implying hidden control.

## Pillar 3: Minimal Performance Overlay

Performance information should stay useful and small. The overlay should help a
player decide whether the game is smooth without becoming a telemetry dashboard.

Recommended metric tiers:

- Minimal: FPS or app/present FPS, compact frametime, and source status.
- Detailed: average frametime, 1% low style values, pacing/stutter hint, and
  conservative frame-generation estimate.
- Research/offline: PresentMon CSV analysis and backend validation documents.

Frame-generation rules:

- Distinguish app/present FPS from displayed/generated FPS.
- Treat timing, cadence, and FPS differences as estimates or suspicions only.
- Call frame generation verified only when an explicit provider exposes
  generated or interpolated frame evidence for the current sample window.
- AMD AFMF driver-generated frames may only be visible in AMD Adrenalin overlay
  for some captures; LightCrosshair must not convert that into a verified claim
  without provider evidence.

Implementation direction:

- Keep diagnostics optional and off by default or low frequency.
- Prefer minimal and detailed display modes over a large fixed dashboard.
- Keep source labels visible when data is estimated, external, or unavailable.

## Performance And Compatibility Requirements

LightCrosshair should protect its lightweight identity with a standing
performance budget:

- Crosshair-only mode should avoid unnecessary redraws and background sampling.
- Diagnostics should update at a lower rate than rendering-critical game loops.
- Ultra-lightweight mode should disable advanced diagnostics, graphs, and any
  optional polling that is not required for the crosshair.
- Future validation should measure LightCrosshair CPU, memory, handle, GDI, and
  GPU impact during idle, crosshair-only, settings-open, and diagnostics-on
  scenarios.
- Any new feature must define its overhead, fallback behavior, and off switch.

Compatibility caveats:

- LightCrosshair avoids injection and hooks.
- Normal desktop overlays may not appear in exclusive fullscreen.
- Some games, capture modes, and anti-cheat systems may block overlays.
- RTSS, PresentMon, driver tools, or vendor overlays are external tools with
  their own compatibility and policy risks.

## Frame Cap Direction

The user-facing product need is valid, but a fake limiter would damage trust.
For now, plan a Frame Cap Assistant instead of claiming active limiting.

Realistic options:

- In-game cap guidance, including how to find and validate a game's own limiter.
- Driver control panel guidance where appropriate.
- RTSS delegation as a future opt-in path only if a supported control method,
  rollback plan, and user consent flow are approved.
- Driver profile APIs only after public API, license, permission, and rollback
  review.
- Native or hook-based limiters remain blocked behind explicit review.

Assistant behavior:

- Help the user choose an external or in-game limiter.
- Explain app/present FPS versus displayed FPS.
- Use telemetry only to validate observed results, not to claim LightCrosshair
  enforced a cap.

## Milestone 12 Implementation Plan

This milestone is documentation-only:

- Create this product direction document.
- Update the agentic development plan so Milestones 13-15 follow the product
  pillars rather than backend research momentum.
- Mark backend research as decision support, not the roadmap.
- Run the normal build, tests, preflight, diff check, and status checks.

No runtime feature, PresentMon provider, RTSS control, frame limiter, hook,
injection, native backend, vendor private API, release, tag, package
publication, or Special K code adaptation is part of Milestone 12.
