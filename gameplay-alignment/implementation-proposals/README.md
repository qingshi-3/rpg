# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-06-20-strategic-operation-foundation-loop.md` | Proposed - Not Started | Implements STRAT-OPS-001 foundation strategic loop after authority merge: bounded city construction, foundation resources, reserve soldiers, recruitment/replenishment, hero company expedition, and target occupation/development; full local battle support remains a later slice. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |
| `2026-06-19-strategic-city-dispatchable-company-roster.md` | Implemented - Automated Verification Passed | Fixes selected-city expedition roster so dispatched hero companies leave the source city's available company list; editor manual QA remains pending. |
| `2026-06-19-strategic-battle-result-army-carrier-cleanup.md` | Implemented - Automated Verification Passed | Fixes Strategic Management battle return so resolved expedition world-map carriers cannot keep retriggering the same assault; editor manual QA remains pending. |
| `2026-06-19-strategic-battle-launch-snapshot-sync.md` | Implemented - Automated Verification Passed | Fixes Strategic Management battle launch so Runtime consumes the final deployment snapshot instead of the early active-context snapshot. |
| `2026-06-11-thunder-mark-demo-skill-family.md` | In Progress | Implements the accepted thunder-mark demo skill family first slice: mark throw, legal teleport, and channeled melee damage. |

## Archived Implementation Proposals

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
