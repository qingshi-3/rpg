# Battle Presentation Motion Lane Smoothing

Status: Implemented - Automated Verification Passed

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## Scope

Improve live battle movement readability by smoothing Presentation consumption of already-confirmed Runtime movement events.

Runtime remains authoritative for actor cells, occupancy, reservations, attack range, movement commits, and combat events. Presentation may buffer and interpolate committed movement segments, but it must not predict future cells or create alternate movement truth.

## Non-Goals

- Do not change Runtime pathfinding, target choice, reservations, or movement legality.
- Do not let Presentation infer or predict the next movement cell.
- Do not change battle settlement, reports, or combat event semantics.

## Touched Systems

- `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- `src/Presentation/Battle/Entities/BattleUnitRoot.Movement.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePerceptionOverlay.cs`
- `src/Presentation/World/Sites/BattleRuntimeLivePresentationState.cs`
- `tests/BattleHitFeedbackRegression/`

## Implementation Direction

- Treat `move` animation as a sustained visual state. Consecutive Runtime movement events append visual segments and must not restart the move animation.
- Add a short actor-local visual buffer window at the start of a movement stream and after a lane drains. This buffer only waits for confirmed movement events; it does not predict future cells.
- Keep visual interpolation frame-driven over queued committed segments so movement reads as continuous even though Runtime commits grid cells discretely.
- Queue perception/debug overlay refreshes instead of rebuilding them once per movement event.

## Tests

- Regression test that live movement uses actor-local motion lanes without per-step tween creation.
- Regression test that consecutive movement segments enter a buffered lane without replaying move animation.
- Regression test that movement-triggered perception overlay refresh is queued/batched.

## Diagnostics And QA

- Existing battle performance monitors remain the primary automated diagnostic entry point: movement event count, active movement lanes, presentation observe time, and movement event gap.
- Manual QA should check a battle with multiple companies moving at once: units should keep a continuous move loop, movement should not visibly stop at every cell, and attack/skill/death events should still interrupt or transition cleanly.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`
- `git diff --check`
