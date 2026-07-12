# Acceptance

Status: Archived.

## User Direction

The user accepted the recommended independent design proposal:

```text
UI-HOVER-001: UI Hover Presentation Contract
```

Accepted core rules:

- `TooltipText` is only for short, low-risk, layout-free text hints.
- Complex details must use authored tooltip scenes, not ad hoc UI built inside business controls.
- Map and battle hover overlays remain owned by their systems, but should share positioning, styling, and naming boundaries.
- Hover presentation must not own gameplay or state authority; it may only consume view models, rule results, or runtime snapshots.

## Merge State

- Current authority copy prepared: Yes
- Expected authority copy prepared: Yes
- Merged to authority documents: Yes
- Archived: Yes
- Follow-up implementation proposal created: Yes
