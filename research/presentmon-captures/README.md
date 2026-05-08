# Local PresentMon Captures

This folder is for local, manually captured PresentMon CSV/ETL data used during
backend research. Capture files are ignored by Git because they can be large and
may include personal data such as process names, local paths, machine details,
or game/session context.

## What Goes Here

- Short PresentMon CSV exports from manual test scenarios.
- Optional ETL files captured manually outside LightCrosshair.
- Local notes that include machine-specific observations, if they are not meant
  for source control.

## What Must Not Be Committed

- Full real captures.
- Personal paths, usernames, account names, machine identifiers, or session IDs.
- PresentMon binaries.
- Game logs or crash dumps.

Use source-controlled docs under `docs/backend-research/` for anonymized
scenario summaries and decisions.
