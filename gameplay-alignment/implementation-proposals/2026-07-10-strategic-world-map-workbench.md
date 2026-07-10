# Strategic World Map Workbench Implementation Proposal

Status: UX Remediation Complete - Pending User Content QA

## Origin

- Requirement: STRATEGIC-WORLD-MAP-001
- Design Proposal: `design-proposals/archived/2026-07-10-strategic-world-chunk-authoring/`
- Authority:
  - `system-design/strategic-world-map-authoring-architecture.md`
  - `system-design/resource-authoring-taxonomy.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/README.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Blocking Issues: None known.

## Requirement

Implement the accepted local strategic-world geographic-data editor and validator as one isolated tool under `tools/world-map-workbench/`. The tool must share canonical game coordinates, chunk identities, and stable object ids with Godot while keeping geographic editing outside the engine and leaving navigation authority in Godot.

## User QA Finding (2026-07-11)

The first content-facing review found that terrain painting was understandable, but the surrounding authoring surface exposed unrelated tools and settings at the same time. The implementation was therefore remediated without changing geographic data ownership, supported feature scope, or saved formats.

The accepted remediation is task-oriented progressive disclosure:

- organize authoring into terrain, networks, strategic locations, territories/regions, and review/output workspaces;
- show only the tools and settings needed by the active workspace;
- keep object selection and modification available as a consistent cross-workspace action;
- move the complete ten-layer stack behind an explicit layer view instead of permanently competing with authoring tasks;
- collapse validation to a status strip by default and expand its issue list only when requested;
- provide concise, persistent instructions for the current tool, including how to start and finish map gestures;
- preserve all existing canonical data, editing operations, shortcuts, and Godot ownership boundaries.

## Scope

- Create an isolated TypeScript workbench with:
  - OpenLayers map interaction;
  - Turf.js geometry operations and validation;
  - a local Node service restricted to configured project paths;
  - Sharp-backed terrain and territory mask output.
- Implement the accepted ten-layer stack with visibility, lock, opacity, and ordering controls.
- Implement continuous terrain editing across chunk boundaries with brush, erase, fill, lasso, polygon fill, terrain-id inspection, and chunk-aligned terrain masks.
- Implement global editable rivers, roads, and mountain ranges with control points, type-specific properties, river snapping, automatic tributary receiver relationships, chunk-clipping preview, and boundary diagnostics.
- Implement strategic-location placement and property editing for cities, gates/passes, bridges, ferries, ports, ruins, and resource sites, including stable-id and illegal-placement validation.
- Implement city-territory and smaller-region polygons, region hover, city-union preview, topology validation, `territory_mask`, region lookup, and compiled outlines.
- Implement local load/save, bounded undo/redo, property binding, validation navigation, and explicit save feedback.
- Present the accepted features through task workspaces with contextual tools, settings, guidance, and progressive disclosure.
- Provide a bootstrap project document so the tool can start before campaign geography and real chunk art are authored.

## Non-Goals

- Do not modify Godot runtime code, scenes, navigation resources, or gameplay state.
- Do not implement CraftPix browsing, final chunk-art generation, regeneration, batch generation, or art comparison.
- Do not add version queues, stale-chunk tracking, production management, or advanced automatic repair.
- Do not become a full GIS replacement or add advanced projection-cleanup workflows.
- Do not infer navigation walkability or movement cost from terrain ids.
- Do not write into the external read-only asset library.

## Touched Systems

- `tools/world-map-workbench/`
- `gameplay-alignment/implementation-proposals/2026-07-10-strategic-world-map-workbench.md`
- `gameplay-alignment/implementation-proposals/README.md`

The tool may create canonical project data under `config/world/` and derived masks under `assets/textures/world/` only when the user saves a workbench project. The implementation itself remains isolated under the tool directory.

## GodotPrompter Skills

No installed GodotPrompter skill applies. This slice creates a standalone TypeScript/Node authoring tool and does not edit Godot code, scenes, resources, navigation, or UI.

## Implementation Tasks

### Task 1: Isolated service, schema, and project bootstrap

- Create the package, TypeScript, Vite, Vitest, and server configuration under `tools/world-map-workbench/`.
- Define canonical project, chunk, terrain, vector-feature, strategic-location, and region schemas.
- Restrict service paths to the repository `config/world/` and `assets/textures/world/` roots.
- Add bootstrap/load/save APIs and atomic JSON writes.
- Add terrain-mask and region-artifact compilation APIs.
- Add path-boundary, schema, atomic-write, and mask-generation tests.

### Task 2: Map shell and layer management

- Create the global top bar, task-workspace navigation, central OpenLayers canvas, contextual right-side layer/property drawers, and collapsible bottom validation area.
- Render the accepted ten layers.
- Add visibility, lock, opacity, and reordering controls without changing data authority.
- Keep pointer coordinates in the canonical game coordinate system.

### Task 3: Terrain editing

- Implement sparse chunk-aligned terrain storage.
- Add brush, erase, fill, lasso, and polygon-fill operations across chunk edges.
- Add terrain-id inspection and terrain palette selection.
- Add unclassified, unknown-id, and isolated-terrain validation.
- Save dirty terrain chunks through the Sharp mask endpoint.

### Task 4: Global lines and strategic locations

- Add draw, select, modify, and property editing for rivers, roads, and mountains.
- Add river endpoint snapping and automatic tributary receiver relationships.
- Render chunk grid/clipped context and validate unexplained boundary endpoints.
- Add strategic-location draw, move, property editing, stable-id validation, reference deviation, and water/mountain conflict diagnostics.

### Task 5: Territories, regions, and derived artifacts

- Add polygon draw/modify for city territories and smaller regions.
- Add pointer hover for actual `RegionId` and authoring simulation for default-hidden/city-hover/region-highlight presentation.
- Add overlap, holes, discontinuity, orphan-region, duplicate-id, and cross-city validation.
- Compile `territory_mask`, region lookup data, city-union outlines, and region outlines.

### Task 6: Verification and evidence

- Run unit and integration tests.
- Run the production build and TypeScript checks.
- Start the local service and verify health, bootstrap, save, and compilation endpoints.
- Exercise the workbench in a browser at desktop resolution.
- Record exact evidence and remaining manual-QA limitations here.

### Task 7: User-QA interaction remediation

- Replace the all-tools toolbar with five task workspaces and a contextual tool shelf.
- Separate workflow navigation from the ten-layer stack through explicit authoring/layer views.
- Render terrain, network, location, and region settings only when their matching tool is active.
- Add current-tool guidance and task-specific empty states to the inspector.
- Make the validation list collapsible while keeping its summary and rerun action visible.
- Preserve map editing behavior and add tests for the workspace/tool information architecture.

## Tests

- Schema validation rejects duplicate chunk ids/coordinates, invalid dimensions, and unknown layer ids.
- Path guards reject traversal and writes outside configured project roots.
- Terrain operations update both chunks when a stroke crosses a boundary.
- Fill/lasso/polygon operations preserve configured terrain ids.
- Terrain diagnostics detect unclassified, unknown, and isolated cells.
- River snapping records the receiver id and snaps the confluence coordinate.
- Global line validation identifies unexplained chunk-boundary endpoints.
- Strategic-location validation detects duplicate ids and water/mountain conflicts.
- Region validation detects overlaps, holes, disconnected geometry, orphan ownership, duplicate ids, and cross-city conflicts.
- Region compilation emits a mask, lookup table, and outlines with stable ids.
- `npm test` passes.
- `npm run build` passes.
- Every drawing tool belongs to exactly one task workspace, and every workspace has a valid default tool and actionable guidance.

## Diagnostics

- The service logs startup, bound project root, successful saves/compiles, and rejected path operations.
- The UI reports validation items with object id, world position, severity, and a locate action.
- Save failures remain visible and never report success after a partial write.
- Do not log pointer movement, paint samples, frames, or licensed asset contents.

## Manual QA

- Open the workbench and confirm the map remains the primary canvas while the five task workspaces are immediately distinguishable.
- Toggle, lock, reorder, and change opacity for every layer.
- Paint terrain across a chunk boundary with every accepted terrain tool and reload the saved result.
- Draw and modify a river, road, and mountain range across chunk boundaries; confirm the river confluence relationship is visible.
- Add every strategic-location type, change its properties, and locate validation errors.
- Draw two cities with smaller regions and confirm region hover, topology errors, union outline, mask, lookup, and outline output.
- Confirm final chunk art and reference layers remain comparison-only.
- Confirm no request leaves the local service and no external asset-library file is modified.
- Start from each workspace and confirm that only its relevant tools and settings are visible.
- Open the layer view, edit visibility/lock/opacity/order, and return to the previous authoring workspace without losing the active task.
- Expand and collapse the validation drawer and confirm the map regains the released canvas space.
- Confirm a first-time user can infer how to finish lines and polygons from the visible current-tool guidance.

## Acceptance

- The design proposal is archived and accepted authority is current.
- All implementation files live under `tools/world-map-workbench/` apart from proposal evidence and user-authored project outputs.
- The five accepted feature areas are usable through one local UI.
- The five feature areas are presented as distinct workspaces; unrelated controls are not simultaneously exposed.
- Canonical coordinates and stable ids round-trip without a persisted Web/Godot offset or scale conversion.
- The local service enforces path boundaries and atomic writes.
- Terrain and territory masks plus lookup/outlines compile reproducibly.
- Excluded art-generation, production-queue, GIS, and navigation systems are absent.
- Automated tests and production build pass.

## Verification Evidence

- 2026-07-10: TDD RED was observed before implementation: all five initial suites failed because schema, terrain, geometry, path-policy, and artifact modules did not exist. A later artifact test exposed RGB channel expansion before the mask assertion was corrected to verify the authoritative grayscale id channel.
- 2026-07-10: `npm run typecheck` completed with exit code `0` after all strict TypeScript and OpenLayers layer-selection issues were resolved.
- 2026-07-10: `npm test` completed with exit code `0`: `7` files and `18` tests passed. Coverage includes schema invariants, path traversal rejection, cross-chunk terrain edits, fill/polygon operations, terrain diagnostics, river confluence, source/lake/coast anchors, stale relationship removal, explicit chunk clips, region topology, strategic-location conflicts, terrain PNG ids, region artifacts, and complete local API flows.
- 2026-07-10: `npm run build` completed with exit code `0`. Vite built `486` client modules; the production JavaScript bundle was approximately `452 kB` before gzip and `132 kB` after gzip. Server TypeScript compilation also passed.
- 2026-07-10: production `npm start` bound only to `127.0.0.1:4174`. `/api/health`, `/api/project`, `/`, and the hashed client JavaScript asset all returned successfully. The default bundle exposed all ten layers, eight bootstrap chunks, and the water-anchor collection without creating canonical project outputs.
- 2026-07-10: browser QA at a `1280x720` viewport confirmed the full toolbar/panel/map layout, ten visible layer controls, cross-chunk forest painting, terrain-id inspection, undo/redo, layer lock and reorder, river drawing and properties, strategic-location placement and properties, region Hover with real `RegionId`, and region property selection. Browser console inspection returned no page warnings or errors.
- 2026-07-10: automated geometry and build verification after browser QA added the final source/lake/coast anchor tool and explicit per-chunk clip preview. These additions are covered by strict typecheck, unit tests, production build, and production HTTP smoke checks.
- 2026-07-10: API integration tests used temporary project roots to verify bootstrap, canonical geography save, Sharp terrain-mask output, `territory_mask`, lookup, and outline compilation. No test geography or mask was written into the RPG project.
- 2026-07-10: `npm audit --registry=https://registry.npmjs.org --omit=dev --audit-level=high` reported `0` vulnerabilities. The configured local npm mirror was not used because it does not implement the audit endpoint.
- 2026-07-10: tool-source trailing-whitespace scan returned no findings. `git diff --check` reported no whitespace errors; it printed only two unrelated pre-existing CRLF normalization warnings outside this implementation scope.
- 2026-07-10: the local service was stopped after verification. No Godot process, build, scene, runtime code, or external asset-library file was modified.
- 2026-07-11: desktop browser QA at `1280x720` confirmed the five task workspaces, workspace-specific tools and settings, persistent current-tool guidance, contextual layer drawer, contextual object-property drawer, and collapsible validation area. With auxiliary drawers closed and validation collapsed, the map occupied approximately `65%` of the viewport.
- 2026-07-11: browser QA exercised the terrain, network, strategic-location, territory/region, and review contexts; layer controls remained available without replacing the active task, and validation could expand for issue work then collapse back to the status strip.
- 2026-07-11: territory and smaller-region drawing remained disabled until an explicit `CityId` was supplied. The UI directed users without a city to place one in the strategic-location workspace instead of silently creating an unassigned owner.
- 2026-07-11: the UX-remediated build passed `npm test` (`8` files, `21` tests), `npm run typecheck`, and `npm run build`. The added interaction tests cover workspace defaults, tool ownership, task guidance, and review-workspace behavior.

## Remaining User Content QA

- Initialize the tool against the real campaign world when the final Chunk manifest, reference layers, and final art paths are available.
- Exercise every strategic-location type and a representative multi-city region set with production geography.
- Confirm the resulting masks and canonical ids inside Godot navigation-authoring and runtime presentation workflows.
- Keep this proposal active until that content-level QA is accepted; the standalone tool implementation itself is complete.
