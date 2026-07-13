# StrategicMap Stage 1 Production Chunk Presentation

Status: Completed
Executor: Stage 1 execution Agent (current context)
Verifier: Main Agent independent verification context
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Implement the greenfield production `StrategicMap` Chunk presentation path so the accepted canonical world can boot in an isolated authored scene, display and query production visual Chunks, show the canonical static province/city-region presentation baseline, and support bounded map-camera inspection without any legacy world or frozen-Preview runtime dependency.

## Confirmed Discussion Result

- This is terminal-migration Stage 1. Stage 0 is completed and verified.
- The user authorized implementation of Stages 1 and 2 in order and expects an initial large-world visual acceptance after both.
- Stage 1 owns production presentation only. It must not read mutable Strategic Management state, add interaction, navigation, movement, detailed-map entry, battle wiring, cut over the main scene, or delete legacy code.
- Use final-named `StrategicMap` directories and authored `.tscn`/`.tres`/shader resources. Runtime code binds data and manages residency; it must not construct the full scene structure or resource family in code.
- Reuse the existing generic `MapCameraController`; do not fork camera behavior.
- The user-accepted Preview quality is the minimum visual baseline, but Preview scenes, scripts, shaders, configs, and internal models remain frozen reference evidence and cannot be production dependencies.
- Promote the 35 accepted reference Chunk pixels into a separate production visual-chunk directory by byte-preserving copy and bind them through canonical `visualTexturePath`. Reference images remain unchanged and separate. This establishes the initial production visual baseline without claiming final art lock.

## Authority Impact

None. Current accepted authority already defines the greenfield `StrategicMap` presentation roots, canonical manifest, final visual-chunk separation, resource taxonomy, static geography ownership, Preview boundary, and Stage 1 scope. Update only the migration workstream progress record, not gameplay or system authority.

## Architecture Judgment

- Subsystems: Map presentation, Camera, Content Pipeline, static geographic query.
- Reuse: `StrategicMapCanonicalLoader`, canonical chunk/geography definitions and validation, generic `MapCameraController`, canonical derived territory masks and lookup artifacts.
- New owner: `src/Presentation/StrategicMap/`, `scenes/world/strategic_map/`, and `resource/world/strategic_map/` own production presentation. Canonical facts remain under `config/world/`; production pixels remain under `assets/textures/world/`.
- Long-term safety: no new type in this scope may reference `StrategicWorldRuntime`, `StrategicWorldState`, `WorldArmyState`, `StrategicWorldRoot`, visible legacy TileMap classes/scenes, fixed legacy site ids, or any `Presentation.World.Preview` type.

## Execution Scope

1. Add a focused authored production scene tree with a root composition scene, reusable Chunk visual scene, camera, static region overlay boundary, and minimal authored error/status presentation required for isolated inspection.
2. Add focused read-only presentation resources under `resource/world/strategic_map/`; do not duplicate canonical geography or mutable state.
3. Add presentation/application query code for canonical world bounds, world-position-to-Chunk resolution, visible-rect Chunk selection, stable residency decisions, and explicit load failures.
4. Copy all 35 accepted reference Chunk PNGs byte-for-byte into a separate production visual directory and add `visualTexturePath` to every canonical manifest entry. Do not modify reference pixels.
5. Render the canonical static region/province baseline from production-owned resources and derived masks without hover, selection, or Strategic Management state.
6. Reuse `MapCameraController` with authored world bounds, initial focus, zoom, keyboard movement, wheel zoom, and middle-mouse pan.
7. Add focused regression coverage for scene/resource isolation, complete production visual bindings, chunk coordinate/residency/world-query behavior, static geography presentation inputs, and forbidden dependencies.
8. Update the terminal-migration workstream Stage 1 status and maintain this task's execution/verification evidence.

## Non-Goals

- No mutable ownership/control source, Strategic Management integration, city interaction, hover, click, selection, or HUD action flow.
- No detailed-map entry/return, semantic marker mapping, navigation, movement, fog, discovery, supply, strategic AI, battle, settlement, cutover, or legacy deletion.
- No main-scene change and no modification of legacy world behavior.
- No use of visible TileMapLayer for the new large-world art.
- No Preview runtime import, inheritance, copied production responsibility, or feature iteration.
- No new artwork generation or modification of the accepted source pixels.

## Constraints And Risks

- Work on dirty `main`; preserve all unrelated changes and do not create or switch branches.
- Canonical JSON remains the only editable cross-tool chunk/geography authority. Godot resources may configure presentation but cannot duplicate coordinates, ids, province membership, or geometry.
- Production Chunk texture load failures must identify `ChunkId` and path; missing canonical data must fail the isolated scene visibly rather than silently fall back to reference paths.
- Residency must be deterministic and bounded. Do not reload unchanged visible chunks every frame.
- Static Stage 1 color treatment cannot become campaign-control truth; Stage 2 replaces its state source through a read-only Strategic Management presentation port.
- Do not start or terminate the user's Godot editor. Prefer static checks, focused regressions, and low-concurrency builds.

## Acceptance Criteria

1. `StrategicMap` has an independently runnable production scene under `scenes/world/strategic_map/`; `project.godot` still points to the legacy main scene.
2. All 35 canonical chunks have non-empty production `visualTexturePath` bindings whose files exist in a final visual directory separate from `reference/`, and every promoted PNG is byte-identical to its accepted source.
3. The production root displays/query-loads visible raster Chunks in canonical world coordinates, applies deterministic residency, and reports missing data/texture failures with stable ids and paths.
4. The scene camera is bounded to the 7168x5120 world, supports the existing map navigation controls, and starts focused on the authored initial inspection area.
5. Static canonical Qinghe/Chiyan city-region presentation is visible from production-owned scene/resource/shader code without owning mutable state or interaction.
6. No new production source/scene/resource references legacy world owners, visible TileMap art, fixed legacy site ids, or frozen Preview types/resources.
7. Focused regressions, manifest/workbench tests affected by the visual bindings, low-concurrency project build, and `git diff --check` pass; Godot is not started.
8. The task records exact scope, skills, evidence, remaining Stage 2 boundary, and hands off at `Awaiting Verification`.

## Current Progress Snapshot

### Completed

- Stage 0 greenfield foundation is completed and independently verified.
- User confirmed Stage 1 and Stage 2 execution and the initial-acceptance expectation.
- Main Agent completed the architecture check and loaded `using-godot-prompter`, `scene-organization`, `resource-pattern`, `camera-system`, `csharp-godot`, and `godot-testing`.
- Execution began on dirty `main`; required task, work-item route, current authorities, migration workstream, Stage 0 archive, canonical manifest/geography, and all listed GodotPrompter skills were read in full. No authority conflict was found.
- Added pure world/chunk query, deterministic residency, production visual-binding validation, and derived region-lookup validation under the greenfield `StrategicMap` layers.
- Added the independently runnable authored production scene, reusable Chunk visual and region-overlay scenes, focused presentation resource/shader, shared `MapCameraController` binding, visible Chinese status/error surface, and static canonical province/city-region treatment.
- Added all 35 production `visualTexturePath` bindings and promoted the accepted PNGs into the separate `assets/textures/world/visual-chunks/` directory by byte-preserving copy; all 35 source/target SHA-256 pairs match.
- Added `StrategicMapPresentationRegression`; its first run passed all 6 focused cases covering media bindings, queries, residency, region artifacts, authored scene bounds/isolation, and forbidden dependencies.
- Completed compatibility and build verification: Stage 0 foundation regression passed 4/4, frozen Preview regression passed 6/6, Web workbench passed 13 files / 39 tests plus production build, and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- Completed execution-side `godot-code-review`: no critical or required improvement remained. Final forbidden dependency and Stage 2+ scope scans were clean; `project.godot` still targets the legacy main scene; `git diff --check` passed; Godot was not started or terminated; build servers were shut down.
- Independent verification completed the first review pass and returned Stage 1 to execution with two scoped defects: the 35 production visual Chunk PNGs lack committed path-safe unique `.png.import` settings, and synchronous `GD.Load` texture residency can stall while decoding the roughly 51 MB production PNG set.
- Repaired both scoped defects: added 35 deterministic production-only lossless+mipmap import sidecars plus a collision-checking offline generator, and replaced synchronous Chunk texture loading with a bounded native threaded scheduler that collects terminal results, discards stale completions, and remembers failures for the scene lifetime.
- Extended `StrategicMapPresentationRegression` to 7 cases covering sidecar count/path/cache/UID/settings/media identity and pure bounded scheduling, duplicate rejection, stale completion, and no-retry failure behavior; the focused suite passes 7/7.
- Completed the full repair verification matrix: Stage 0 foundation 4/4, frozen Preview 6/6, Web workbench 13 files/39 tests plus production build, and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings/0 errors.
- Final execution-side `godot-code-review` found no critical or required improvement. Production forbidden-dependency scan is clean; the only Stage 2+ keyword scan hit is the accepted canonical `navigationScenePath` manifest field; all 35 source/target hashes and import contracts pass; no production `.godot/imported` cache was fabricated; `git diff --check` passes; Godot was not started or terminated; build servers were shut down.

### Remaining

- None. Stage 1 is independently verified and ready for archive.
- Stage 2 may now begin under its own confirmed active task.

## Pause Or Blocker

None.

## Resume Condition

No resume is required. Stage 1 is complete and independently verified.

## Resume Entry

Use the archived record and terminal-migration workstream as evidence if a later stage changes the production Chunk boundary.

## Verification Handoff

Independent verification passed every acceptance criterion that can be exercised without starting Godot. The verifier reviewed architecture isolation, authored resource/scene usage, all 35 media/import contracts, bounded threaded scheduling including stale/failure semantics, canonical queries, camera/static-region contracts, diagnostics, and forbidden dependencies. It reran StrategicMap presentation 7/7, foundation 4/4, frozen Preview 6/6, Web workbench 13 files/39 tests plus production build, low-concurrency project build with 0 warnings/0 errors, and `git diff --check`; build servers were shut down. Runtime visual inspection remains the user's post-Stage-2 acceptance activity rather than an unverified Stage 1 requirement.

## GodotPrompter Skills

- `using-godot-prompter`: implementation skill routing.
- `scene-organization`: production root and reusable Chunk scene composition.
- `resource-pattern`: focused read-only presentation resources without canonical-data duplication.
- `camera-system`: bounded Camera2D behavior and inspection controls.
- `csharp-godot`: Godot C# source/resource conventions.
- `godot-testing`: focused behavior and scene-contract regression strategy.
- `godot-code-review`: required during independent verification.
- `assets-pipeline`: production PNG import settings, cache-path/UID hygiene, mipmaps, and threaded large-resource loading.

## Execution Record

- 2026-07-13: Main Agent created the Stage 1 task after the user authorized implementation of the first two remaining terminal-migration stages.
- 2026-07-13: Stage 1 executor set the task to `In Progress` after confirming dirty `main`, preserving unrelated changes, completing the required bootstrap reads, and passing the scoped architecture check.
- 2026-07-13: Production presentation milestone completed. Focused presentation regression passed 6/6 cases; all 35 promoted Chunk PNGs are SHA-256 identical to their accepted sources. The first failed regression was a test-fixture property-name mismatch for `region_outlines.json`; after correcting the test to the actual version-2 artifact contract, the full focused suite passed. No product defect or direction change was involved.
- 2026-07-13: Compatibility verification passed: `npm test` 13 files / 39 tests; `npm run build`; StrategicMap foundation regression 4/4; frozen Preview regression 6/6; StrategicMap presentation regression 6/6; low-concurrency `rpg.csproj` build 0 warnings / 0 errors. `dotnet build-server shutdown` completed successfully.
- 2026-07-13: Final execution checks passed: production forbidden-dependency scan, Stage 2+ scope scan, 35-pair SHA-256 media identity, production-directory PNG-only check, unchanged legacy main-scene check, execution-side `godot-code-review`, and `git diff --check`. Skills used: `using-godot-prompter`, `scene-organization`, `resource-pattern`, `camera-system`, `csharp-godot`, `godot-testing`, and `godot-code-review`.
- 2026-07-13: Executor handed off at `Awaiting Verification`; no Stage 2 implementation was started.
- 2026-07-13: Independent verification returned Stage 1 to `In Progress` with two scoped defects: missing safe committed `.png.import` sidecars for all 35 production Chunks, and synchronous `GD.Load` residency for the roughly 51 MB PNG set. The repair remains presentation-only and Stage 2 remains unstarted.
- 2026-07-13: Scoped repair implementation completed. Added 35 reproducible production import sidecars with path-derived collision-checked UIDs and exact cache paths, then replaced synchronous visual residency with bounded Godot-native threaded requests and pure scheduling/failure state. The expanded focused regression passes 7/7; full verification remains in progress. Skills applied: `assets-pipeline`, `csharp-godot`, `resource-pattern`, `scene-organization`, `godot-testing`, and `using-godot-prompter`.
- 2026-07-13: Repair verification passed: StrategicMap presentation 7/7, foundation 4/4, frozen Preview 6/6, Web workbench 13 files/39 tests and production build, low-concurrency project build 0 warnings/0 errors, byte-idempotent offline sidecar generation, import UID collision and cache-path checks, zero fabricated production cache files, forbidden dependency/scope scans, and `git diff --check`. Godot was not started or terminated. `godot-code-review` found no critical or required improvement, build servers were shut down, and Stage 1 returned to `Awaiting Verification`.
- 2026-07-13: Main Agent independently reviewed the repaired scheduler/import implementation and reran the complete recorded matrix. All checks passed again; Stage 1 was set to `Completed` and approved for archive. No Godot process was started or terminated.

## Final Result

Stage 1 is complete and independently verified. The isolated production `StrategicMap` now presents canonical raster Chunks through bounded asynchronous residency, uses reproducible production import settings, preserves the accepted static province/city-region baseline, and remains isolated from mutable Strategic Management state and all legacy/Preview runtime owners. Stage 2 is authorized to begin under its own active task.
