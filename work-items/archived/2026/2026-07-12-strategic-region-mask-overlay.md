# Strategic Region Mask Overlay Rebuild

Status: Completed
Executor: Codex current context
Verifier: Parent Agent (independent context)
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Replace the standalone strategic-region preview's per-region Polygon2D rendering with chunk-aligned region-ID mask overlays so borders, hover, and selection render without polygon triangulation artifacts.

## Confirmed Discussion Result

- Use the mature grand-strategy pattern: derived region-ID masks plus one overlay shader per visual chunk.
- Region geometry and identity remain canonical in geography/workbench artifacts.
- Runtime must not triangulate or fill one Polygon2D per region.
- Soft borders come from mask-neighbour sampling; hover/selection compare the sampled region id against state uniforms.
- The preview remains standalone and does not affect the formal strategic runtime, persistence, or main scene.
- The user verified that the prototype reaches the minimum acceptable visual quality for future strategic-region presentation.
- Preserve only the player-facing visual baseline as durable authority. The standalone scene, shader, mask compiler changes, and other prototype code/resources remain test/reference material and are not a formal runtime implementation or a mandatory future technical solution.

## Authority Impact

- Add a focused gameplay-detail authority for strategic-world region presentation and route it from the gameplay detail index.
- Record natural curved boundaries, restrained low-saturation translucent/frosted treatment, faction-readable outer borders, local-only hover emphasis, and strict cross-city/cross-faction visual isolation as the minimum player-facing baseline.
- Update `system-design/strategic-world-map-authoring-architecture.md` only to reference that presentation authority and explicitly avoid prescribing reuse of this prototype's shader, scene, mask compiler, or other technical implementation.

## Scope

- Produce or consume chunk-aligned territory ID masks for the six preview chunks.
- Add a reusable chunk overlay scene, material, and CanvasItem shader.
- Pick regions from the ID mask in canonical world coordinates.
- Drive hover/selection and the existing HUD from region ids.
- Delete the preview's Polygon2D region and city-territory rendering path.
- Retain city anchors, reference chunks, camera, and standalone isolation.
- Synchronize the verified visual baseline into gameplay and system authority without promoting prototype code to production authority.
- Complete and archive this work item as a verified visual-reference prototype.

## Non-Goals

- No strategic ownership, fog, encounter, persistence, battle entry, or formal runtime integration.
- No final terrain art or campaign geography authoring.
- No per-region mesh, Polygon2D, or screen-backbuffer solution.
- No decision that the formal strategic runtime must reuse the prototype scene, shader, compiler, resources, or exact rendering technique.
- No new visibility, detection, encounter-timing, territory-control, or detailed-map mapping gameplay rules.

## Constraints And Risks

- Region-ID textures must use nearest-neighbour sampling and stable integer encoding.
- Soft visual edges must not blur the sampled identity used for picking.
- Mask/world/chunk transforms must remain exact.

## Acceptance Criteria

- Preview contains no per-region Polygon2D fill or closed per-region outline rendering.
- Exactly one region overlay instance exists per intersecting preview chunk.
- Shader derives fill and border from a nearest-sampled region-ID mask.
- Hover and selection resolve region ids from masks and update the existing HUD.
- Standalone scene and focused regression/build checks pass.
- User performs final visual verification.
- Gameplay authority records the confirmed minimum visual baseline without implementation-specific tunables.
- System authority references the visual baseline while keeping the formal implementation technique open.
- The archived result identifies this work as a standalone reference prototype, not production integration.

## Progress Snapshot

- Completed: prototype implementation, automated checks, independent user visual/interaction verification, gameplay-authority synchronization, detail-index routing, and the non-prescriptive system-authority reference.
- Changed documents: `gameplay-design/details/strategic-world-region-presentation.md`, `gameplay-design/details/README.md`, `system-design/strategic-world-map-authoring-architecture.md`, and this work item.
- Remaining: None.

## Pause Or Blocker

None.

## Resume Entry

Verify this work item against `gameplay-design/details/strategic-world-region-presentation.md`, its route in `gameplay-design/details/README.md`, and the reference in `system-design/strategic-world-map-authoring-architecture.md`. Review only the scoped documentation diff. Do not treat prototype implementation files as formal runtime authority.

## Latest Verification

- Independent parent-Agent verification: passed. The minimum visual baseline is complete, both authority routes resolve, unconfirmed gameplay remains excluded, and no prototype implementation is promoted to production authority.
- User visual acceptance: the standalone prototype was confirmed as the minimum acceptable visual quality for future strategic-region presentation.
- Documentation path check: all four scoped documents exist.
- Focused route check: the gameplay detail index and strategic-world authoring architecture both resolve to `gameplay-design/details/strategic-world-region-presentation.md`.
- Focused content check: the authority contains every confirmed visual baseline, separates unconfirmed gameplay rules, and marks all standalone prototype implementation as reference evidence only.
- Scoped `git diff --check`: passed.
- Installed GodotPrompter skills: none apply; this execution is documentation-only.
- Focused strategic-region regression: 5/5 passed.
- Workbench: 12 files / 34 tests passed; typecheck and production build passed.
- Categorical-mask regression: 4/4 artifact tests passed; hostile-edge rasterization contains only background or the exact hostile id.
- Regenerated-mask spatial check: zero player ids exist to the right of mask X=900, the hostile territory range.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: 0 warnings and 0 errors.
- Godot 4.7 Compatibility renderer: shader compilation and direct-scene run passed.
- Cross-faction halo correction: focused regression, Godot shader compilation, direct-scene run, and diff check passed; user interaction verification remains.
- Visual capture contains no polygon triangulation colors, black mask blocks, or repeated chunk UVs.
- `git diff --check`: passed.

## Execution Record

- 2026-07-12: Independent parent-Agent review passed; the task was marked `Completed` for archive.
- 2026-07-12: Added focused strategic-world region-presentation gameplay authority, routed it from the detail index, and added a minimal non-prescriptive reference from the accepted map-authoring architecture.
- 2026-07-12: Focused path, route, content, and scoped diff checks passed; documentation execution handed off at `Awaiting Verification` for independent parent-Agent review.
- 2026-07-12: Documentation-only authority synchronization started; task set to `In Progress`. No installed GodotPrompter skill applies because this execution changes documentation only.
- 2026-07-12: Work item created after user confirmed the mask-overlay rebuild.
- 2026-07-12: Removed per-region and city-territory Polygon2D implementations.
- 2026-07-12: Added one explicit ID-mask overlay per preview chunk and mask-based mouse picking.
- 2026-07-12: Moved deterministic natural shared-edge sampling into the workbench artifact compiler and regenerated territory artifacts.
- 2026-07-12: Corrected explicit mask uniforms and per-chunk global UV transforms after engine checks.
- 2026-07-12: User reported cross-faction blue halo points and confirmed strict center-region color/hover gating correction.
- 2026-07-12: Removed neutral halo coloring from borders; hover/selection strength now uses exact center-region gates and every edge/halo inherits the center region faction color.
- 2026-07-12: GPU ID diagnostic proved SVG antialiasing converted hostile IDs 5–8 into valid player IDs 1–4 at region edges; user confirmed categorical binary-mask compilation fix.

- 2026-07-12: Recompiled each region through a thresholded binary coverage mask and wrote exact categorical ids into the final PNG; regenerated all territory artifacts and added an edge-leak regression.
- 2026-07-12: Full workbench tests, typecheck, production build, focused preview regression, solution build, and diff check passed. Final hover verification remains with the user.

## Final Result

Completed and independently verified. The accepted player-facing minimum is authoritative, while the standalone prototype remains reference evidence and the formal rendering technique remains open. Remaining risks: none within this visual-baseline scope. Vision, discovery, encounter timing, territorial control, and detailed-map mapping remain separate future discussion subjects.
