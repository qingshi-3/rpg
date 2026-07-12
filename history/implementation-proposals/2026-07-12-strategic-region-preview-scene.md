# Strategic Region Preview Scene Implementation Proposal

Status: Visual Polish In Progress

## Origin

- Requirement: `STRATEGIC-REGION-PREVIEW-001`
- User acceptance: 2026-07-12, for a standalone independently runnable two-city preview scene.
- Originating design proposal: `design-proposals/archived/2026-07-10-strategic-world-chunk-authoring/`
- Accepted authority:
  - `system-design/strategic-world-map-authoring-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/resource-authoring-taxonomy.md`
- Parent implementation proposal: None.
- Supersedes: None.
- Superseded by: None.
- Amends: None.
- Amended by: None.
- Blocking issues: None known.

## Requirement

Create a standalone Godot 4.7 C# scene that can be opened and run directly without changing the project's main scene, scene router, strategic runtime, save data, or current gameplay. The scene demonstrates two city territories with four smaller regions each, reference-map chunk presentation, city anchors, hover/selection feedback, camera navigation, and a compact Chinese information HUD.

The preview consumes the accepted canonical geography and derived region artifacts. It must not hardcode city or region geometry in C# and must not become a second strategic-state authority.

### Accepted Visual Polish (2026-07-12)

- Keep canonical polygon geometry unchanged, but derive deterministic curved display borders so shared edges remain coincident and read like natural province boundaries.
- Replace flat hover fills with a translucent glass material: softened background sampling, restrained faction tint, edge sheen, and a short eased hover/selection transition.
- Keep collision and gameplay semantics unchanged; this is presentation-only and remains isolated to the preview scene.

## Architecture

```text
config/world/workbench.project.json + config/world/geography.json
-> existing workbench region validation/compilation
-> territory_mask.png + region_lookup.json + region_outlines.json
-> standalone Godot preview loader
-> reference chunk visuals + reusable region/city visual scenes
-> hover/selection signals -> preview HUD
```

The preview scene is a composition root. Repeated chunks, regions, polygon parts, and city anchors instantiate reusable packed scenes. Canonical geography remains JSON owned by the workbench; `.tres` owns only preview presentation configuration such as bounds, colors, opacity, and line widths.

## Scope

- Author two city locations and eight non-overlapping regions in `config/world/geography.json`.
- Keep the sample inside a configured `3 x 2` chunk preview window in the existing canonical world coordinates.
- Generate and consume the accepted region mask, lookup, city union outline, and region outline outputs.
- Create an independently runnable preview composition scene under `scenes/world/preview/`.
- Load only reference chunks intersecting the preview window and label them as reference-only visual validation.
- Render reusable city-territory, smaller-region, and city-anchor visuals.
- Default to quiet presentation; reveal the city outline and region highlight on hover or selection.
- Show Chinese city/region information and controls in an authored HUD scene.
- Reuse `MapCameraController` for wheel zoom, middle-mouse pan, and existing camera input actions.
- Add low-noise startup, selection, and explicit failure diagnostics.

## Non-Goals

- Do not change `project.godot` main-scene, autoload, input-map, or scene-router configuration.
- Do not edit or instance the current `StrategicWorldRoot` or current strategic runtime.
- Do not add save/load, strategic time, armies, navigation, ownership mutation, buildings, fog, vision, detection, encounters, battle entry, or detailed-map mapping.
- Do not treat reference chunks as accepted final runtime art.
- Do not create fallback geometry when canonical geography or derived artifacts are invalid or missing.
- Do not modify files under the external asset library.

## Touched Systems

- `config/world/geography.json`
- `assets/textures/world/masks/territory/`
- `resource/world/preview/`
- `scenes/world/preview/`
- `src/Presentation/World/Preview/`
- focused regression coverage
- this proposal and the implementation-proposal indexes

## GodotPrompter Skills

- `using-godot-prompter`
- `scene-organization`
- `resource-pattern`
- `csharp-godot`
- `godot-ui`
- `godot-testing`
- `godot-code-review`

## Implementation Tasks

1. Add RED checks for the standalone scene route, isolation rules, canonical two-city/eight-region content, reusable authored scenes, and loader contracts.
2. Author two city locations and eight simple polygon regions in the workbench geography document.
3. Validate and compile the geography through the existing workbench toolchain.
4. Add a read-only preview configuration Resource and authored `.tres` instance.
5. Implement narrow JSON DTOs and a loader for project, geography, and compiled region outlines.
6. Implement reusable chunk, region, city-outline, and city-anchor presentation scenes.
7. Implement the standalone composition root and signal-driven HUD binding.
8. Run focused tests, workbench tests/build, low-concurrency .NET build, and standalone scene smoke verification.
9. Review against the Godot code-review checklist and record exact evidence.

## Tests

- Geography contains exactly two city locations and eight regions.
- Every region has a unique id, valid city id, role, direction, and closed valid polygon.
- Each city owns four regions and compiled city-union geometry.
- Preview bounds intersect exactly six configured reference chunks.
- Scene files exist under `scenes/world/preview/` and do not modify the formal root scene or project main-scene route.
- Repeated region visuals use an authored packed-scene template.
- Loader preserves canonical coordinates without offset or scale conversion.
- Invalid or missing geography/artifact data fails explicitly with a named diagnostic.
- `npm test`, `npm run typecheck`, and `npm run build` pass for the workbench.
- Focused C# regression and `dotnet build rpg.sln -maxcpucount:2 -v:minimal` pass.

## Diagnostics

- Log one preview load summary with chunk, city, and region counts.
- Log selection changes only when the selected city or region changes.
- Report missing files, unsupported geometry, duplicate ids, unknown city ids, and invalid texture paths with stable ids and paths.
- Do not log pointer movement, hover frames, render frames, or full geometry payloads.

## Manual QA

- Open `scenes/world/preview/StrategicRegionPreview.tscn` and run the current scene with F6.
- Confirm the formal game main scene and current gameplay are unchanged.
- Confirm six reference chunks fill the preview window without visible coordinate drift.
- Confirm two city anchors appear and each city has four surrounding regions.
- Confirm default boundaries are quiet, city outline appears on hover, and the hovered region is emphasized.
- Confirm click locks selection and the Chinese HUD reports city, region, role, and direction.
- Confirm mouse-wheel zoom, middle-mouse pan, keyboard camera movement, and reset-view control work.
- Confirm no save file or strategic runtime state is read or written.

## Acceptance

- The scene runs independently and is not routed from current gameplay.
- Canonical geography and derived artifacts are the only geometry inputs.
- The two-city/eight-region visual concept is readable at default and zoomed camera levels.
- Presentation components are authored reusable scenes and contain no strategic gameplay authority.
- Automated verification passes and manual QA produces no blocking error.

## Verification Evidence

- Geography topology validation completed with no diagnostics; generated territory mask, region lookup, and region/city outlines contain the two-city/eight-region sample.
- Workbench verification passed: 12 test files / 32 tests, TypeScript typecheck, and production build.
- Focused C# regression passed all four checks: content shape, independent isolation, authored reusable resources/scenes, and canonical-coordinate loading.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 errors; remaining warnings belong to pre-existing code outside this preview.
- Godot 4.7 direct-scene headless smoke passed with exit code 0. Runtime diagnostic: `StrategicRegionPreviewLoaded chunks=6 cities=2 regions=8 bounds=(2048, 1024), (3072, 2048)`.
- Visual capture: `.codex/qa/strategic-region-preview/frame00000002.png` at 1960 x 1080 confirms six aligned reference chunks, two city anchors, region geometry, Chinese HUD, and preview-only labeling.
- Godot code-review checklist passed: composition responsibilities remain narrow; direct-child references are stable; signals are disconnected; resource loading is outside hot paths; input is confined to preview visuals/camera/HUD; no formal runtime, persistence, or scene-router coupling exists.
- Pending user visual QA: hover both cities and regions, click-lock/clear selection, reset view, wheel zoom, middle-mouse pan, and keyboard camera movement in an interactive F6 run.
