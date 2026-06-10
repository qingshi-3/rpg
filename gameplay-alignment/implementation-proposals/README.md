# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-06-08-config-content-index-boundary.md` | Implemented - Automated Verification Passed | Manual Godot UI QA remains as an optional follow-up check before archiving. |
| `2026-06-08-battle-preparation-deployment-polish.md` | Implemented - Automated Verification Passed | Manual Godot UI QA remains as an optional follow-up check before archiving. |
| `2026-06-07-first-slice-hero-skill-content-expansion.md` | Implemented - Automated Verification Passed | Manual Godot UI QA remains as an optional follow-up check before archiving. |
| `2026-06-09-first-slice-expedition-capacity.md` | Implemented - Automated Verification Passed | Manual Godot UI QA remains as an optional follow-up check before archiving. |
| `2026-06-09-battle-combat-zone-overlap-engagement.md` | Implemented - Automated Verification Passed | Manual battle QA remains as a follow-up check before archiving. |
| `2026-06-09-battle-presentation-motion-lane-smoothing.md` | Implemented - Automated Verification Passed | Manual multi-company battle QA remains as a follow-up check before archiving. |
| `2026-06-10-battle-local-neighbor-navigation.md` | Implemented - Automated Verification Passed | Manual multi-company battle QA remains as a follow-up check before archiving. |
| `2026-06-10-battle-local-steering-navigation.md` | Implemented - Automated Verification Passed | Manual multi-company battle QA remains as a follow-up check before archiving. |
| `2026-06-10-battle-combat-pressure-advance.md` | Implemented; manual QA pending | Implements local-combat degradation so units keep pressure when attack/support entry is full. |
| `2026-06-10-battle-hierarchical-route-hints.md` | Implemented - Automated Verification Passed | Manual Bonefield multi-company battle QA remains as a follow-up check before archiving. |
| `2026-06-10-battle-player-autonomous-target-regions.md` | Implemented - Automated Verification Passed | Manual Bonefield multi-company battle QA remains as a follow-up check before archiving. |

2026-06-07 cleanup result: proposals aligned with the current implementation were moved to `archived/`; the obsolete early one-hero active-skill proposal was deleted because the current skill implementation is definition-backed, target-locked, and effect-executor based.

## Archived Implementation Proposals

Archived battle and movement implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
