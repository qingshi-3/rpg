# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-07-05-unit-idle-preview-presentation-contract.md` | Implemented - Pending Manual QA | Shared Presentation contract and current military UI adoption are implemented; keep active until the user verifies the in-game visual result. |
| `2026-07-05-ui-hover-presentation-contract.md` | Implemented - Focused Guard Passing, Full Suite Blocked By Unrelated Taxonomy Guard | Keeps UI-HOVER-001 open until the unrelated resource taxonomy guard is cleared or formally accepted as out of scope for final implementation acceptance. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |

## Archived Implementation Proposals

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
