# Authority Map

This document records which document family wins when older material conflicts with the new direction.

## Authority Order

1. `gameplay-design/`: accepted player-facing gameplay and content-system rules.
2. `system-design/`: accepted implementation architecture and system contracts.
3. `design-proposals/active/<proposal>/expected/`: accepted but not-yet-merged proposal copies, only for the proposal's implementation scope.
4. `docs/`: existing project documentation and historical implementation material.
5. `design-proposals/archived/`: historical records; not active authority.

## Default Conflict Rule

When older `docs/` material conflicts with accepted `gameplay-design/` or `system-design/`, do not silently follow the old document.

Instead:

```text
identify the conflict
-> register or update a gap
-> use a design proposal if authority documents must change
-> repair implementation and old docs through a scoped workstream
```

## Archived Proposal Rule

Archived proposal bodies are not current design input. Read only `design-proposals/archived/README.md` summaries unless the user explicitly requests a specific archive.
