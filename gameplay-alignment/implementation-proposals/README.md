# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-07-01-battle-skill-configuration-authority.md` | Active - Proposed For Implementation | Defines the concrete cutover plan for Resource-authored battle skills, strategic grants, typed effect executors, Runtime availability, trait-driven HUD/presentation, and removal of legacy ability paths. Execution plan: `2026-07-01-battle-skill-configuration-authority-plan.md`. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |

## Archived Implementation Proposals

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
