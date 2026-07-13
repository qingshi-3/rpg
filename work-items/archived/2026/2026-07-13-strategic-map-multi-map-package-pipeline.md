# StrategicMap Multi-Map Package And Publishing Pipeline

Status: Completed
Executor: executor
Verifier: root
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Insert terminal-migration Stage 2.5 and replace the current single-map, fixed-path authoring/runtime coupling with one versioned multi-map content pipeline. The Web workbench must support independent map projects and incomplete drafts, publish internally consistent read-only map packages, and let the production `StrategicMap` scene consume a selected package without map-specific code, paths, dimensions, or identities. Strategic Management and saves must identify the compatible map/scenario instead of assuming the current Qinghe/Chiyan geography.

## Confirmed Discussion Result

- The current raster art and Qinghe/Chiyan geography are mock data used to run the pipeline, not final world content and not a permanent runtime dependency.
- Logic and data must remain separate. A new published world map can replace the mock by changing package/scenario selection, without modifying `StrategicMap` C# or `.tscn` structure.
- The editor, publishing/compiler boundary, Godot package loader, Strategic Management scenario binding, and save identity must be designed and implemented together.
- Workbench editable sources remain the sole authoring authority. Published packages are generated, read-only snapshots; Godot resources select packages and presentation tuning but do not duplicate geographic facts.
- Workbench save must allow structurally readable incomplete drafts. Validation and publishing apply stricter, capability-specific completeness gates.
- `ProvinceId`, `LocationId`, province membership, world position, city geometry, and province-owned `LayoutId` are static map facts. Player-start/first-hostile roles, initial control, garrisons, resources, and other starting campaign facts belong to a scenario/Strategic Management package.
- Region artifacts must stop applying prototype-only artificial waviness. Published masks reflect authored geometry exactly; visual softness belongs to presentation.
- Replace the global 8-bit/255-region prototype mask with deterministic chunk-aligned region artifacts capable of more than 255 city regions. Region codes remain derived and always resolve to stable `LocationId`.
- The current map is migrated as an explicitly named mock package. A second materially different fixture/package proves replacement without code or scene changes.
- This task precedes Stage 3. Hover, selection, clicking, detailed-map entry/return, navigation, and movement remain outside this task.

## Authority Impact

Large architecture change. Before implementation code or resources change, update:

- `system-design/strategic-world-map-authoring-architecture.md`: source/workspace/published-package separation, multi-map identity, draft and publish validation levels, map package capabilities, deterministic region artifacts, runtime consumption, and scenario boundary.
- `system-design/strategic-management-system-architecture.md`: scenario-owned campaign roles and map/scenario compatibility identity.
- `system-design/resource-authoring-taxonomy.md`: map-scoped source, published config, generated artifacts, final visual media, and Godot selection-resource ownership.
- `gameplay-alignment/strategic-map-terminal-migration-workstream.md`: insert Stage 2.5 as the verified prerequisite between completed Stage 2 and future Stage 3.
- `system-design/README.md` or focused routing indexes only if names/routes change.

Player-facing gameplay authority does not change. Do not create a proposal or archival design duplicate.

## Architecture Judgment

- Subsystems: Map/Content Pipeline, Definitions, Strategic Management, Save, Presentation, editor tooling, and test infrastructure.
- Existing reusable foundations: stable `ProvinceId`/`LocationId`/`LayoutId`, one world-coordinate space, canonical GeoJSON-style geography, chunk residency, threaded texture loading, Strategic Management presentation port, and workbench validation/compiler infrastructure.
- Required replacement boundaries: fixed `config/world` project loading, fixed world texture root, global region mask, scene-authored current-map bounds/paths, hardcoded first-scenario geography selection, and saves without map/scenario identity.
- Long-term owner split: workbench sources own editable geography; publisher owns derived immutable artifacts; `StrategicMap` owns static map loading/query/presentation; Strategic Management owns mutable scenario state; Godot-authored navigation remains separate and is referenced by packages when available.

## Execution Scope

### 1. Contract And Routes

- Synchronize the authority and workstream documents named above.
- Define versioned map catalog/source/package/scenario identities and compatibility rules.
- Define capability/completeness profiles at least sufficient to distinguish visual preview, region-interactive mock, and later strategic-runtime readiness.

### 2. Workbench Multi-Map And Draft Workflow

- Replace fixed singleton file/output routing with MapId-scoped safe paths and a map catalog or equivalent explicit selection route.
- Add usable list/create/open/duplicate/switch behavior with immutable stable MapId and unsaved-change protection.
- Add missing province authoring needed for a new map, including one-main-city membership and province-owned `LayoutId` editing.
- Separate structurally parseable draft saving from strict validation and publish blocking.
- Keep editor-only layer/workspace state out of the runtime package contract.

### 3. Publisher And Artifact Contract

- Add an explicit publish operation that validates a selected profile, stages all output, verifies cross-artifact consistency, and exposes a new revision only after success.
- Publish map-scoped visual/geography/terrain/region/navigation references through one versioned package manifest.
- Generate deterministic chunk-aligned region masks with an exact categorical encoding beyond 255 regions and a lookup to stable identities.
- Rasterize exact authored geometry, remove compiler-authored edge distortion, and make province-union/topology failures block publish instead of falling back.
- Enforce or verify category-mask import semantics (lossless, nearest, no mipmaps) separately from zoomable visual-chunk semantics (mipmaps enabled).

### 4. Godot Package Consumption

- Add pure package definitions/loaders/validators and a read-only loaded map context/query boundary.
- Refactor production `StrategicMap` composition to obtain world bounds, chunk paths, region artifacts, and static geography from the selected package.
- Remove current-map dimensions, mask paths, config-root assumptions, and concrete geography identities from generic production scenes/resources/code.
- Preserve deterministic bounded chunk loading and existing failure diagnostics without adding fallback or dual authority.

### 5. Scenario, Strategic Management, And Save Identity

- Move campaign-role/start facts out of map geography into an explicit scenario/Strategic Management definition source.
- Select compatible map and scenario data through a composition/config boundary rather than fixed factory paths.
- Persist and validate at least MapId and ScenarioId plus the required identity/content compatibility revision.
- Preserve supported current saves through one explicit, versioned, atomic migration into the mock map/scenario identity; mismatched new-map saves fail explicitly.

### 6. Migration, Proof, And Cleanup

- Migrate current source/artifacts into an explicit mock map package without polishing or claiming final content.
- Provide a second test package with different world dimensions, chunk count, and city/province composition.
- Prove both packages load through the same production scene and generic code by changing only package/scenario selection.
- Delete superseded singleton fixed-path loaders/config bindings and temporary in-task adapters after cutover; no long-term dual path remains.
- Update focused workbench, package, presentation, Strategic Management, and save regressions.

## Non-Goals

- No final world-map artwork, final geographic roster, content balancing, or manual polishing of the mock region shapes.
- No Stage 3 hover, selection, click, detailed-map entry/return, or semantic marker assignment.
- No Stage 4 navigation compilation, strategic movement, passage rules, or army simulation; this task only preserves/package-references navigation capability where applicable.
- No battle launch/settlement changes beyond map/scenario identity compatibility required by save/runtime composition.
- No main-scene cutover, legacy strategic-world deletion, or broad unrelated cleanup.
- No rewrite of the first-city detailed-map authoring workflow.
- No modification of `C:\Users\qs\asset`.

## Constraints And Risks

- Work only on dirty `main`; do not create or switch branches. Preserve every unrelated modification and untracked file.
- Do not run multiple write-capable Agents, Godot editors, builds, imports, or generators concurrently against this workspace.
- Do not start or terminate the user's Godot editor. Prefer static checks, focused tests, and low-concurrency builds.
- Existing Stage 0-2 work in the dirty tree is the baseline. Do not revert, reformat, or relocate unrelated files.
- MapId is immutable after creation. Renaming display text cannot rename identity or silently migrate saves.
- Draft tolerance must not weaken publish/runtime validation. Runtime consumes only a successfully published package.
- Published artifacts are derived snapshots, never a second editable geography authority.
- Categorical mask codes are not persistence or gameplay keys. `LocationId`/`ProvinceId` remain authoritative.
- Package/scenario/save mismatches fail before partial runtime initialization or state mutation.
- Moving many raster files risks Godot UID/import churn; use map-scoped migration and deterministic import verification rather than broad manual reimports.
- If exact atomic directory replacement is unreliable on Windows, publish immutable revision directories and atomically update only the small current-revision manifest/pointer.

## Acceptance Criteria

1. Authority and workstream documents describe the confirmed source/publish/runtime/scenario split and Stage 2.5 prerequisite without conflicting with gameplay authority.
2. The workbench can create, list, open, duplicate, and switch at least two MapId-scoped maps without overwriting one another or losing unsaved-change state.
3. A structurally valid but incomplete map can be saved and reopened; publishing reports profile-specific blockers and cannot expose an invalid revision.
4. Province authoring supports stable ProvinceId, name, one main city plus auxiliary members, and province-owned LayoutId without campaign-start roles in geographic source data.
5. Publishing stages and validates one internally consistent read-only package and never exposes mixed geography/mask/chunk revisions after failure.
6. Region masks are chunk-aligned, deterministic, exact to authored geometry, support more than 255 location geometries by contract/tests, and resolve only through stable LocationId lookup.
7. Category-mask and visual-chunk import settings are verified for their different purposes.
8. `StrategicMap.tscn` and generic production resources/code contain no concrete MapId, Qinghe/Chiyan identity, fixed 7168x5120 bound, singleton geography path, or singleton region-mask path.
9. The same production scene loads the migrated mock package and a materially different second package by changing only selection/configuration, with correct bounds, chunks, and static region presentation.
10. Strategic Management scenario data owns campaign roles and initial mutable facts, validates against the selected map package, and the current mock scenario still initializes all required city identities.
11. Current saves migrate atomically into explicit mock MapId/ScenarioId compatibility; a save from an incompatible map/scenario fails without partial mutation.
12. No superseded singleton runtime loader, global region-mask authority, permanent compatibility fallback, or dual write remains in the production path.
13. Focused workbench tests, package/StrategicMap regressions, Strategic Management/save regressions, low-concurrency main build, relevant static scans, and `git diff --check` pass or record only proven unrelated baseline failures.
14. The task names skills used, exact verification evidence, remaining risks, and hands off at `Awaiting Verification`; it is not self-completed by the executor.

## Current Progress Snapshot

Completed:

- Discussion confirmed the multi-map, draft/publish, package/runtime, scenario, and save boundaries.
- Read-only audit identified fixed singleton paths, editor/runtime schema mixing, strict draft-save validation, missing province CRUD, global 8-bit region mask, compiler-added edge distortion, fallback province union, fixed scene bounds/mask paths, hardcoded first scenario, and saves without map identity.
- Active task created on dirty `main` before implementation.
- Executor completed the required repository, authority, work-item, dirty-`main`, and skill bootstrap reads; no confirmed-direction conflict was found. Existing Stage 0-2 dirty changes remain the preserved baseline.
- Synchronized accepted architecture before implementation: MapId-scoped editable sources, capability-profile publication into immutable revisions, `rgb24-location-code-v1` exact chunk masks, generic loaded-map context, scenario-owned campaign starts, save compatibility identity, repository ownership, and terminal-migration Stage 2.5 are now explicit.
- Implemented the MapId catalog/source repository, incomplete-draft save boundary, create/open/duplicate/switch UI with dirty-state protection, province identity/name/LayoutId authoring, and profile-aware publishing controls.
- Implemented staged immutable publication, source-content revisions, exact chunk-aligned RGB24 region masks, category/visual import contracts, topology/overlap blockers, and pointer exposure only after complete revision installation.
- Migrated the current geography into `mock_qinghe_chiyan` and published it as a 7168x5120/35-chunk package. Added and published the materially different `fixture_north_pass` 2048x1024/2-chunk package with a different province/city composition.
- Workbench milestone verification passes: TypeScript typecheck and 43 Vitest cases, including multi-map isolation, draft reopen/publish blocking, failed-publish pointer preservation, exact geometry, chunk alignment, RGB24 code 256, and import-sidecar differences.
- Repaired the independent-verification findings with package schema v2. Every runtime-referenced chunk manifest, geography, visual, region mask, region lookup, region outline, and optional terrain/navigation artifact now carries a SHA-256 declaration; `ContentHash` is the deterministic aggregate of that exact artifact set.
- Runtime loading now confines the package manifest and every top-level/child reference to one immutable `MapId`/revision config or asset root, rejects retained reference media, validates exact artifact coverage, recomputes `ContentHash`, and streams every referenced file before returning a loaded context.
- Revision input now includes the schema/integrity contract and all capability source bytes, including navigation files for `strategic-runtime`; changing navigation content cannot collide with an existing immutable revision.
- Scenario composition now rejects unknown or duplicate `LocationId`, duplicate province assignments, province/location double assignment of a city, duplicate faction/resource starts, and negative amounts before definition/state creation. The public Strategic Management definition boundary reuses the same composition validation so loader bypass does not permit partial initial state.
- Added `location_timber_site` as canonical non-geometric `resource-site` geography for the mock scenario; the canonical city/region geometry set remains 11.
- Republished schema-v2 packages and switched selections: mock `r-39e55028317fc997`, fixture `r-352ea908c3e07bb7`. Old immutable schema-v1 revisions remain untouched.

Remaining:

- None. Independent verification is complete; future hover/click/entry work remains a separate Stage 3 task.

## Pause Or Blocker

None. If execution discovers a confirmed-contract contradiction, an unavoidable map-format decision with materially different product behavior, or overlap that cannot preserve unrelated dirty changes, set `Status: Needs Discussion`, record evidence, and stop mutations.

## Resume Condition

Resume from `Ready` or `In Progress` only with this task as the controlling contract and the authority documents synchronized to the confirmed result.

## Resume Entry

1. Read repository `AGENTS.md`, `gameplay-alignment/authority-map.md`, this task, and the four named authority/workstream files.
2. Recheck `git branch --show-current` and scoped dirty-file overlap.
3. Set the task to `In Progress` and update authority before implementation code/resources.
4. Start with source/package/scenario schemas and path ownership; do not start from hover or visual polish.

## Latest Verification

Independent verification by `root` completed without starting Godot:

- Re-ran the workbench suite: 14 files / 45 tests passed.
- Re-ran StrategicMap foundation 5/5, presentation 9/9, and Strategic Management 122/122 in strict sequence; the integrity tests rejected tampered files and cross-revision references, and scenario composition rejected unknown/duplicate/double assignments before state creation.
- Rebuilt `rpg.csproj` with `-maxcpucount:2 -v:minimal`: 0 warnings / 0 errors.
- Independently recomputed every declared artifact SHA-256 and aggregate `ContentHash` for both selected schema-v2 packages. Mock `r-39e55028317fc997` passed with 74 artifacts / 35 chunks; fixture `r-352ea908c3e07bb7` passed with 8 artifacts / 2 chunks.
- Re-ran fixed-identity, fixed-size, singleton-path, retired-loader, and geographic campaign-role scans with no forbidden production hit.
- Re-ran `git diff --check`; it passed with only the pre-existing CRLF conversion notice for an unrelated regression file.
- Review against `godot-testing` and `godot-code-review` found no remaining scoped critical or improvement item. The task satisfies all acceptance criteria and may be archived.

Executor verification completed without starting Godot:

- Workbench production build: `npm run build` passed, including TypeScript typecheck, Vite client build, and server compilation.
- Workbench tests: `npm test -- --reporter=dot` passed 14 files / 45 tests. New coverage verifies exact schema-v2 hash declarations, aggregate `ContentHash`, referenced-file bytes, failed-publish pointer preservation, and navigation-byte revision changes.
- StrategicMap foundation regression: 5 passed. The new isolated-package regression proves a valid fixture loads and rejects geography/visual tampering, altered `ContentHash`, top-level cross-revision paths, and chunk child cross-revision paths without mutating repository packages.
- StrategicMap presentation regression: 9 passed, including loading both published packages through the same generic contracts.
- Strategic Management regression: 122 passed, including scenario-owned starts, v5 identity validation, incompatible-map rejection, explicit mock-only v4 migration, and pre-state rejection of unknown/duplicate/double initial assignments.
- Strategic region preview regression: 6 passed after routing the retained preview to the MapId-scoped mock source.
- WorldSite deployment/cache relevant regression suite passed in full; its pre-existing nullable warnings do not fail the suite.
- Main build: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings / 0 errors on the final run.
- Static scans passed: generic StrategicMap production code/resources contain no current MapId, Qinghe/Chiyan identity, fixed 7168/5120 bounds, singleton geography path, singleton region-mask path, or retired singleton loader. Geography sources/packages contain no campaign role field/type.
- Published import audit found 37 visual and 37 region `.png.import` sidecars (35 mock + 2 fixture) with no generated `.godot/imported` cache committed or fabricated.
- `git diff --check` passed. Branch remained `main`; the pre-existing dirty workspace was preserved. Build servers were shut down after verification.
- `godot-code-review` handoff review found no critical or improvement item requiring a scoped change: node references are cached in `_Ready`, per-frame work uses cached state, visual chunks remain bounded/threaded, dynamic visuals use `QueueFree`, and authored scenes/resources retain presentation structure. Small categorical region masks are synchronously resolved only during initial composition.
- Remaining risk: SHA-256 establishes internal package consistency for this local generated-content pipeline; package signing and hostile publisher authentication are outside the confirmed scope. Runtime and presentation were not launched because the task explicitly prohibited starting Godot; independent verification should retain that constraint.

## Execution Record

- 2026-07-13: User confirmed the systematic multi-map/editor/package/runtime redesign and explicitly authorized execution with an activity task document.
- 2026-07-13: Task created on `main` with the existing large dirty Stage 0-2 baseline preserved. Planned implementation skills: `assets-pipeline`, `resource-pattern`, `csharp-godot`, `scene-organization`, and `godot-testing`; use `save-load` for the scoped save identity migration.
- 2026-07-13: Executor set the task to `In Progress` after reading current authority/workstream, confirming dirty `main`, and loading `assets-pipeline`, `resource-pattern`, `csharp-godot`, `scene-organization`, `godot-testing`, and `save-load`. Authority synchronization is the first implementation milestone.
- 2026-07-13: Authority milestone completed before code/resource changes. Updated `strategic-world-map-authoring-architecture`, `strategic-management-system-architecture`, `resource-authoring-taxonomy`, and the terminal-migration workstream; no gameplay authority changed.
- 2026-07-13: Workbench and publication milestone completed. `npm run typecheck` passed; `npm test -- --reporter=dot` passed 14 files/43 tests. Published revisions: mock `r-b443ddb17142d592`, fixture `r-3d513e074acc4eb5`.
- 2026-07-13: Runtime composition milestone completed. Production selection loads package + scenario, Strategic Management validates the selected content identity, scenario data owns province roles/control/resources, and save format v5 persists MapId/ScenarioId/package/scenario revisions with explicit mock-only v4 migration.
- 2026-07-13: Removed the superseded root-level geography/project JSON and `StrategicMapCanonicalLoader`; production fixed-path and concrete-identity scans are clean. Updated focused regressions and retained preview routing.
- 2026-07-13: Executor completed `godot-code-review`, full scoped regression/build/static verification, and handed off at `Awaiting Verification`. Skills used: `assets-pipeline`, `resource-pattern`, `csharp-godot`, `scene-organization`, `godot-testing`, `save-load`, and `godot-code-review`.
- 2026-07-13: Independent verification returned the task to `In Progress`: package `ContentHash` was not runtime-verifiable and package child paths were not proven to share one immutable MapId/revision root; scenario location starts also lacked unknown/duplicate assignment validation before state creation.
- 2026-07-13: Executor implemented schema-v2 artifact hashes and same-revision confinement, atomic scenario composition validation, focused regressions, and schema-v2 package republication. Final scoped verification passed and the task returned to `Awaiting Verification`. Skills used remained `assets-pipeline`, `resource-pattern`, `csharp-godot`, `scene-organization`, `godot-testing`, `save-load`, and `godot-code-review`.
- 2026-07-13: `root` independently verified the repaired package and scenario contracts, repeated the focused suites and main build, recomputed both selected packages' complete integrity chains, found no remaining scoped defect, and marked the task `Completed`. Verification skills used: `godot-testing` and `godot-code-review`.

## Final Result

Completed and independently verified. The multi-map workbench/publisher, immutable schema-v2 package contract, generic StrategicMap consumption, scenario-owned initialization, and save identity migration now form the accepted Stage 2.5 baseline. Stage 3 hover, selection, clicking, and detailed-map entry remain explicitly outside this completed task.
