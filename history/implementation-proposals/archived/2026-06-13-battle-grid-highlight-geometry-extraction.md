# Battle Grid Highlight Geometry Extraction Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review follow-up: remaining battle-facing Presentation optimization after `BattleUnitRoot` hit feedback extraction.
- Priority: P1 battle Presentation cleanup; the user explicitly excluded strategic-management site-map / facility-slot presentation refactors from this pass.
- Authority documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`

No design proposal is required because this is a behavior-preserving Presentation refactor. If implementation changes highlight meaning, target legality, runtime command authority, deployment validation, or authored scene/resource taxonomy, stop and use the design proposal flow first.

## Scope

- Extract battle grid highlight geometry calculations from `BattleGridHighlightOverlay` into a focused `BattleGridHighlightGeometry` helper.
- Keep `BattleGridHighlightOverlay` owning hover input, highlight layer state, dynamic node creation, styles, tile-layer configuration, and pause/static presentation.
- Preserve existing geometry for:
  - cell polygons;
  - skill range boundary segments;
  - hover footprint frames;
  - target-lock footprint frames;
  - path arrow cell centers and extents.

## Non-Goals

- Do not change strategic-management site-map, facility-slot, city/stronghold/resource-site, garrison, or large-map management presentation.
- Do not change highlight colors, animation/pulse timing, shader use, tile-layer draw order, hover semantics, target picking, skill range semantics, deployment-zone validation, or command submission.
- Do not redesign authored overlay resources or replace current vector overlay drawing in this pass.

## Touched Systems

- `src/Presentation/Battle/BattleGridHighlightOverlay.cs`
- New `src/Presentation/Battle/BattleGridHighlightGeometry.cs`
- `tests/BattleHitFeedbackRegression` source-architecture guards
- This implementation proposal index

## Tests

- Add RED source-architecture guard requiring `BattleGridHighlightGeometry`.
- Run `tests/BattleHitFeedbackRegression`.
- Run `tests/TargetBattleArchitectureRegression` because oversized file guards and battle Presentation source guards are adjacent.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.

## Diagnostics

- No new runtime logs are required. Geometry helper must stay Presentation-only and must not read Runtime state, issue commands, mutate deployment requests, or create hidden fallback behavior.

## Manual QA

No Godot editor launch is required for this behavior-preserving source decomposition unless automated checks expose a runtime-only issue. Optional manual QA: hover units with multi-cell footprints, preview movement/path, enter skill target picking, and confirm target-lock and skill-range outlines match the previous geometry.

## Acceptance Evidence

- 2026-06-13: RED: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed because `BattleGridHighlightGeometry.cs` did not exist.
- 2026-06-13: GREEN: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after extracting grid highlight geometry. Existing Godot source-generator warning remains in the test host.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
