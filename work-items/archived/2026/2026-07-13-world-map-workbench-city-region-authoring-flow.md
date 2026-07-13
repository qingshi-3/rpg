# World Map Workbench City Region Authoring Flow

Status: Completed
Executor: executor
Verifier: root
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Replace the workbench's parallel province/location authoring prompts with one domain-level city-region workflow. Creating a province or auxiliary city must generate stable technical identities automatically and move directly into map-region drawing, so early geography authoring is not blocked by naming or identifier entry.

## Confirmed Discussion Result

- Province and location remain separate canonical data responsibilities, but the authoring UI must not expose them as parallel creation workflows.
- One province owns exactly one main-city location and zero or more auxiliary-city locations; every city location owns one exact city-region geometry. The province-to-city relationship is one-to-many, while city location to city-region geometry is one-to-one.
- `ProvinceId`, `LocationId`, and province `LayoutId` are generated automatically by one collision-safe authoring identity factory. They are stable after creation and are never regenerated when display names change.
- Initial persisted display names may equal the generated stable ID. Naming is editable later and must not interrupt drawing.
- Technical IDs are visible only as read-only advanced/diagnostic information; ordinary creation never asks the user to type them.
- `MapId` remains explicitly authored because it owns map-scoped directories and is immutable after creation.
- Creating a province atomically creates the province, its layout identity, and its main-city location, selects the new city, and immediately enters region-polygon drawing.
- Adding an auxiliary city under the selected province atomically creates its location identity, selects it, and immediately enters region-polygon drawing.
- Completing the first region polygon creates/binds the location geometry and places the location marker at the polygon centroid. The marker remains editable later through the existing location editing tools.
- Draft save remains permissive. Placeholder ID-based names may produce a non-blocking completion reminder, but they do not prevent drawing or draft save; existing publish topology and exactly-one-main-city rules remain authoritative.

## Authority Impact

Medium authoring-workflow and stable-identity architecture change.

- Update `system-design/strategic-world-map-authoring-architecture.md` before implementation to state that the workbench auto-generates province/location/layout identities and treats province creation plus its required main city as one authoring transaction.
- Update `tools/world-map-workbench/README.md` with the unified create-and-draw workflow and advanced-only identity presentation.
- No gameplay authority, published package schema, runtime identity, or province/location cardinality change.

## Execution Scope

1. Add a central collision-safe prefixed ID factory for `ProvinceId`, `LocationId`, and `LayoutId`, with focused deterministic tests through injected randomness or an equivalent test seam.
2. Replace province create/edit prompt chains with immediate creation and later inline/property editing. Existing names remain editable; stable IDs remain read-only.
3. Make new-province creation produce the required main-city child and enter exact region drawing without a second location-creation step.
4. Add an `添加辅城区域` action under the selected province that creates one auxiliary-city location and enters exact region drawing.
5. On first polygon completion, bind the geometry to the pending location and initialize its point marker from the polygon centroid without creating duplicate identities or geometry.
6. Present the city-region workspace as province aggregates with main/auxiliary children. Keep technical identities out of the primary flow and available as read-only advanced information.
7. Preserve undo/redo, dirty-state protection, save/reopen, validation, publication, map switching, existing authored geography, and non-city strategic-location editing.
8. Add focused unit/static/browser coverage and relaunch the workbench for user acceptance.

## Non-Goals

- No change to province/location one-to-many cardinality.
- No merge of province and location canonical records.
- No automatic naming from geography, reverse geocoding, final content naming, or final map artwork.
- No change to `MapId` creation or immutability.
- No gate/pass, resource-site, ruin, dungeon, or opportunity creation redesign beyond preserving existing behavior.
- No published schema v2, Godot runtime, scenario, save, detailed-map mapping, or deployment-entry change.
- No Godot launch and no modification of `C:\Users\qs\asset`.

## Constraints And Risks

- Work on dirty `main`; preserve all Stage 0-2.5 and unrelated active-task changes.
- Do not create/switch branches, use destructive Git commands, or run concurrent write-capable Agents.
- Identity generation must be collision-safe within the current map and must not derive stable identity from mutable display names or geometry.
- The province/main-city aggregate mutation and pending draw state must be undoable as one user operation or roll back completely when drawing is cancelled.
- Existing maps and fixtures must load unchanged; no migration may rewrite their stable identities merely to fit the new workflow.
- Do not weaken exactly-one-main-city, geometry topology, publish, or package isolation validation.

## Acceptance Criteria

1. `新建省份` requests no ProvinceId, LocationId, LayoutId, or display name and immediately starts drawing the new main-city region.
2. The new province, layout, and main-city location receive unique stable prefixed IDs; initial names equal their generated IDs and remain independently renameable without changing IDs.
3. Cancelling the initial draw leaves no orphan province, layout binding, location, point marker, or geometry and does not create an undo-history fragment.
4. Completing the polygon creates exactly one main-city location geometry and one point marker at its polygon centroid, selects it, marks the draft dirty, and supports undo/redo as one coherent operation.
5. `添加辅城区域` is contextual to the selected province, requests no technical data, creates exactly one auxiliary-city child, and follows the same draw/centroid/rollback rules.
6. Province aggregate UI shows one main child and auxiliary children; ordinary controls focus on selection, drawing, and later display-name editing. Technical IDs are read-only advanced information.
7. Renaming a province or city changes only its display name; every stable ID, geometry binding, province membership, layout binding, and detailed-map mapping reference remains unchanged.
8. Existing province/location/geometry data, non-city location tools, save/reopen, undo/redo, validation, publish, multi-map isolation, and responsive canvas tests remain passing.
9. Workbench typecheck/build, full Vitest suite, focused browser inspection, and `git diff --check` pass; the service is relaunched for user acceptance.
10. Execution records exact evidence and hands off at `Awaiting Verification`; an independent verifier accepts before completion/archive.

## Current Progress Snapshot

Completed:

- User confirmed that the current raw province/location parallel workflow is too cumbersome.
- User confirmed automatic technical IDs, ID-based initial names, and delayed renaming so drawing remains the primary action.
- Active task contract created; no implementation changes have started.
- Executor read the required authority, work-item route, focused architecture, workbench README, and dirty `main` status/diff overlap; no direction conflict was found.
- Focused system authority and workbench README now define the generated-identity create-and-draw transaction, centroid marker, complete cancellation rollback, delayed naming, and advanced-only identity presentation.
- Implemented a central collision-retrying `ProvinceId` / `LocationId` / `LayoutId` factory with injectable entropy, unified province/main-city and auxiliary-city creation, immediate polygon drawing, centroid marker initialization, and one-snapshot undo/redo ownership.
- Removed main/auxiliary cities from the parallel generic location-placement flow while preserving all non-city location types; province and city names remain editable without changing identity or bindings.
- Added aggregate province/main/auxiliary UI, contextual auxiliary creation, explicit pending cancellation, read-only advanced identities, and non-blocking placeholder-name reminders.
- Focused identity, rollback, UI hierarchy, and reminder tests pass; typecheck passes; the full Vitest suite currently passes 61/61 tests across 19 files.

Remaining:

- None. Independent verification passed and the task is ready for archive.

## Pause Or Blocker

None. If existing editor interaction ownership cannot roll back province plus pending main-city creation atomically, or authority contradicts automatic identity generation, set `Needs Discussion` before continuing.

## Resume Condition

Resume from this task on dirty `main` with no concurrent workbench writer.

## Resume Entry

1. Read repository `AGENTS.md`, `gameplay-alignment/authority-map.md`, `work-items/README.md`, this task, `system-design/strategic-world-map-authoring-architecture.md`, and the workbench README.
2. Confirm the current branch remains dirty `main` and inspect overlapping workbench files before edits.
3. Set `In Progress`, update focused authority/README, then implement identity generation and the pending city-region draw transaction before changing the visible hierarchy.
4. Stop/relaunch only the workbench development service as needed; do not start Godot.

## Latest Verification

- Independent verifier `root` reran `npm run typecheck`, the full Vitest suite (19 files / 61 tests), `npm run build`, and `git diff --check`; all passed. The only diff-check output was the pre-existing unrelated CRLF advisory.
- Independent Edge/CDP QA used a fresh temporary profile and did not save fixture data. It independently confirmed complete cancellation rollback, generated stable identities, main-city centroid `X 1024 · Y 512`, coherent undo/redo with restored selection context, rename without identity changes, auxiliary-city centroid `X 1378 · Y 512`, auxiliary cancellation without history/cardinality changes, and an empty browser runtime-exception list. The temporary browser profile was removed afterward.
- The listed in-app browser skill resource was unavailable on disk, so both execution and verification used isolated Edge/CDP fallback without touching the user's browser session.

- `npm run typecheck`: passed explicitly during implementation and again inside the final production build.
- `npm test`: passed all 61 tests across 19 files, including identity collision/retry/failure, pending rollback, UI hierarchy, placeholder-name reminders, existing map/publish/artifact/API coverage, and all prior workbench regression tests.
- `npm run build`: passed TypeScript client/server checks and Vite production build (490 modules; `dist/client/assets/index-Bq32jrTO.js` and `index-CgfQTxH-.css`). Vite emitted only its non-blocking greater-than-500-kB chunk-size advisory.
- Scoped static checks confirmed the unified actions, generated main/auxiliary roles, central identity factory, centroid path, read-only identities, selection-bearing history snapshots, and warning-only name reminders.
- Full `git diff --check`: exit 0. It printed only the pre-existing unrelated CRLF-to-LF advisory for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Real browser QA passed in a separate temporary Edge `150.0.4078.65` headless/CDP profile at `1600x1000`, against explicit fixture `fixture_north_pass`; the profile was removed afterward and no fixture save was performed.
  - Initial state: 2 provinces, clean draft, empty undo/redo history.
  - New-province pending state: 3 provinces; generated `province_`, `layout_`, and `location_` identities; still clean with no history entry. Cancel restored 2 provinces, clean state, and empty history.
  - Completed main-city polygon: generated aggregate selected; marker selected as `主城` at polygon centroid `X 1024 · Y 512`; one undo removed the whole province/main-city/geometry aggregate and redo restored it plus its selected province context.
  - Province and main-city renames preserved exact advanced identities. Browser sample remained `province_4b09045d87d8`, `layout_57929e801ca0`, and `location_cc8955f16bd0` before and after rename.
  - Completed auxiliary polygon selected `辅城` at centroid `X 1378 · Y 512`; auxiliary undo/redo restored the same aggregate, and a second auxiliary pending creation cancelled without changing aggregate cardinality or history availability.
  - Browser runtime exception list was empty.
- Workbench development service was relaunched after QA. Health is `{"ok":true,"projectRoot":"D:\\godot\\rpg"}` on `127.0.0.1:4174`; the Vite acceptance page returns HTTP 200 on `127.0.0.1:4173`.

## Execution Record

- 2026-07-13: User confirmed execution of automatic IDs and unified province/main-or-auxiliary city-region creation. No installed GodotPrompter skill applies because this is a TypeScript/Web authoring-tool workflow; browser verification will use the local Edge/CDP fallback because the listed browser skill resource is unavailable.
- 2026-07-13: Execution started on dirty `main`. Existing Stage 0-2.5/user changes overlap the workbench, focused architecture, and README, so implementation will be applied as incremental patches without branch changes, cleanup, or destructive Git operations.
- 2026-07-13: Implementation milestone passed `npm run typecheck` and `npm test` (19 files, 61 tests). Focused coverage includes injected-entropy collision retry/failure, pending aggregate rollback without history mutation, unified UI ownership, centroid-path presence, and warning-only ID-name reminders.
- 2026-07-13: Isolated Edge/CDP QA exposed and repaired initial history-button synchronization, deferred OpenLayers single-click selection stealing the centroid marker selection, redo selection-context loss, and the inspector obscuring immediate auxiliary drawing. The full browser flow then passed with no runtime exceptions.
- 2026-07-13: Final full suite, production build, scoped static checks, full diff check, service relaunch, and health/page probes passed. Scoped execution moved to `Awaiting Verification`; no Godot process was started and `C:\Users\qs\asset` was not modified.

## Final Result

Implemented and independently verified the confirmed unified province/main-or-auxiliary city-region authoring flow. Stable technical identities are collision-safe and advanced/read-only; creation immediately draws, completion materializes one location marker at the polygon centroid, cancellation fully rolls back, and undo/redo preserves the whole aggregate plus selection context. Existing non-city tools, schema/runtime boundaries, permissive draft saving, strict publication validation, and prior workbench behavior remain covered. The workbench remains running for user acceptance.
