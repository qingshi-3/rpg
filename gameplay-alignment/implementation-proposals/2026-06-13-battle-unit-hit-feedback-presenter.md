# Battle Unit Hit Feedback Presenter Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review follow-up: remaining battle-facing Presentation optimization after `WorldSiteRoot` battle presentation decomposition.
- Priority: P1 battle Presentation cleanup; the user explicitly excluded strategic-management site-map / facility-slot presentation refactors from this pass.
- Authority documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`

No design proposal is required because this is a behavior-preserving Presentation refactor. If implementation changes damage truth, Runtime event order, health/death semantics, battle command authority, scene/resource taxonomy, or player-facing battle rules, stop and use the design proposal flow first.

## Scope

- Extract `BattleUnitRoot` hit feedback presentation into a focused `BattleUnitHitFeedbackPresenter`.
- Keep `BattleUnitRoot` as the scene-level battle unit shell that routes action result presentation and owns entity lifecycle, movement lanes, selection previews, action cues, and defeated presentation waits.
- Preserve existing behavior for:
  - attack/skill impact delay;
  - target-side skill impact FX;
  - hit outline pulse;
  - floating damage number spawn position and formatting;
  - runtime skill damage avoiding caster cast replay.
- Repair stale source-architecture guards that still read the removed `WorldSiteRoot.BattleRuntimePlayback.cs` file after live runtime observation moved to dedicated Presentation collaborators.

## Non-Goals

- Do not change strategic-management site-map, city/stronghold/resource-site, facility-slot, garrison, or large-map management presentation.
- Do not change Runtime damage, health, defeat, movement, command validation, settlement, or report semantics.
- Do not redesign battle unit scenes, authored FX resources, damage-number resources, outline shaders, or animation timing.
- Do not move movement lanes, command selection, target preview, action cues, thunder-mark presentation, or defeated presentation into this presenter.

## Touched Systems

- `src/Presentation/Battle/Entities/BattleUnitRoot*.cs`
- New `src/Presentation/Battle/Entities/BattleUnitHitFeedbackPresenter.cs`
- `tests/BattleHitFeedbackRegression` source-architecture guards
- This implementation proposal index

## Tests

- Add RED source-architecture guard requiring `BattleUnitHitFeedbackPresenter`.
- Run `tests/BattleHitFeedbackRegression` for battle feedback and stale runtime-observer guard coverage.
- Run `tests/WorldSiteDeploymentCacheRegression` because the previous `WorldSiteRoot` decomposition and runtime observer changes are adjacent.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.

## Diagnostics

- Preserve existing low-noise presentation behavior. The new presenter may create authored feedback resources and call existing components, but it must not emit Runtime events, mutate health, decide defeat, advance Runtime, or create hidden fallback gameplay behavior.

## Manual QA

No Godot editor launch is required for this behavior-preserving source decomposition unless automated checks expose a runtime-only issue. Optional manual QA: launch battle, watch a basic attack and a skill damage event, confirm target pulse, damage number position, impact FX, hit audio, and death timing remain unchanged.

## Acceptance Evidence

- 2026-06-13: RED: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed because `BattleUnitHitFeedbackPresenter.cs` did not exist. During the RED pass, stale `WorldSiteRoot.BattleRuntimePlayback.cs` source guards were migrated to the current live observer source set so the test failure targeted this slice instead of an already-deleted file.
- 2026-06-13: GREEN: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after extracting `BattleUnitHitFeedbackPresenter` and updating hit-feedback guards. Existing Godot source-generator warning remains in the test host.
- 2026-06-13: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
