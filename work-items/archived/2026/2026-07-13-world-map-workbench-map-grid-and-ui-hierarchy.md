# World Map Workbench Map Grid And UI Hierarchy Repair

Status: Completed
Executor: executor
Verifier: root
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Repair the multi-map workbench's first-use and information architecture so a user can create a map with an intentional Chunk grid, safely expand that grid, distinguish authoring maps from verification fixtures, and manage provinces inside the current map's city-region workflow instead of beside map-document commands. The map canvas must track real browser viewport changes during normal use.

## Confirmed Discussion Result

- `Chunk` is a structural grid unit, not a freely dragged object. Chunk identity, origin, and dimensions remain derived from one map-level grid contract.
- A new map must no longer silently use the hardcoded 4x2 default. Creation must collect immutable MapId, display name, Chunk columns, and Chunk rows; first-version Chunk size remains fixed at 1024x1024.
- Existing maps need map-level safe grid expansion. Initial implementation may add columns on the right and rows on the bottom so existing world coordinates and authored content never shift. Shrinking, arbitrary per-Chunk resizing, and left/top coordinate migration are excluded until separately designed.
- Each Chunk may still own/import its own reference, visual, terrain, territory, and later navigation artifacts; structural size and placement are not edited independently.
- `fixture_north_pass` is a replacement-verification fixture, not the normal authoring entry. The map catalog must distinguish authoring maps from fixtures and identify an explicit default authoring map. The workbench restores the last successfully opened map when it still exists, otherwise opens the catalog default; it must not fall back to lexicographic first entry.
- Top-level toolbar commands are map/document operations only: map selection/management, validation/publish, save state, undo/redo, layers, and save.
- Province create/edit/delete operations remain necessary because ProvinceId, name, LayoutId, one main city, and auxiliary membership are authored facts. These operations move into the `城市区域` workspace with a visible province list/selection and contextual actions.
- The browser launch issue was partly caused by a fixed automation viewport, but the application must still explicitly refresh the OpenLayers map on container/window resize and have regression coverage for responsive ownership.

## Authority Impact

Medium cross-file behavior and authoring-contract change.

- Update `system-design/strategic-world-map-authoring-architecture.md` only where needed to state that the editor derives Chunk identity/origin from map-level columns/rows, preserves coordinates during supported expansion, and keeps fixture/default catalog metadata editor-only.
- Update `tools/world-map-workbench/README.md` with the new-map grid workflow, safe expansion boundary, default/fixture behavior, and province UI route.
- No gameplay authority changes.
- Do not create a proposal or add these task details to `AGENTS.md`.

## Execution Scope

### 1. Catalog And Default Selection

- Version or compatibly extend the map catalog with explicit default-map identity and per-entry authoring/fixture kind.
- Mark `mock_qinghe_chiyan` as the default authoring map and `fixture_north_pass` as a fixture.
- Restore a valid last-opened MapId in the client; otherwise use the catalog default.
- Keep fixtures available for explicit verification without presenting one as the ordinary first map.

### 2. New-Map Grid Wizard

- Replace prompt chaining with an authored modal/dialog or equivalent structured in-app form.
- Collect MapId, display name, columns, and rows with clear validation and a total world-size preview.
- Keep Chunk width/height fixed at 1024 and generate deterministic IDs, coordinates, origins, and draft paths from columns/rows.
- Enforce practical bounded positive dimensions and reject invalid/oversized grids before filesystem mutation.

### 3. Safe Grid Expansion

- Add map settings in an appropriate map-level UI, not as per-Chunk structural editing.
- Support increasing columns to the right and rows to the bottom while preserving every existing Chunk ID/origin and all authored world coordinates.
- Generate only the newly required Chunk definitions and paths; initialize their editable terrain state without overwriting existing media or data.
- Reject shrinking or any operation that would relocate/delete existing content. Save, validation, publication, and reload must consume the expanded grid normally.

### 4. Province UI Hierarchy

- Remove province CRUD from the global toolbar.
- Add province list/selection and contextual create/edit/delete actions to the `城市区域` workspace.
- Make the selected province's ProvinceId, name, LayoutId, main city, and auxiliary membership understandable without changing their authoritative data ownership.
- Keep incomplete drafts allowed; publishing still blocks provinces without exactly one main city.

### 5. Responsive Canvas

- Ensure the workbench shell and OpenLayers canvas update when the browser/container size changes.
- Avoid fixed viewport dimensions in application code.
- Preserve usable toolbar/sidebar behavior at the existing supported desktop breakpoints.

### 6. Verification And Relaunch

- Extend focused workbench tests for catalog selection, fixture/default behavior, grid generation, bounded creation, coordinate-preserving expansion, hierarchy anti-rot, and resize handling.
- Repair existing map-scoped authoring-media references. Migrate or remove stale `referenceTexturePath` values so the mock and fixture do not request missing `maps/<MapId>/draft/reference/**` files; source media must resolve through its declared ownership boundary instead of producing a per-Chunk 404 storm or silently degrading to colored placeholders.
- Run workbench typecheck, production build, full Vitest suite, relevant static scans, and `git diff --check`.
- Relaunch the development workbench and inspect the default map, toolbar hierarchy, city-region province controls, new-map dialog, map settings, and resize behavior in a real non-fixed browser viewport.

## Non-Goals

- No final world-map content or artwork.
- No arbitrary per-Chunk size/origin editing.
- No grid shrinking, left/top expansion with world-coordinate migration, or deletion of populated Chunk rows/columns.
- No hover/click/detailed-map entry work for the Godot StrategicMap scene.
- No change to published package schema v2, region encoding, scenario/save identity, or Godot runtime selection.
- No Godot editor or main-scene launch.
- No modification of `C:\Users\qs\asset`.

## Constraints And Risks

- Work on dirty `main`; preserve the completed Stage 0-2.5 baseline and all unrelated changes.
- Do not create or switch branches, use destructive Git commands, or run multiple write-capable agents.
- MapId remains immutable. Catalog kind/default metadata is editor routing only and never becomes runtime geography authority.
- Expansion must be atomic across project source and newly initialized editable Chunk state; failure must leave the previous map readable.
- Do not hand-edit existing mock/fixture authored geography merely to make UI demonstrations look fuller.
- The currently running workbench may be stopped during execution and must be relaunched for user acceptance afterward.

## Acceptance Criteria

1. Opening the bare workbench URL restores a valid last-opened map or opens `mock_qinghe_chiyan`; it never selects the fixture merely because its MapId sorts first.
2. The map selector visibly distinguishes authoring maps and fixtures, and the fixture remains explicitly openable.
3. New-map creation uses one structured form with MapId, display name, columns, rows, fixed 1024 Chunk size, and total-size preview.
4. Creating a 2x1 and another non-default grid produces the exact deterministic Chunk count, IDs, coordinates, origins, and isolated map directories without overwriting another MapId.
5. Invalid, zero, excessive, or conflicting creation input fails before partial catalog/source/filesystem mutation.
6. Map settings can expand columns rightward and rows downward while preserving every prior Chunk identity/origin and all existing authored geographic coordinates/data.
7. Shrink/relocation requests fail clearly and do not partially change the map.
8. Province create/edit/delete is absent from the global toolbar and available contextually inside `城市区域`, with province selection and ownership fields understandable there.
9. The map canvas updates to the real container size after browser/window resize; no fixed application viewport remains.
10. Existing draft save/reopen, validation, publication, mock/fixture isolation, and schema-v2 package behavior remain passing.
11. Opening both existing maps produces no missing map-scoped reference-media requests; the mock displays its intended reference/visual source and the fixture declares only media it actually owns.
12. Workbench typecheck/build, the complete Vitest suite, static hierarchy/route scans, browser inspection, and `git diff --check` pass.
13. The task records exact evidence and hands off at `Awaiting Verification`; a different Agent independently verifies before completion and archive.

## Current Progress Snapshot

Completed:

- User identified the fixed-window launch behavior, confused toolbar hierarchy, fixture-first default, two-Chunk fixture presentation, and missing visible grid-sizing workflow.
- Read-only diagnosis confirmed CSS uses full-size responsive ownership, the automation browser had a fixed viewport, province CRUD is placed beside map-level actions, the catalog defaults to its lexicographically first entry, new maps are hardcoded to 4x2, and no grid expansion UI exists.
- Stopping the first acceptance server exposed repeated missing requests for `maps/<MapId>/draft/reference/sample-world-stretched/<Chunk>.png`; existing source-media migration is incomplete and must be repaired in this task rather than accepted as placeholder-only rendering.
- Direction confirmed and execution authorized.
- Focused architecture and workbench guidance now define editor-only default/fixture routing, deterministic map-level Chunk grids, and right/down-only expansion before implementation.
- Catalog/default restore, structured new-map creation, bounded deterministic grid generation, rollback-protected expansion, contextual province management, responsive OpenLayers sizing, and explicit authoring-media ownership are implemented.
- Focused tests, production build, static scans, real Edge inspection, and development-service relaunch all pass from the restored final state.

Remaining:

- None.

## Pause Or Blocker

None. If safe expansion cannot preserve existing identifiers/data atomically, or implementing the structured grid requires a conflicting authority change, set `Status: Needs Discussion` and stop mutations.

## Resume Condition

Resume only from this task on dirty `main`, preserving the completed Stage 2.5 multi-map/package baseline.

## Resume Entry

1. Read repository `AGENTS.md`, `gameplay-alignment/authority-map.md`, `work-items/README.md`, this task, and `system-design/strategic-world-map-authoring-architecture.md`.
2. Independently verify the acceptance criteria without relying only on the executor's evidence; the development workbench is relaunched at `http://127.0.0.1:4173/?mapId=mock_qinghe_chiyan`.
3. If verification passes, set `Completed` and archive under `work-items/archived/2026/`; scoped defects return to `In Progress`, while direction or authority conflicts return to `Needs Discussion`.

## Latest Verification

- `npm run typecheck`: passed.
- `npm test`: passed all 18 files and 56 tests, including default/fixture selection, 2x1 and 3x2 creation, invalid/conflicting input, coordinate-preserving expansion, shrink rejection, hierarchy anti-rot, responsive ownership, source-media resolution, publication, and prior workbench regressions.
- `npm run build`: passed TypeScript checks, Vite production build of 489 modules, and server compilation.
- Static scans found no lexicographic `catalog.maps[0]` selection, prompt-chained new-map route, global-toolbar province CRUD ids, stale implicit reference/visual paths, staging debris, or residual fixture expansion masks. `git diff --check` passed; branch remains dirty `main` as required.
- Real Edge inspection from a clean local-storage session opened bare `http://127.0.0.1:4173/` on `mock_qinghe_chiyan`, showed separate “创作地图” and “验证 Fixture” selector groups, kept province CRUD out of the toolbar, exposed province identity/LayoutId/main/auxiliary facts inside “城市区域”, and showed both structured grid dialogs.
- Edge resize changed the viewport from 1416x774 to 1696x914; the map container changed from 1112x670 to 1392x810 and the OpenLayers canvas matched 1392x810. Explicit fixture opening restored the accepted 2x1/2-Chunk source and both maps reported zero failed workbench asset requests and zero missing `draft/reference` requests.
- The development service health endpoint reports the repository root and remains relaunched for user acceptance. No Godot editor, main scene, or `.NET` build was started.
- 2026-07-13 independent verifier reran `npm run typecheck`, all 18 Vitest files / 56 tests, `npm run build`, and scoped `git diff --check`; all passed.
- Independent headless Edge inspection from a fresh profile opened the bare URL on `mock_qinghe_chiyan`, showed separate authoring/fixture groups, kept province CRUD out of the toolbar and inside `城市区域`, and exposed both structured grid dialogs. Resizing from 1400x900 to 1800x1100 changed the map/OpenLayers canvas from 1096x796 to 1496x996 with matching CSS and pixel dimensions.
- Independent fixture inspection confirmed the accepted 2x1 world with exactly `chunk_0_0` and `chunk_1_0`, no page exceptions, no HTTP error responses, and no failed resource loads. All acceptance criteria are accepted.

## Execution Record

- 2026-07-13: User confirmed execution of the workbench hierarchy, default-selection, Chunk-grid creation/expansion, and responsive-canvas repair. No installed GodotPrompter skill applies because this task is a TypeScript/Web authoring tool change; browser inspection will use the available browser automation fallback because the listed browser skill file is unavailable.
- 2026-07-13: Executor completed repository bootstrap, confirmed dirty `main` with the Stage 0-2.5 baseline, found no authority conflict, and classified the change as a medium StrategicMap authoring-workbench update. Updated the focused architecture and workbench README before implementation; no GodotPrompter skill applies.
- 2026-07-13: Implemented the confirmed catalog, grid, UI hierarchy, resize, media-route, and regression scope without changing package schema v2 or Godot runtime selection. Creation validates before mutation and commits through staged map-owned directories; expansion preserves prior sources/media and rolls back newly initialized terrain/project changes on failure.
- 2026-07-13: During visible Edge QA, the development service received one unexpected fixture 6x6 expansion request outside the scripted inspection sequence. Executor restored the fixture's exact accepted 2x1 source, catalog revision, and media ownership, removed only the 34 generated blank masks, confirmed no staging debris, then reran the full typecheck/test/build/static/browser suite from the restored state.
- 2026-07-13: Scoped execution complete. Development workbench relaunched; handoff set to `Awaiting Verification` for a different Agent.
- 2026-07-13: Root independently verified the completed scope, accepted all criteria, and marked the task `Completed` for archive.

## Final Result

Implemented and independently verified the confirmed workbench repair. The catalog/grid/UI/resize/media contract is accepted, and the development workbench remains available for user acceptance.
