# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-07-10-strategic-world-map-workbench.md` | Implementation Complete - Pending User Content QA | Implements STRATEGIC-WORLD-MAP-001 as an isolated local TypeScript/OpenLayers/Turf/Node/Sharp geography editor under `tools/world-map-workbench/`; automated, build, API, and synthetic browser QA pass. Keep active for production geography and Godot integration QA. |
| `2026-07-10-lightweight-reserve-recovery.md` | Implementation Complete — Pending Manual QA | Implements SM-RESERVE-RECOVERY-001; Strategic Management and Presentation regression suites pass after stale resource-taxonomy and foundation-atlas test constraints were corrected. Keep active only for the requested in-game manual QA. |
| `2026-07-08-hero-corps-reassignment-workbench.md` | Implemented - Pending Manual QA | Implements SM-HERO-CORPS-001; focused Strategic Management checks and the complete Presentation regression suite pass. Keep active only for the requested in-game manual QA. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |

## Archived Implementation Proposals

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
