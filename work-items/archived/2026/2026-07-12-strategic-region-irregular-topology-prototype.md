# Strategic Region Irregular Topology Prototype

Status: Completed
Executor: executor (`gpt-5.6-sol`, high)
Verifier: User
Created: 2026-07-12
Updated: 2026-07-13

## Objective

Replace the standalone strategic-region preview's two regular four-quadrant city territories with two visibly different irregular topologies and remove the prototype's four-regions-per-city presentation limit.

## Confirmed Discussion Result

- Ignore the abandoned discussion about continent-scale or fixed multi-city detailed maps; it has no authority or implementation effect.
- Qinghe becomes a five-region serpentine river-valley territory with an asymmetric crescent-like outer silhouette, curved ribbon, bottleneck, fan, and offset core shapes.
- Chiyan becomes a six-region horseshoe mountain-valley territory with a concave outer silhouette, unequal ridge/pass/mine/basin/wasteland shapes, and no quadrant reading.
- Adjacent regions must share exact boundaries so compiled artifacts contain no gaps, overlaps, or mismatched curves.
- Remove the `Vector4`/four-id city-membership limit. Region faction and city membership must be data-driven for all mask ids supported by the prototype.
- Preserve the accepted frosted, restrained overlay, faction-readable outer borders, local-only hover/selection, and cross-city/cross-faction isolation.
- The scene, code, shader, artifacts, and geography remain standalone reference-prototype material and do not enter the formal game runtime.

## Authority Impact

None. `gameplay-design/details/strategic-world-region-presentation.md` already defines the durable minimum visual baseline and explicitly treats this prototype as reference evidence only.

## Scope

- Redraw the two prototype city territories and their internal shared boundaries in canonical geography data.
- Regenerate the derived territory mask, lookup, and outlines.
- Replace fixed four-id shader membership uniforms with a data-driven lookup suitable for arbitrary per-city region counts within the prototype's global mask-id limit.
- Update focused regression coverage for unequal region counts, irregular/concave topology, exact membership, hover isolation, and standalone isolation.
- Update prototype HUD labels only where existing hardcoded region-name translations would otherwise expose stale names.

## Non-Goals

- No formal strategic runtime integration.
- No continent-scale detailed world, streaming world, multi-city detail-map rule, vision, discovery, encounter timing, territorial-control, or detailed-map mapping work.
- No change to the accepted strategic-region visual baseline.
- No production content authoring or final terrain art.

## Constraints And Risks

- Do not simulate irregularity by merely perturbing the existing four quadrants; both city silhouettes and internal topology must be structurally different.
- Do not introduce a new small fixed per-city region cap.
- Preserve exact categorical mask ids; visual softness remains presentation-only.
- Keep each smaller region contiguous and avoid holes.
- Preserve the heavily dirty worktree and do not modify unrelated files.
- Do not start or terminate the user's Godot editor.

## Acceptance Criteria

- Qinghe has exactly five contiguous regions and reads as a serpentine river-valley territory rather than quadrants.
- Chiyan has exactly six contiguous regions and reads as a concave horseshoe mountain-valley territory rather than quadrants.
- The two cities differ in count, outer silhouette, internal topology, area distribution, and connection pattern.
- Every region receives correct faction/context presentation regardless of being the fifth or sixth region in its city.
- Hovering or selecting one region affects only that region and never contaminates another city or faction.
- Region compilation reports no overlap, invalid geometry, duplicate id, or cross-city conflict for the prototype geography.
- Focused workbench tests, preview regression, type/build checks, and diff checks pass.
- User performs final visual and interaction verification.

## Progress Snapshot

- Completed: canonical Qinghe 5-region branched serpentine topology and Chiyan 6-region horseshoe topology; exact shared boundaries; 256-entry mask metadata lookup; shader/C# binding; focused labels and regressions; scoped territory artifact regeneration; automated semantic, topology, regression, type, server-build, C# build, and diff checks; user visual/interaction verification and independent acceptance.
- Remaining: None.

## Pause Or Blocker

None. The normal Vite client build remains unable to start `esbuild` in the recorded environment (`spawn EPERM`), but the user accepted this as a non-blocking environment limitation after all other recorded checks passed and the required visual/interaction verification was completed.

## Resume Entry

Not applicable. The task is complete and has no remaining scoped work.

## Verification Handoff

- The user personally accepted the standalone preview's visual and interaction result, including the two irregular territory readings, all eleven local-only hover/selection interactions, faction isolation, restrained frost, and overall visual quality.
- The user confirmed that the prototype has no gameplay functionality and will receive no feature iteration.
- The previously blocked normal Vite client build remains recorded as blocked at `esbuild` startup by environment `spawn EPERM`; it is not claimed as passed and is accepted as a non-blocking environment limitation.

## GodotPrompter Skills

- `csharp-godot` for Godot C# runtime binding and image/texture handling.
- `godot-testing` for focused regression updates and verification.
- `godot-debugging` only if execution encounters a runtime or shader failure requiring diagnosis.
- No additional installed GodotPrompter skill applies to this documentation-only closure. The entries above preserve the historical implementation skill-use record.

## Execution Record

- 2026-07-12: Task created after the user confirmed returning to the standalone prototype, removing the four-region limit, and replacing both quadrant layouts with structurally irregular shapes.
- 2026-07-12: Execution started on `main`. Confirmed no authority update is required and recorded the heavily dirty worktree as an isolation constraint.
- 2026-07-12: Discarded an incorrect recursive-delegation attempt. The current execution channel is itself the required explicitly configured executor; task returned to `In Progress` without implementation changes from that attempt.
- 2026-07-12: Implemented distinct exact-boundary Qinghe and Chiyan topologies and replaced all `Vector4` city membership uniforms with one 256-entry categorical metadata texture. Added focused 5/6-count, adjacency, concavity, isolation, and no-small-cap regression contracts; workbench TypeScript typecheck passed. Focused Vitest startup is currently environment-blocked by `esbuild` `spawn EPERM`, before test discovery.
- 2026-07-12: Regenerated only `territory_mask.png`, `region_lookup.json`, and `region_outlines.json` through `ProjectRepository.compileRegions`. Semantic checks found 11 unique ids, exact 5/6 city membership, pixels for every id, no unexpected categorical values, two valid Polygon unions, and no union fallback.
- 2026-07-12: Worked around the environment's Node child-process restriction without changing test logic by compiling TypeScript to a deleted temporary directory and running Vitest in one worker thread: focused 6/6 and full workbench 36/36 passed. `StrategicRegionPreviewRegression` passed 6/6; low-concurrency `dotnet build rpg.csproj` passed with 0 warnings and 0 errors; `git diff --check` passed.
- 2026-07-12: Normal `npm run build` passed its TypeScript typecheck stage but Vite could not start `esbuild` (`spawn EPERM`). Direct server TypeScript build passed. MSBuild server shutdown succeeded; compiler-server shutdown reported failure and no process was forcibly terminated.
- 2026-07-13: The user personally completed visual and interaction verification and accepted the standalone prototype. The user accepted the blocked normal Vite client build as a non-blocking environment limitation, confirmed there will be no feature iteration, and directed that the unchanged implementation remain as a frozen temporary visual reference.

## Final Result

Completed and independently verified by the user. The standalone implementation is preserved unchanged as a frozen temporary visual reference; it has no gameplay functionality and will receive no feature iteration.

Exact scoped files changed by this task:

- `config/world/geography.json`
- `assets/textures/world/masks/territory/territory_mask.png`
- `assets/textures/world/masks/territory/region_lookup.json`
- `assets/textures/world/masks/territory/region_outlines.json`
- `src/Presentation/World/Preview/StrategicRegionPreviewRoot.cs`
- `src/Presentation/World/Preview/StrategicRegionOverlayChunk.cs`
- `src/Presentation/World/Preview/StrategicRegionPreviewHud.cs`
- `resource/world/preview/strategic_region_overlay.gdshader`
- `tests/StrategicRegionPreviewRegression/Program.cs`
- `tests/StrategicRegionPreviewRegression/PreviewRegressionCases.cs`
- `tools/world-map-workbench/tests/strategicRegionPrototype.test.ts`
- `work-items/archived/2026/2026-07-12-strategic-region-irregular-topology-prototype.md`

Verification evidence:

- Workbench focused tests: 6/6 passed.
- Workbench full tests: 36/36 passed.
- Workbench typecheck and direct server TypeScript build: passed.
- Normal workbench Vite build: blocked at `esbuild` startup by environment `spawn EPERM`; not claimed as passed.
- `StrategicRegionPreviewRegression`: 6/6 passed.
- Low-concurrency main C# build: passed, 0 warnings, 0 errors.
- Geography validation: 5 Qinghe + 6 Chiyan regions, 11 unique ids, no overlap, invalid geometry, duplicate id, or cross-city conflict.
- Derived artifacts: exact categorical values 0–11, every region present, no city-union fallback.
- `git diff --check`: passed; only unrelated dirty-file line-ending warnings were printed.

GodotPrompter skills used: `csharp-godot`, `godot-testing`.

Remaining scoped work: None.

Final disposition: user-accepted and archived; preserve the preview implementation unchanged as a frozen temporary visual reference with no feature iteration.

Remaining risks: None within the confirmed scope. The normal Vite client build was not rerun successfully and is not claimed as passed; its `esbuild` `spawn EPERM` failure is accepted as a non-blocking environment limitation because all other recorded checks passed and the user completed the required visual/interaction verification.
