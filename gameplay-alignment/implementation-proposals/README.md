# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-07-09-battle-preparation-targeting-flow.md` | Draft - Ready For Implementation | Implements UI-BATTLE-PREP-TARGET-001: battle preparation removes the bottom plan bar, uses a lower-right launch button, and enters left-click destination targeting with a curved map guide after placement. |
| `2026-07-08-strategic-world-detail-bottom-bounce.md` | Implemented - Pending Manual QA | Implements UI-WORLD-DETAIL-BOUNCE-001: strategic-world selected-city operation sheet enters from below the screen with the shared q-bounce popup feel. |
| `2026-07-08-recruitment-centered-modal-bounce.md` | Implemented - Pending Manual QA, Presentation Suite Blocked By Unrelated Taxonomy Guard | Implements UI-RECRUIT-MODAL-BOUNCE-001: entered-city recruitment opens as a centered modal with backdrop fade, overshoot settle, and reverse close bounce. |
| `2026-07-08-site-management-fullscreen-tab-rail.md` | In Progress | Implements UI-SITE-001: entered-city management keeps the map fullscreen and uses a left tab rail plus task-sized overlay panels. |
| `2026-07-08-battle-map-operation-hud-suppression.md` | In Progress | Implements UI-BATTLE-HUD-001: battle map operations must suppress blocking screen-space HUD and restore the previous battle HUD layer after submit/cancel. |
| `2026-07-08-hero-corps-reassignment-workbench.md` | Implemented - Pending Manual QA, Presentation Suite Blocked By Unrelated Taxonomy Guard | Implements SM-HERO-CORPS-001; focused Strategic Management/build/UI guards pass, while the unrelated TileSets taxonomy guard remains open. |
| `2026-07-05-unit-idle-preview-presentation-contract.md` | Implemented - Pending Manual QA | Shared Presentation contract and current military UI adoption are implemented; keep active until the user verifies the in-game visual result. |
| `2026-07-05-ui-hover-presentation-contract.md` | Implemented - Focused Guard Passing, Full Suite Blocked By Unrelated Taxonomy Guard | Keeps UI-HOVER-001 open until the unrelated resource taxonomy guard is cleared or formally accepted as out of scope for final implementation acceptance. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |

## Archived Implementation Proposals

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
