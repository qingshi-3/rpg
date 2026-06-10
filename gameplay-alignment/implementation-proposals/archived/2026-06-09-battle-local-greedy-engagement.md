# Battle Local Greedy Engagement Implementation Proposal

Status: Archived - Accepted Implementation Record

## Authority

- Implements `system-design/battle-runtime-architecture.md` for actor state-machine decision boundaries and Runtime validation.
- Implements `system-design/battle-navigation-topology-architecture.md` for region-directed movement and avoiding per-unit global attack-position search.
- Implements `system-design/battle-ai-boundary-architecture.md` for behavior-tree intent output and low-frequency local combat decisions.

## Scope

- Add a first-slice local engagement movement path for ordinary corps.
- When a corps has a selected local target outside attack range, try nearby legal neighbor cells that reduce footprint-aware distance to that target.
- Keep movement legality, occupancy, reservations, action locks, and event emission in Runtime validators.
- Prefer this local greedy step before target-specific flow-field construction for ordinary target pursuit.
- Keep support-slot and explicit combat-slot movement on the existing slot planner so crowded-front behavior stays stable.

## Non-Goals

- No multithreading.
- No new player commands, UI, deployment behavior, settlement logic, or skill rules.
- No global formation or group path constraints.
- No authored LimboAI scene changes.
- No removal of region/objective flow fields used for battle-group movement.

## Touched Systems

- Battle Runtime actor decision context building.
- Battle Runtime movement continuation for target pursuit.
- Battle Navigation local movement planner.
- Target battle architecture regression tests.

## Tests

- Add a regression that local target pursuit can choose a legal neighboring step without building a target/open-attack flow field.
- Reuse the existing performance regression to confirm ordinary nearby engagement reduces target and slot field churn.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Reuse existing movement, reservation, and runtime spike diagnostics.
- Manual QA: deploy multiple companies into upper/middle/lower objectives, let side groups join a nearby fight, and confirm ordinary corps move toward sensed local enemies without visible runtime spikes or excessive lateral wandering.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed first on 2026-06-09 with `flowBuilds=1` for the new retained-local-target regression.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-09 after adding local greedy target pursuit.
- Manual log check on 2026-06-09 after the optimization showed only one latest-run `BattleRuntimeSpike` at battle `tick=0`; the previous mid-battle target/open-attack flow-field spike pattern did not reappear in that run segment.
