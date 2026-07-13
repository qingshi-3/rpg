# World Map Workbench Map Selection And City Cards

Status: Completed
Executor: executor
Verifier: root
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Make the map canvas and city-region workspace behave as one editor: clicking a city region or city marker must select the owning province and exact main/auxiliary city in the left UI, while the main-city and auxiliary-city controls become visually scannable cards instead of repeated text-heavy rows.

## Confirmed Discussion Result

- Clicking an authored main-city or auxiliary-city region enters the city-region workspace, selects its owning province and exact city, highlights the corresponding left-side card, and leaves the right inspector focused on the clicked geometry.
- Clicking a city location marker performs the same province/city synchronization while leaving the right inspector focused on that marker.
- Clicking empty map space may clear the map feature selection but must preserve the most recent province/city workspace context instead of falling back to the first province.
- The map, left workspace, and right inspector use one explicit selection-synchronization path; do not add a second business identity or infer city membership from labels or coordinates.
- The main city is presented as one visually prominent card with a city icon, a clear main-city badge, its name as the primary information, and an obvious selected state.
- Auxiliary cities are presented as compact selectable cards. Repetitive helper copy such as "select and edit name" is removed.
- Add-auxiliary-city is presented as the final dashed add card in the auxiliary-city group.
- Stable technical ids remain read-only advanced information and do not become ordinary authoring inputs.

## Authority Impact

Medium local workbench behavior and presentation change.

- No gameplay or system architecture authority changes: `ProvinceId` and `LocationId` remain the accepted identities, and the canonical data/published-package contracts are unchanged.
- Update `tools/world-map-workbench/README.md` before implementation with the durable canvas-to-workspace selection behavior and the main/auxiliary card hierarchy.
- Do not add a proposal, alter `AGENTS.md`, or create another task document for this scope.

## Execution Scope

1. Add one client-side synchronization path that resolves a selected city geometry or city location from its authoritative `provinceId` and `locationId` properties.
2. Synchronize the active workspace, selected province, selected city, left-side rendering, map highlight, and right-side inspector without creating recursive selection events or losing the clicked feature.
3. Preserve the last province/city context when selection is cleared or when a non-city feature is selected.
4. Rework the city-region workspace markup and styles into one prominent main-city card, compact auxiliary-city cards, clear selected states, and a dashed add card.
5. Keep rename/edit behavior, automatic ids, create-and-draw transactions, cancellation rollback, undo/redo, province switching, validation, save/reload, and publication behavior intact.
6. Add focused regression coverage for region click, marker click, empty-click preservation, main/auxiliary role hierarchy, add-card placement, and selection-state rendering.
7. Run typecheck, full workbench tests, production build, scoped static checks, `git diff --check`, and real-browser QA; relaunch the workbench afterward if its service is interrupted.

## Non-Goals

- No change to world-map geometry, mock content, final art, hover behavior, OpenLayers layer ownership, or published package schema.
- No change to province/city identity generation, rename semantics, detailed-map mapping, Strategic Management, or Godot runtime.
- No new city type, province role, drag/drop ordering, card thumbnails, or final art asset production.
- No Godot editor/main-scene launch and no modification of `C:\Users\qs\asset`.

## Constraints And Risks

- Work on the existing dirty `main` branch. Preserve all unrelated changes and do not create/switch branches or use destructive Git commands.
- `WorkbenchApp.ts` and `styles.css` already contain the preceding workbench changes; patch them incrementally without reverting or normalizing unrelated work.
- `ProvinceId` plus `LocationId` remain the only authoritative selection identity. Geometry type, mutable name, centroid, rendered icon, and list position are not identities.
- Programmatic left-side synchronization must not steal the map selection or recursively re-enter OpenLayers select handlers.
- Use semantic buttons/cards with keyboard focus and accessible selected state; appearance must not be the only selection signal.
- No installed GodotPrompter skill applies because this is the TypeScript/Web workbench, not Godot UI or runtime code.

## Acceptance Criteria

1. Clicking a main-city region switches to the city-region workspace, selects the correct province and main city, highlights its card, and keeps the right inspector on the region geometry.
2. Clicking an auxiliary-city region selects its owning province and exact auxiliary city with the same left/right consistency.
3. Clicking a main/auxiliary city marker produces the same left-side selection and keeps the right inspector on the marker.
4. Clicking empty map space does not jump the left UI to another province or city.
5. Main city is a prominent, clearly badged card; auxiliary cities are compact cards; selected states are obvious and accessible.
6. The auxiliary add action is the final dashed card and remains usable for the existing create-and-draw flow.
7. Repetitive instructional text is removed from each city entry, while rename/edit behavior and advanced read-only ids remain available.
8. Undo/redo, pending-creation cancellation, province switching, save/reopen, validation, and existing map interactions remain passing.
9. Workbench typecheck, complete Vitest suite, production build, focused static checks, browser inspection, and `git diff --check` pass with no scoped runtime exceptions.
10. Execution records evidence and hands off at `Awaiting Verification`; the independent verifier completes or returns actionable findings.

## Current Progress Snapshot

Completed:

- User confirmed the selection synchronization and card-based main/auxiliary presentation.
- Read-only diagnosis found that the OpenLayers select listener refreshes only the right inspector; it does not synchronize `selectedLocationProvinceId`, `selectedGeometryLocationId`, the active workspace, or the left workspace rendering.
- Existing city-member markup is confirmed to repeat role/name/edit copy and lacks a strong role hierarchy.
- Relevant accepted authoring architecture and preceding workbench task records have been reviewed; no authority conflict was found.
- Executor re-read the repository rules, authority map, work-item lifecycle, accepted authoring architecture, focused README, dirty `main`, and relevant client/tests; execution is now in progress.
- Focused README now records the durable canvas/workspace identity synchronization, empty-click preservation, and main/auxiliary/add-card hierarchy.
- Client implementation now resolves city markers and city geometry through one `ProvinceId + LocationId` path, preserves the exact OpenLayers selection during workspace synchronization, and keeps empty/non-city clicks from replacing the last city context.
- Main city, auxiliary city, pending creation, selected state, and final dashed add card now have distinct semantic card markup and styling; repetitive per-city helper copy is removed.
- Focused typecheck and 12 regression tests passed, covering main/auxiliary region identity, marker identity, empty-selection preservation, retained map selection, hierarchy/order, and accessible selected state.
- Complete workbench verification passed: 20 Vitest files / 66 tests, production build, scoped static assertions, and `git diff --check`.
- Edge 150 real-browser QA passed for city-region click, city-marker click, empty-click context preservation, exact inspector ownership, selected-card accessibility, role hierarchy, dashed add-card placement/focus, and removal of repeated helper copy.
- The pre-existing workbench service was not interrupted and remained available at HTTP 200 after QA; no relaunch was required.

Remaining:

- None.

## Pause Or Blocker

None. Execution and independent verification are complete.

## Resume Condition

Task is complete; resume only through a separately confirmed follow-up work item.

## Resume Entry

Archived completion record; no resume entry remains.

## Latest Verification

- `npm run typecheck`: passed.
- `npx vitest run tests/cityWorkspaceSelection.test.ts tests/interactionSafety.test.ts tests/uiHierarchy.test.ts`: passed, 3 files / 12 tests.
- `npm test`: passed, 20 files / 66 tests.
- `npm run build`: passed; Vite emitted only its non-blocking existing >500 kB chunk-size advisory.
- Scoped static source/style assertions: passed.
- `git diff --check`: passed; emitted only an unrelated existing CRLF-to-LF warning for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Edge 150 headless browser QA against `mock_qinghe_chiyan`: `chiyan_mine` region selected province `chiyan`, exact city card, regions workspace, and city-region inspector; empty click retained that context and closed the inspector; with territory rendering hidden, a city-marker click selected `qinghe_river_delta`, province `qinghe`, its auxiliary card, and the auxiliary-city inspector.
- Browser presentation/accessibility inspection: one main card and four auxiliary cards for the inspected province; main card 70 px versus auxiliary 49 px; add card was final, dashed, and keyboard-focusable; selected state used `aria-pressed`; repeated helper copy was absent.
- Browser console: no scoped runtime exception. The only error entry was Vite's unrelated `/favicon.ico` 404.
- Post-QA service check: `http://127.0.0.1:4173/` returned HTTP 200; the existing service was not interrupted.
- Independent verifier reviewed the scoped selection path and confirmed it preserves the exact OpenLayers feature while synchronizing only through `ProvinceId + LocationId`, without recursive selection or authority drift.
- Independent verifier reran `npm test`: passed 20 files / 66 tests.
- Independent verifier reran `npm run build`: typecheck, 491-module Vite production build, and server TypeScript compilation passed; only the existing non-blocking chunk-size advisory remained.
- Independent verifier reran `git diff --check`: passed with only the unrelated existing C# CRLF advisory; branch remained `main` and the workbench URL returned HTTP 200.
- Independent Edge/CDP browser QA from a fresh temporary profile clicked the `chiyan_mine` map region and confirmed province `chiyan`, exact selected card, active city-region workspace, and unchanged city-region inspector ownership. Main/auxiliary card heights were 70/49 px and the dashed add card remained last. The temporary browser/profile were removed afterward.

## Execution Record

- 2026-07-13: User confirmed execution. The task is classified as a medium Map/UI workbench change with no gameplay or system-authority update. No installed GodotPrompter skill applies.
- 2026-07-13: `executor` started scoped execution on the existing dirty `main`; unrelated changes are preserved. Architecture review confirmed this remains a client-side Map/UI selection and presentation change using the existing stable identities and OpenLayers selection owner.
- 2026-07-13: README, implementation, styling, and focused regressions completed. `npm run typecheck` passed; focused Vitest run passed 3 files / 12 tests.
- 2026-07-13: Full tests, build, static checks, diff checks, and Edge browser QA passed. Scoped execution finished with no authority change, persistent-data change, Godot launch, or external-asset modification. Handed off at `Awaiting Verification`.
- 2026-07-13: `root` independently reviewed the scoped implementation, reran the complete test/build/diff suite, and repeated real-browser region-click/card-hierarchy QA in a fresh Edge context. All acceptance criteria passed; task marked `Completed` for archive.

## Final Result

Implemented and independently verified the stable-identity synchronization between map region/marker selection and the left province/city workspace. Empty and non-city selections preserve the last city context; the clicked feature remains the right-inspector owner; main, auxiliary, selected, pending, and add actions use the confirmed accessible card hierarchy. No installed GodotPrompter skill applied. Disposition: `Completed`; remaining scoped risks: None; follow-up work: None.
