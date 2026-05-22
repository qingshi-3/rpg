# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

These proposal files remain in this directory because performance diagnosis or manual QA is still open:

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-05-21-battle-movement-performance.md` | Archived - accepted with residual runtime-spike follow-up | Performance workstream remains a parent record for unresolved runtime-spike follow-up. |
| `2026-05-21-battle-runtime-spike-diagnostics.md` | Implemented - pending manual QA | Performance diagnostics require manual QA evidence. |
| `2026-05-21-battle-open-attack-flowfield-cache.md` | Implemented - pending manual QA | Performance optimization requires manual QA evidence. |

## Archived Implementation Proposals

Accepted battle and movement architecture proposals that no longer need active follow-up live under `archived/`.
