# Implementation Proposals

This directory stores focused implementation proposals and acceptance records for code, scene, resource, data, and old-document repair work that implements current accepted authority.

Implementation proposals are not gameplay or architecture authority. They must reference the accepted `gameplay-design/` and `system-design/` documents they implement, define scope and non-goals, list touched systems, tests, diagnostics, manual QA, and record acceptance evidence after verification.

Use this directory only after the relevant accepted authority is current. If implementation reveals a missing or wrong design rule, stop implementation and use the design proposal flow instead.

## Active / Pending Index

| Proposal | Status | Reason Kept Active |
|---|---|---|
| `2026-06-15-strategic-management-decomposition-followup.md` | Pending | Tracks the required split of newly oversized Strategic Management command and regression-test files after the current integration push. |
| `2026-06-14-strategic-expedition-retarget-authority.md` | Implemented - Automated Verification Passed | Fixes selected strategic army retargeting so Strategic Management expedition intent stays aligned with the world-army movement adapter before battle entry. |
| `2026-06-14-strategic-battle-active-context-cutover.md` | Implemented - Automated Verification Passed | Cuts Strategic Management-backed battle active handoff and result summary generation over to Bridge Active Context instead of legacy request/result/handoff authority. |
| `2026-06-14-strategic-battle-result-legacy-writeback-retirement.md` | Implemented - Automated Verification Passed | Retires legacy world result writeback from Strategic Management-backed battle results while preserving return notice and presentation cleanup. |
| `2026-06-14-strategic-management-bonefield-reward-and-hero-feedback.md` | Implemented - Automated Verification Passed | Bonefield battle result now records Strategic Management reward, hero reaction, progression, equipment sample, defeat feedback, and one-time reward facts for the first playable loop. |
| `2026-06-14-strategic-management-multi-company-expedition.md` | In Progress | Seeds three first-slice hero companies and lets one Strategic Management expedition carry 1-3 selected companies. |
| `2026-06-14-strategic-battle-bridge-identity-writeback.md` | Implemented - Automated Verification Passed | Preserves Strategic Management expedition/hero/corps identity through battle bridge preparation and applies battle results back to Strategic Management state. |
| `2026-06-14-strategic-management-expedition-authority.md` | Implemented - Automated Verification Passed | Moves player expedition formation from old garrison unit counts to Strategic Management hero-company and corps-instance state. |
| `2026-06-14-strategic-world-location-detail-cutover.md` | Implemented - Automated Verification Passed | Cuts the large-map selected-location detail panel over to Strategic Management location dashboards and stops showing legacy facility/garrison facts. |
| `2026-06-14-strategic-world-resource-bar-cutover.md` | Implemented - Automated Verification Passed | Cuts the large-map top resource bar over to Strategic Management resources so elapsed-time settlement is visible. |
| `2026-06-14-strategic-world-clock-settlement-bridge.md` | Implemented - Automated Verification Passed | Bridges each running large-map clock settlement into Strategic Management elapsed-time settlement without replacing legacy `WorldTick` internals. |
| `2026-06-14-strategic-world-clock-presentation-terminology.md` | Implemented - Automated Verification Passed | Cleans player-facing strategic world-clock wording so internal ticks are not presented as turns or steps. |
| `2026-06-14-strategic-management-site-entry-timeflow.md` | Implemented - Automated Verification Passed | Wires site-management scene entry and return boundaries to the Strategic Management paused/running timeflow gate. |
| `2026-06-14-strategic-management-world-timeflow-boundary.md` | Implemented - Automated Verification Passed | Replaces step-oriented settlement naming with elapsed world-map time semantics and adds the first paused/running timeflow gate. |
| `2026-06-14-strategic-management-step-advancement.md` | Superseded | Step-oriented naming has been replaced by the world timeflow boundary slice after accepted authority clarified Sanguo Qunying-style realtime world-map time. |
| `2026-06-14-strategic-management-resource-production-settlement.md` | Implemented - Automated Verification Passed | First resource-site passive production settlement now runs through Strategic Management commands; optional manual QA can confirm the resource-site production summary. |
| `2026-06-14-strategic-management-non-city-location-dashboard.md` | Implemented - Automated Verification Passed | Minimum read-only Strategic Management dashboard now appears for mapped non-city strategic locations; optional manual QA can confirm resource-site panel behavior in Godot. |
| `2026-06-14-strategic-management-map-site-mapping.md` | Implemented - Automated Verification Passed | Temporary all-sites-to-first-city mapping has been replaced with explicit Strategic Management map-site resolution; optional manual QA can confirm non-city site behavior. |
| `2026-06-14-strategic-management-command-buttons.md` | Implemented - Automated Verification Passed | First usable Strategic Management panel commands now route through Application commands; optional manual QA can confirm button interaction in Godot. |
| `2026-06-14-strategic-management-dashboard-ui-binding.md` | Implemented - Automated Verification Passed | Existing Godot site management HUD now reads the Strategic Management dashboard; optional manual QA can confirm the left city panel display. |
| `2026-06-14-strategic-presentation-cutover.md` | Implemented - Automated Verification Passed | First Strategic Management presentation boundary slice completed; optional manual QA begins after a later Godot UI binding slice. |
| `2026-06-14-strategic-management-core-foundation.md` | Implemented - Automated Verification Passed | First clean Strategic Management foundation slice completed; UI, expedition replacement, battle bridge wiring, and save/load remain later slices. |
| `2026-06-13-unit-animation-timing-policy-extraction.md` | Implemented - Automated Verification Passed | P1 battle Presentation cleanup completed; optional manual QA can confirm idle, move, attack, skill cast, hit, and defeated cue pacing. |
| `2026-06-13-battle-grid-highlight-geometry-extraction.md` | Implemented - Automated Verification Passed | P1 battle Presentation cleanup completed; optional manual QA can confirm hover footprint frames, skill range borders, target lock rings, and path arrows. |
| `2026-06-13-battle-unit-hit-feedback-presenter.md` | Implemented - Automated Verification Passed | P1 battle Presentation cleanup completed; optional manual QA can confirm attack/skill hit pulse, impact FX, damage number, and death timing. |
| `2026-06-13-battle-presentation-decomposition-followup.md` | Implemented - Automated Verification Passed | Battle-facing Presentation cleanup completed; optional manual QA can confirm objective planning, battle-preparation controls, and live runtime playback feedback. |
| `2026-06-13-world-site-root-presentation-decomposition.md` | Implemented - Automated Verification Passed | TD-004 cleanup batch 3 completed; optional manual QA can confirm site management, battle-preparation drag, and runtime command HUD interactions. |
| `2026-06-13-strategic-army-command-application-boundary.md` | Implemented - Automated Verification Passed | Batch 2 completed; optional manual QA can confirm strategic map army command interactions. |
| `2026-06-13-scene-transition-and-autobattle-authority.md` | Implemented - Automated Verification Passed | Batch 1 completed; manual Godot scene-entry QA remains optional before archiving. |
| `2026-06-11-thunder-mark-demo-skill-family.md` | In Progress | Implements the accepted thunder-mark demo skill family first slice: mark throw, legal teleport, and channeled melee damage. |

## Archived Implementation Proposals

Archived battle and movement implementation records live under `archived/`. Archived records are historical evidence, not active implementation instructions.
