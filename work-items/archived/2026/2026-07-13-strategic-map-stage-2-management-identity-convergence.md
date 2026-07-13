# StrategicMap Stage 2 Strategic Management Identity And State Convergence

Status: Completed
Executor: `executor-xhigh` (current context)
Verifier: Main Agent independent verification context
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Converge the retained Strategic Management system onto the accepted canonical province/member-city identities and expose its mutable campaign facts to the replacement `StrategicMap` through a read-only presentation boundary. After this stage, the isolated production map can present the initial Qinghe/Chiyan faction-control state without receiving mutable facts from the legacy world, while old Strategic Management saves and the still-retained legacy main scene cross one explicit migration boundary.

## Confirmed Discussion Result

- The user authorized terminal-migration Stages 1 and 2 in order and expects the first large-world acceptance after both. Stage 1 is complete and independently verified.
- Canonical `StrategicMap` geography remains the only owner of static `ProvinceId`, `LocationId`, province membership, city role, geometry, and province-owned `LayoutId`.
- Strategic Management remains the only owner of mutable campaign facts: ownership/control, availability, resources, city state, corps, heroes, expeditions, and battle consequences.
- The existing first-slice Strategic Management city identities converge as follows:
  - `location_plains_city` -> `qinghe_core`
  - `location_bonefield_outpost` -> `chiyan_high_basin`
- The still-retained legacy world may translate its fixed site ids only at one explicit temporary boundary outside `StrategicMap`:
  - `player_camp` -> `qinghe_core`
  - `bonefield` -> `chiyan_high_basin`
- `location_timber_site` is a retained non-city Strategic Management content identity in this stage. It is not fabricated into canonical city geography and is not exposed as a replacement-map city region until later canonical content explicitly adds it.
- All eleven canonical Qinghe/Chiyan member cities receive Strategic Management location-control facts for large-world presentation. Auxiliary-city balance, building content, battle metadata, and management surfaces remain unassigned; Stage 2 must not clone main-city content or invent those deferred facts.
- The replacement map consumes only immutable/read-only view data. It cannot mutate Strategic Management, infer control from static Chunk pixels or canonical campaign role after initialization, or fall back to legacy state.
- Stage 2 does not change the main scene and does not implement city hover/click, detailed-map entry, navigation, army movement, battle launch, settlement, or broad legacy deletion.

## Authority Impact

No new gameplay or architecture decision is required. Current accepted authority already fixes canonical static-geography ownership, Strategic Management mutable-state ownership, the read-only integration boundary, province-owned layouts, migration-adapter restrictions, and Stage 2 scope. Update only the terminal-migration workstream progress and this task record unless implementation exposes a genuine authority conflict.

## Architecture Judgment

- Subsystems: Strategic Management definitions/state/rules/commands/view models, Save/Load migration, Map presentation integration, temporary legacy identity adaptation.
- Reuse: canonical `StrategicMap` geography loader/validator; retained Strategic Management definitions, state, command, invariant, save, and view-model services; Stage 1 region lookup/overlay; existing JSON version-migration pipeline.
- Static authority: `config/world/geography.json` through the accepted `StrategicMap` loader. Strategic Management may validate and reference canonical identities but must not duplicate editable province membership, city role, geometry, or `LayoutId` facts.
- Mutable authority: one `StrategicManagementState`; do not introduce `StrategicWorldState`, a replacement campaign state, state mirroring, fallback reads, reverse synchronization, or dual writes.
- Integration: define a focused read-only campaign-presentation port and immutable province/location view contract. The implementation adapter/composition belongs outside the pure `StrategicMap` core and owns no mutable facts.
- Legacy continuity: the old main scene remains live until Stage 6, so a temporary legacy-site adapter may translate only the two accepted fixed ids into canonical location ids. It must be visibly migration-only and deletable without changing Strategic Management facts.
- Persistence: increment the Strategic Management save version and migrate older supported versions incrementally. Version-3 identity migration must update dictionary keys and every persisted location/city reference atomically; ambiguous collisions or partial/mismatched identity graphs fail explicitly without publishing a candidate state.

## Execution Scope

1. Extend Strategic Management's definition/convergence boundary so canonical province/member-city identity, membership, and province-owned layout lineage are validated from `StrategicMap` geography rather than separately authored copies.
2. Replace first-slice temporary city ids throughout retained Strategic Management definitions, initialization, rules, commands, view models, battle-intent inputs, tests, and current-state output with `qinghe_core` and `chiyan_high_basin`.
3. Initialize mutable location-control records for all eleven canonical member cities from the accepted first-slice campaign roles while retaining existing main-city gameplay content only on the two mapped main cities. Do not assign unconfirmed auxiliary-city management/battle/balance content.
4. Add or update invariants so every replacement-map city view resolves one canonical `ProvinceId`, member `LocationId`, and authoritative province `LayoutId`; missing, extra, cross-province, or ambiguous identities fail with stable ids.
5. Upgrade Strategic Management JSON persistence with an incremental, atomic legacy-identity migration. Cover location/city dictionary keys, record ids, corps home city, expedition source/target/rollback stations, battle feedback targets, and any other persisted location reference found during implementation. Reject old/new collisions, mismatched keys/values, incomplete graphs, and legacy ids in the current save version.
6. Replace the old embedded `MapSiteId` mapping responsibility with one explicit temporary legacy-site identity adapter outside `StrategicMap`. Keep the legacy main scene operational through this adapter without adding new behavior or facts to the old world.
7. Add a read-only Strategic Management campaign-presentation port for canonical province/city control and integrate it into the isolated production `StrategicMap` composition. Region/province faction treatment must come from this port, not canonical campaign-role colors or legacy world state. Missing state fails visibly; no fallback is allowed.
8. Add focused regression coverage for canonical convergence, all-eleven-city initialization, non-invention of auxiliary management content, read-only presentation views, legacy adapter isolation, save migration success/failure/atomicity, current-version legacy-id rejection, retained old-main compatibility, and forbidden dual authority.
9. Run Strategic Management, StrategicMap Stage 0/1, Preview, affected Bridge/world compatibility tests, workbench tests/build when canonical consumers are touched, low-concurrency project build, forbidden scans, and `git diff --check`; update this task and the migration workstream.

## Non-Goals

- No hover, click, selection, city action UI, detailed-map entry/return, semantic marker resolution, navigation, movement, world-map time presentation, army rendering, battle launch, settlement integration, main-scene cutover, or legacy deletion.
- No new province/city gameplay rules, auxiliary-city display names, construction content, battle content, resources, balance identities, or map art.
- No redesign of retained city construction, economy, corps, hero, expedition, battle-result, or save-recovery behavior beyond required identity convergence.
- No second campaign state, map-owned ownership/control facts, state snapshots used as write authority, compatibility aliases inside canonical `StrategicMap`, fallback to legacy state, or dual write.
- No change to the independent first-city detailed-map internal-authoring task.

## Constraints And Risks

- This is a correctness-critical identity and persistence migration; execute with `executor-xhigh` (`gpt-5.6-sol`, `xhigh`).
- Work on dirty `main`; preserve unrelated changes and do not create or switch branches.
- Do not start or terminate the user's Godot editor. Prefer focused regressions, static contract checks, and low-concurrency builds.
- Existing supported saves must migrate incrementally through the current version. Migration must transform a complete candidate before publication and never partially rewrite live state.
- Canonical geography load or identity convergence failures must name the offending `ProvinceId`, `LocationId`, or `LayoutId`; do not synthesize missing canonical facts.
- The legacy main scene must continue to resolve its two fixed sites while it remains the production entry point, but the adapter may not leak `player_camp` or `bonefield` into replacement-map view data or current saves.
- Any need to choose auxiliary-city content, assign a noncanonical site to a canonical city, change province control rules, or alter the accepted identity lineage is a direction conflict: set this task to `Needs Discussion` and stop.

## Acceptance Criteria

1. Strategic Management production initialization validates canonical Qinghe/Chiyan geography and uses `qinghe_core`/`chiyan_high_basin` as its first-slice main-city ids; no live Strategic Management definition/state/view/command path uses `location_plains_city` or `location_bonefield_outpost` except versioned migration fixtures/code.
2. All eleven canonical member cities have exactly one mutable Strategic Management location-control record whose `ProvinceId`, `LocationId`, and authoritative `LayoutId` lineage matches canonical geography. Auxiliary cities do not silently receive cloned main-city construction, battle, resource, or balance content.
3. The replacement `StrategicMap` receives immutable/read-only control views from Strategic Management and displays initial Qinghe player control and Chiyan enemy control through production region/province presentation. It does not infer mutable control from Chunk pixels, canonical campaign role during refresh, or legacy world state.
4. Strategic Management remains the sole mutable authority. Repository scans and tests find no replacement campaign state, map-owned mutable control, fallback read, reverse synchronization, or dual write.
5. Version-3 and earlier supported saves migrate incrementally to the new current version with every location reference preserved under canonical ids. Ambiguous old/new collisions, key/value disagreement, incomplete identity graphs, and current-version legacy ids fail explicitly without publishing partial state.
6. The retained legacy main scene continues to translate only `player_camp` and `bonefield` through one named temporary adapter outside `StrategicMap`; replacement-map source, scene, resource, and view data contain neither legacy id.
7. Existing Strategic Management behaviors and accepted battle/expedition/settlement invariants remain regression-green under canonical ids. Stage 0/1 and frozen Preview regressions remain green; `project.godot` still targets the legacy main scene.
8. Focused regressions, relevant compatibility suites, low-concurrency `rpg.csproj` build, and `git diff --check` pass; Godot is not started. Execution hands off at `Awaiting Verification` with exact evidence and user-facing initial-acceptance boundaries.

## Current Progress Snapshot

### Completed

- User authorized Stages 1 and 2 and confirmed the first large-world acceptance expectation.
- Stage 1 production Chunk presentation is complete, independently verified, and archived.
- Main Agent reviewed current Strategic Management authority, definition/state/save/runtime/view-model boundaries, canonical geography, legacy site resolver usage, and the terminal-migration Stage 2 contract.
- Architecture judgment identified identity replacement, atomic save migration, all-city control initialization, read-only map-state integration, and a temporary legacy adapter as the complete Stage 2 boundary.
- `save-load` was loaded for incremental migration and persistent stable-id discipline. Previously loaded applicable skills are listed below.
- Execution began on dirty `main`; the complete authority/workstream route, Stage 1 handoff, canonical task contract, and all routed GodotPrompter skills were read before mutation. No authority or migration-direction conflict was found.
- Strategic Management now uses `qinghe_core` and `chiyan_high_basin` as its retained main-city identities, while `location_timber_site` remains a noncanonical resource-site content identity.
- Canonical StrategicMap geography is projected through a read-only convergence reference. All eleven Qinghe/Chiyan member cities receive exactly one mutable control record; only the two mapped main cities retain existing management, battle, construction, production, and balance content.
- Missing, extra, duplicate, cross-province, empty-layout, and key/value-mismatched identities fail with stable offending identity evidence. The replacement map receives immutable province/location control views and derives production faction treatment only from the Strategic Management presentation port.
- Save version 4 incrementally migrates supported version-3-and-earlier documents. The migration renames location/city keys and values plus corps homes, expedition source/target/rollback stations, and battle-feedback targets before publishing a complete candidate; mixed, partial, colliding, mismatched, or current-version legacy graphs fail explicitly.
- The embedded Strategic Management `MapSiteId` owner and resolver were removed. The retained legacy main translates only `player_camp` and `bonefield` through `TemporaryLegacyStrategicSiteIdentityAdapter` outside `StrategicMap`.
- Execution-side `godot-code-review` found no critical or required improvement. Node references are cached in `_Ready`, Chunk resource reads remain bounded/threaded, scene/resource ownership remains authored, the campaign port is immutable, and no Stage 3 input or scene-transition behavior entered the replacement map.
- Required verification passed without starting Godot: Strategic Management 119/119; Stage 0 foundation 4/4; Stage 1 presentation 8/8; frozen Preview 6/6; legacy WorldSite compatibility 270/270; WorldArmy movement 8/8; Target Battle architecture suite; Workbench 13 files/39 tests plus production build; and low-concurrency `rpg.csproj` build with 0 warnings/0 errors.
- Forbidden scans confirm retired Strategic Management city ids remain only in versioned migration code, the exact legacy-to-canonical translation has one owner, canonical/replacement map source contains no legacy world or fixed-site dependency, no Stage 3 behavior entered the new path, and `project.godot` still starts `StrategicWorldRoot.tscn`. `git diff --check` passed, with only the pre-existing CRLF normalization warning for one compatibility-test file; idle MSBuild and compiler servers were shut down successfully.

### Remaining

- None. Stage 2 is independently verified and ready for archive.
- The user may perform the first visual acceptance by opening the isolated `scenes/world/strategic_map/StrategicMap.tscn`; the production main scene remains unchanged.

## Pause Or Blocker

None.

## Resume Condition

No resume is required. Stage 2 is complete and independently verified.

## Resume Entry

Use this archived record and the terminal-migration workstream as the verified Stage 2 baseline for later interaction, navigation, battle, and cutover stages.

## Verification Handoff

Independent verification inspected the complete old-to-new identity graph, save-version migration and collision failures, canonical convergence, all-eleven city control facts, absence of invented auxiliary content, read-only map view boundary, region-control presentation source, legacy adapter isolation, retained old-main compatibility, and forbidden dual authority. The verifier reran the following without starting Godot:

- `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` — 119/119 passed.
- `dotnet run --project tests/StrategicMapFoundationRegression/StrategicMapFoundationRegression.csproj -maxcpucount:2 -v:minimal` — 4/4 passed.
- `dotnet run --project tests/StrategicMapPresentationRegression/StrategicMapPresentationRegression.csproj -maxcpucount:2 -v:minimal` — 8/8 passed.
- `dotnet run --project tests/StrategicRegionPreviewRegression/StrategicRegionPreviewRegression.csproj -maxcpucount:2 -v:minimal` — 6/6 passed.
- `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` — 270/270 passed; existing nullable warnings remain in unrelated test sources.
- `dotnet run --project tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -maxcpucount:2 -v:minimal` — 8/8 passed.
- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` — passed; existing nullable warnings remain in unrelated test sources.
- In `tools/world-map-workbench`: `npm test` — 13 files/39 tests passed; `npm run build` passed typecheck, Vite production build, and server TypeScript compilation.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` — 0 warnings, 0 errors.
- Retired-id, adapter-owner, replacement-map forbidden-dependency, Stage 3 scope, main-scene, and dual-authority scans passed. `git diff --check` passed apart from the already-present CRLF normalization warning on `WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.

Isolated production-scene acceptance boundary:

- Available: authored `StrategicMap` scene boot in isolation; bounded production Chunk loading/query and camera inspection; eleven canonical Qinghe/Chiyan city regions; initial five Qinghe player-control and six Chiyan enemy-control treatments read from immutable Strategic Management views; visible failure status when canonical or campaign data is missing or mismatched.
- Retained compatibility: normal project startup remains the legacy `StrategicWorldRoot.tscn`; its two fixed sites continue through the temporary adapter.
- Not available in Stage 2: region hover/click/selection, city actions, detailed-map entry/return, strategic navigation or army movement on the replacement map, world-map time presentation, battle launch/settlement from the replacement map, main-scene cutover, or legacy deletion.

## GodotPrompter Skills

- `using-godot-prompter`: subsystem skill routing and project-authority boundary.
- `csharp-godot`: Godot C# API and source conventions.
- `save-load`: incremental JSON save migration and stable persistent ids.
- `godot-testing`: focused migration, invariant, and presentation-port regression strategy.
- `godot-code-review`: execution-side and independent architecture/safety review.

## Execution Record

- 2026-07-13: Main Agent created the Stage 2 task after independently completing and archiving Stage 1. No Stage 2 code, resource, scene, config, or save change had begun at task creation.
- 2026-07-13: `executor-xhigh` set the confirmed task to `In Progress`, preserved the existing dirty `main` worktree, and began the required identity/reference inventory without starting Godot or entering Stage 3.
- 2026-07-13: `executor-xhigh` completed canonical identity/state convergence, the read-only replacement-map campaign presentation boundary, atomic version-4 save migration, retained-main adapter migration, focused regression coverage, complete execution-side review, and the required verification matrix. Godot was not started or terminated; Stage 3 was not entered.
- 2026-07-13: `executor-xhigh` handed the task to an independent verifier at `Awaiting Verification`. It did not set `Completed`, archive the task, change the main scene, or modify unrelated dirty work.
- 2026-07-13: Main Agent independently reviewed the identity/state/save/presentation/adapter implementation, reran the complete recorded matrix, and repeated the retired-id, adapter-owner, replacement-path, main-scene, branch, and Godot-process checks. All acceptance criteria passed; Stage 2 was set to `Completed` and approved for archive.

## Final Result

Stage 2 is complete and independently verified. Strategic Management now uses canonical province/member-city lineage, owns all eleven mutable city-control facts without invented auxiliary management content, migrates supported saves atomically to version 4, supplies immutable replacement-map control views, and keeps the retained legacy main behind one temporary adapter. The isolated production scene is ready for the user's first large-world acceptance; Stage 3 and later behavior remain unimplemented.
