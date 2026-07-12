# Legacy Implementation Records

This directory preserves implementation proposals and acceptance records created before the project retired its mandatory proposal lifecycle. Some records remain listed because manual QA or deferred work is still useful to track.

These records are not gameplay or architecture authority and are no longer execution gates. Do not create new implementation proposals. New work follows the global discussion -> confirmed execution baseline -> execution workflow and records durable state only in the appropriate authority, module, workstream, testcase, or acceptance document.

When resuming an exact legacy record, use its scope and verification notes as historical evidence, then reconcile them with current authority through discussion. If implementation reveals a missing or wrong rule, stop and return to discussion.

## Pending Legacy Records

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-07-12-strategic-region-preview-scene.md` | Implementation Complete - Pending User Visual QA | Implements STRATEGIC-REGION-PREVIEW-001 as an independently runnable two-city/eight-region Godot preview scene without changing current gameplay entry, runtime, or persistence. Automated verification and visual capture pass; keep active only for requested interactive F6 QA. |
| `2026-07-08-hero-corps-reassignment-workbench.md` | Implemented - Pending Manual QA | Implements SM-HERO-CORPS-001; focused Strategic Management checks and the complete Presentation regression suite pass. Keep active only for the requested in-game manual QA. |
| `2026-06-19-site-map-layout-first-city.md` | Paused - Scaffold Verified, Detailed Authoring Deferred | Keeps the SMLA-001 city-map scaffold and bridge marker contract, but defers detailed TileMapLayer/map-content authoring until the operational loop is validated on `DemoSite`. |

## Archived Legacy Records

Completed, superseded, and historical implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
