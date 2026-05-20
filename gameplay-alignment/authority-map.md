# Authority Map

This document records which document family wins when older material conflicts with the new direction.

## Authority Order

1. `gameplay-design/`: accepted player-facing gameplay and content-system rules.
2. `system-design/`: accepted implementation architecture and system contracts.
3. `gameplay-alignment/implementation-proposals/`: implementation proposals and acceptance records, only when they reference current accepted authority documents.
4. `design-proposals/active/<proposal>/expected/`: proposal-stage document changes; not code implementation authority.
5. `design-proposals/archived/`: historical records; not active authority.

## Default Conflict Rule

When implementation proposals, active proposals, code, resources, or historical notes conflict with accepted `gameplay-design/` or `system-design/`, do not silently follow the lower-authority source.

Instead:

```text
identify the conflict
-> register or update a gap
-> use a design proposal if authority documents must change
-> archive the design proposal after accepted documents are updated
-> use a focused implementation proposal before code changes
-> repair implementation and historical notes through that scoped workstream
```

## Active Proposal Rule

Active design proposals are for changing accepted documents. They do not authorize code changes directly. After a design proposal is accepted, merge it into the authority documents and archive it before starting implementation planning.

Implementation work starts from a focused implementation proposal under `gameplay-alignment/implementation-proposals/`, not from `design-proposals/active/`.

Proposal relationship metadata must be visible in the default AI-readable proposal entry, not buried in deep notes. At minimum, record the requirement id, parent proposal, supersedes/superseded-by links, amends/amended-by links, related implementation proposal, and affected authority documents.

Archived proposals remain immutable historical records. If a rollback, reopen, amendment, or supersession is needed, create a new proposal and update only index/relationship metadata on the archived entry so future agents can follow the chain without reading archived bodies.

## Archived Proposal Rule

Archived proposal bodies are not current design input. Read only `design-proposals/archived/README.md` summaries unless the user explicitly requests a specific archive.
