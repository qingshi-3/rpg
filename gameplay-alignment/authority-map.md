# Authority Map

This document records which document family wins when older material conflicts with the new direction.

## Authority Order

1. `gameplay-design/`: accepted player-facing gameplay and content-system rules.
2. `system-design/`: accepted implementation architecture and system contracts.
3. `docs/50-production/technical-changes/`: implementation proposals and acceptance records, only when they reference current accepted authority documents.
4. `docs/`: existing project documentation and historical implementation material.
5. `design-proposals/active/<proposal>/expected/`: proposal-stage document changes; not code implementation authority.
6. `design-proposals/archived/`: historical records; not active authority.

## Default Conflict Rule

When older `docs/` material, implementation proposals, active proposals, code, or resources conflict with accepted `gameplay-design/` or `system-design/`, do not silently follow the lower-authority source.

Instead:

```text
identify the conflict
-> register or update a gap
-> use a design proposal if authority documents must change
-> archive the design proposal after accepted documents are updated
-> use a technical-change implementation proposal before code changes
-> repair implementation and old docs through that scoped workstream
```

## Active Proposal Rule

Active design proposals are for changing accepted documents. They do not authorize code changes directly. After a design proposal is accepted, merge it into the authority documents and archive it before starting implementation planning.

Implementation work starts from a focused technical-change proposal under `docs/50-production/technical-changes/`, not from `design-proposals/active/`.

Proposal relationship metadata must be visible in the default AI-readable proposal entry, not buried in deep notes. At minimum, record the requirement id, parent proposal, supersedes/superseded-by links, amends/amended-by links, related implementation proposal, and affected authority documents.

Archived proposals remain immutable historical records. If a rollback, reopen, amendment, or supersession is needed, create a new proposal and update only index/relationship metadata on the archived entry so future agents can follow the chain without reading archived bodies.

## Archived Proposal Rule

Archived proposal bodies are not current design input. Read only `design-proposals/archived/README.md` summaries unless the user explicitly requests a specific archive.
