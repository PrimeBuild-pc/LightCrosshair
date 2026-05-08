\# LightCrosshair 1.4.0 Agentic Development Plan



\## Completed

\- Milestone 1: packaging baseline 1.4.0

\- Milestone 2: frame pacing statistics

\- Milestone 3A: diagnostic FPS overlay

\- Milestone 3B: settings toggle for diagnostics

\- Milestone 4A: real frame pacing feasibility

\- Milestone 4B: frame generation detection feasibility

\- Milestone 4C: conservative frame generation detection fallback



\## Next

\### Milestone 4D

Design a real backend path for Special K-like frame generation detection:

\- native/in-process feasibility

\- NGX/DLSSG/Streamline signal access

\- PresentMon/RTSS external integration alternatives

\- anti-cheat risk review

\- GPL/licensing review

\- no implementation of injection without explicit approval



\### Milestone 5

Frame limiter backend architecture:

\- IFrameLimiterBackend

\- NoOp/Unavailable backend

\- RTSS or external backend if feasible

\- native backend plan if required



\### Milestone 6

Manual validation tools and diagnostics:

\- test profiles

\- telemetry logging

\- debug overlay

\- reproducible validation checklist



\### Milestone 7

Packaging validation:

\- Inno Setup

\- portable ZIP

\- Chocolatey metadata

\- WinGet manifest prep

\- PowerShell installer

\- no release publication without approval

