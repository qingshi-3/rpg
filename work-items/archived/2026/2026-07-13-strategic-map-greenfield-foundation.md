# Strategic Map Greenfield Foundation

Status: Completed
Executor: executor (`gpt-5.6-sol`, high)
Verifier: Main Agent (independent parent context)
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Establish the accepted province, main-city, auxiliary-city, visual-region, detailed-layout, and visual-chunk identity model as a new legacy-independent `StrategicMap` module, together with canonical geography loading and validation foundations for later standalone map implementation.

## Confirmed Discussion Result

- Build the replacement strategic-world map as a greenfield final module in new `StrategicMap` directories. New models and services must not depend on `StrategicWorldRuntime`, `StrategicWorldState`, `WorldArmyState`, fixed world-site markers, or the visible legacy TileMap world.
- The visible large world will use aligned raster visual chunks rather than a `TileMapLayer`. Hidden per-chunk TileMap or equivalent navigation authoring may remain a separate later navigation concern.
- One province contains exactly one main city and zero or more auxiliary cities and owns one authoritative detailed-map `LayoutId`.
- Every main or auxiliary city has exactly one corresponding large-world visual region. The region is presentation/query geometry, not an independent campaign-control or persistent-state owner; control color and interaction derive from the corresponding strategic location.
- Qinghe has five locations/visual regions: `qinghe_core` is the main city and the other four accepted regions are auxiliary cities. Chiyan has six: `chiyan_high_basin` is the main city and the other five accepted regions are auxiliary cities.
- Qinghe is the player starting province and Chiyan the first hostile province. The legacy `player_camp`/plains-city identity migrates to Qinghe's main city, and `bonefield`/hostile-outpost identity migrates to Chiyan's main city only through an explicit versioned or cutover boundary; legacy aliases must not enter the new module.
- Province owns layout selection. A main or auxiliary city maps to semantic locations and later entrance/deployment roles inside the province's one layout; individual cities cannot select different layouts.
- Build the new module independently, later connect it to retained Strategic Management and Strategic Battle Bridge ports, switch the main scene once acceptance is met, and delete the old strategic-world implementation and temporary migration adapters. There is no long-term dual-write or fallback path.
- The accepted standalone Preview remains frozen reference evidence. This task may make only the minimum schema-consumption maintenance needed to keep it compiling; it must not add Preview functionality or treat Preview code as production Runtime.

## Authority Impact

Large. Before implementation, update the relevant gameplay and system authority to:

- introduce province as the aggregate containing one main city and multiple auxiliary cities;
- make each main/auxiliary city correspond one-to-one with a large-world visual region;
- move authoritative `LayoutId` ownership from an individual strategic location to its province;
- remove independent mutable strategic-region control from the accepted model;
- define the greenfield `StrategicMap` module, chunk-rendering boundary, permanent Strategic Management/Battle Bridge ports, temporary migration boundary, cutover, and old-system deletion strategy;
- update resource/directory routing for the new module without changing the canonical `config/world/`, `assets/textures/world/`, `resource/world/`, and `scenes/world/` ownership roots.

At minimum review and update the current strategic-region/detail-map gameplay detail, long-term content-system summary, strategic-region/detail-map architecture, strategic-world-map authoring architecture, Strategic Management architecture, Strategic Battle Bridge architecture, site-map layout architecture, semantic-marker architecture, resource taxonomy, and their README routes where required. Delete or correct superseded target-location-owned region wording rather than layering exceptions over it.

## Execution Scope

1. Synchronize the confirmed durable gameplay and architecture authority before implementation.
2. Define a breaking canonical geography schema revision for provinces, main/auxiliary locations, one-to-one location visual geometry, and visual chunks. Do not retain `cityId`, independent gameplay `RegionId`, or `detailMapId` as new-runtime identities.
3. Migrate the accepted Qinghe and Chiyan canonical geography and reproducible region artifacts to that schema without changing their user-accepted shapes.
4. Update the Web workbench schema, terminology, validation, artifact compilation, and focused tests for the new identity contract.
5. Add final-named greenfield module directories under the existing layer roots, using `StrategicMap` rather than `V2` or `NewWorld` naming.
6. Add pure new-module definitions plus canonical geography/chunk loading and validation services. They must not reference legacy world models or services.
7. Add focused regression coverage proving the new module's dependency isolation, province/location/geometry invariants, chunk-coordinate contract, and canonical migrated content.
8. Apply only minimal frozen-Preview schema maintenance needed for compilation and its existing visual regression; do not add gameplay or production ownership.

## Non-Goals

- No production Chunk scene, camera, streaming/residency implementation, region interaction, army movement, navigation compilation, fog, Strategic Management runtime connection, battle entry, detailed-map marker content, main-scene cutover, or old-system deletion in this foundation task.
- No modification of legacy strategic-world behavior or state to imitate the new model.
- No compatibility reads, fallbacks, or dual writes inside the greenfield module.
- No final auxiliary-city display-name or balance design beyond stable identities and confirmed main/auxiliary roles.
- No detailed-map scene authoring or binding to the paused `plains_city_v0` scaffold.

## Constraints And Risks

- Work on `main`; preserve all unrelated dirty changes and do not create or switch branches.
- Canonical plain-text geography remains under `config/world/`; final visual media remains under `assets/textures/world/`; do not create a second canonical data tree inside the new module.
- Keep persistent campaign authority in Strategic Management. The foundation module owns static geography and validation only, not province/location mutable state.
- Region masks may retain categorical mask IDs as derived artifact implementation details, but a mask entry resolves to the corresponding `LocationId`; it must not create a second gameplay identity or owner.
- `LayoutId` declarations may be established before their future authored scenes exist, but this task must not claim detailed-map runtime readiness or invent fallback scenes.
- Any discovered need to reuse a legacy model/service, change the one-to-one city/visual-region rule, restore independent region state, or bind a different layout per city sets the task to `Needs Discussion` before further mutation.
- Use low-concurrency, minimal-output .NET builds only if static and focused checks are insufficient. Do not start or terminate the Godot editor.

## Acceptance Criteria

1. Current authority consistently describes province-owned layouts, one main plus multiple auxiliary cities, and one visual region per city with no independent region campaign state.
2. Canonical geography represents Qinghe as one main plus four auxiliaries and Chiyan as one main plus five auxiliaries, with every visual geometry resolving to exactly one location and province.
3. The new `StrategicMap` definitions, loaders, validators, and tests contain no dependency on legacy world state, runtime, army carrier, scene root, fixed marker, or visible TileMap implementation.
4. Visual chunk identity and world-coordinate validation come from the canonical manifest; static visual art, geographic geometry, navigation, presentation, and mutable strategic state remain separate authorities.
5. Workbench validation and derived artifacts use the new province/location contract and preserve the accepted Qinghe/Chiyan shapes and exact categorical lookup behavior.
6. The frozen Preview either continues to pass its existing focused visual/data regressions through minimal schema adaptation or is explicitly left unchanged because no shared schema consumer was affected; it gains no production responsibility.
7. Focused TypeScript and C# regressions, appropriate static checks, low-concurrency build if required, and `git diff --check` pass, with unrelated failures identified rather than hidden.
8. The activity task records exact changed scope, verification evidence, skills used, remaining work, and the next standalone Chunk-presentation task boundary, then stops at `Awaiting Verification`.

## Current Progress Snapshot

### Completed

- Discussion confirmed the province/main-city/auxiliary-city/visual-region hierarchy, Qinghe and Chiyan counts, player/hostile province roles, greenfield module strategy, Chunk-based visible world, temporary cutover adaptation, and eventual old-system deletion.
- The standalone irregular-region Preview was user-accepted, completed, and archived as frozen visual reference evidence.
- Durable gameplay and system authority now assigns `LayoutId` to the province, binds exactly one visual geometry to every main/auxiliary city `LocationId`, removes independent visual-region campaign state, and defines the greenfield `StrategicMap`/port/cutover boundary.
- Superseded target-location-owned `ApproachRegionId` and per-location layout-selection wording was replaced in the current mapping, Strategic Management, Battle Bridge, Site Map Layout, semantic-marker, world-authoring, resource-taxonomy, and route documents.
- Canonical geography now uses schema version 2 with two province definitions, province-owned layouts, eleven main/auxiliary city locations, and one `LocationId`-bound geometry per city. Qinghe remains one main plus four auxiliaries; Chiyan remains one main plus five auxiliaries.
- Accepted polygon coordinates and categorical ordering were preserved. The compiler-regenerated `territory_mask.png` is byte-identical to the accepted mask; version-2 lookup and outlines resolve mask ids and geometry through `LocationId`/`ProvinceId`.
- The Web workbench schema, compiler endpoint, repository/client flow, authoring terminology, location/geometry editing, validation, and focused tests now use the province/location contract. Canonical derived-artifact reproducibility is covered byte for byte.
- Final-named pure definitions were added under `src/Definitions/StrategicMap/`; canonical chunk/geography loaders and validator services were added under `src/Application/StrategicMap/`. They own static definitions and validation only.
- `tests/StrategicMapFoundationRegression/` covers canonical loading, the 35-chunk coordinate contract, province/main/auxiliary counts, layout ownership, an exact accepted-geometry fingerprint, validator failures, and source-layer dependency isolation.
- The frozen Preview received only schema-consumption maintenance: it maps province main-city anchors and `LocationId` geometries into its existing internal preview model. No scene, shader, interaction, or production responsibility was added.

### Remaining

- None within this task.

## Pause Or Blocker

None.

## Resume Condition

Not applicable; the task is completed and archived.

## Resume Entry

No resume entry. Chunk presentation, navigation, movement, integration, cutover, and old-system deletion require separate confirmed work items.

## Verification Handoff

Independent verification completed. Authority is coherent around province-owned layouts and location-bound visual geometry; the new module is isolated from forbidden legacy owners; canonical Qinghe/Chiyan counts and one-to-one geometry bindings are correct; derived artifacts reproduce byte for byte; Preview changes are schema-consumption-only; and non-goal boundaries remain intact.

## GodotPrompter Skills

- `csharp-godot`: required for new Godot C# module conventions and boundaries.
- No other installed skill is currently required. If execution introduces Godot Resource authoring or tests beyond the confirmed pure-definition/service scope, stop and update this task's skill list before proceeding.

## Execution Record

- 2026-07-13: Main Agent created this task after the user confirmed the greenfield new-directory implementation, full model/service rewrite without legacy coupling, later adapter-based cutover, and old strategic-world deletion.
- 2026-07-13: Executor began implementation on `main`, confirmed the existing dirty worktree, read the required authority and `csharp-godot` skill, and started with durable authority synchronization as required.
- 2026-07-13: Authority synchronization milestone completed before implementation; accepted authority now consistently uses province-owned layouts and location-owned visual geometry with no independent region state.
- 2026-07-13: Continuation executor audited the pre-existing partial diff, found no contradiction with the confirmed task, preserved it, and completed the canonical schema/content/artifact/workbench migration.
- 2026-07-13: Added the final-named pure `StrategicMap` definition, canonical loader, validator, and focused regression layers. Static scans found no references in those files to the forbidden legacy owners, fixed legacy site identities/markers, visible tile implementation, or temporary module naming.
- 2026-07-13: Applied only the minimum frozen-Preview schema maintenance needed for the shared canonical data revision. The existing Preview regression remains green with no new Preview feature or production ownership.
- 2026-07-13: Verification evidence: `npm test` passed 13 files / 39 tests; `npm run build` passed typecheck, Vite build, and server TypeScript compilation; `dotnet run --project tests/StrategicMapFoundationRegression/StrategicMapFoundationRegression.csproj -maxcpucount:2 -v:minimal` passed 4 cases; `dotnet run --project tests/StrategicRegionPreviewRegression/StrategicRegionPreviewRegression.csproj -maxcpucount:2 -v:minimal` passed 6 cases; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors. Godot was not started or terminated.
- 2026-07-13: Final static verification passed: the accepted geometry coordinate sequences match the pre-migration source exactly; forbidden dependency/naming scans are clean; no production presentation, navigation, movement, or Strategic Management integration file entered the diff; `git diff --check` passed. `dotnet build-server shutdown` then closed the MSBuild and compiler servers successfully.
- 2026-07-13: Two exploratory command invocations were invalid (`vitest --runInBand` is unsupported and the first inline `tsx` form used unsupported top-level await); both were corrected immediately. The corrected test suite and canonical compiler run passed, so these were command-shape errors rather than product failures.
- 2026-07-13: Independent parent-context verification used `godot-code-review`, reviewed the new definitions/loaders/validator and authority wording, confirmed the forbidden dependency scan and Preview scope, then reran `npm test` (13 files / 39 tests), `npm run build`, the StrategicMap foundation regression (4 cases), the frozen Preview regression (6 cases), and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` (0 warnings / 0 errors). All acceptance criteria passed.

## Final Result

Completed and independently verified. Changed scope is limited to accepted authority synchronization, canonical province/location geography and derived artifacts, workbench schema/compiler/client/tests, pure `StrategicMap` definitions/loaders/validation, focused regression coverage, and minimum frozen-Preview schema consumption.

No production scene, presentation runtime, navigation, movement, Strategic Management connection, battle integration, main-scene cutover, or old-system deletion was added. The next standalone work item may introduce the production Chunk-presentation boundary: authored `StrategicMap` Chunk scene/resource contracts, read-only chunk selection/loading and world-coordinate queries, and presentation-facing failure diagnostics. That task must remain separate from navigation, movement, campaign-state ownership, Strategic Management/Battle Bridge integration, and cutover unless a new confirmed discussion explicitly expands it.

Remaining risks: none within the accepted foundation scope. Public programmatic definitions could later receive broader defensive validation, but the authoritative canonical path already rejects malformed JSON fields and versions before validation; expanding that contract belongs to the consumer task that needs it.
