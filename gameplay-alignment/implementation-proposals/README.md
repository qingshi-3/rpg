# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

These proposal files remain in this directory because implementation or acceptance is still open:

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-06-03-battle-group-layered-runtime.md` | Implemented - Pending Manual QA | Automated regression and project build pass; bonefield manual QA remains open before archiving. |
| `2026-06-03-battle-combat-zones-group-action-zones.md` | In Progress | Implements accepted global combat-zone and group action-zone architecture plus area snapshot diagnostics. |

## Archived Implementation Proposals

Archived battle and movement implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
