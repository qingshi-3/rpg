# Battle Local Steering Navigation Implementation Proposal

Status: Implemented - Automated Verification Passed

## Originating Design Proposal

- `design-proposals/archived/2026-06-10-battle-local-steering-navigation/`

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Scope

- Add Runtime-owned local steering state to movement intent memory.
- Upgrade objective and region movement so static topology blockers can enter obstacle-following instead of stopping after a shallow lookahead fails.
- Keep dynamic living-unit occupancy and same-tick reservation blockers as queue/support/hold cases, not as reasons to wander around formations.
- Use existing objective and region anchors as coarse movement goals. Authored route-hint facts remain an accepted architecture extension, but this slice does not add a new route-hint data channel.
- Preserve current target, attack-slot, support-slot, queue, and combat-zone semantics.

## Non-Goals

- No Runtime flow fields.
- No per-unit whole-map A* path authority.
- No new map resource format or editor workflow in this slice.
- No authored route-hint snapshot, config, or marker ingestion path in this slice.
- No Presentation path prediction or visual-only movement authority.
- No changes to deployment, settlement, hero skills, or command UI.

## Touched Systems

- `BattleRuntimeActor` movement intent memory.
- `BattleRuntimeActorStateMachine` movement commit and intent clearing.
- `BattleCrowdMovementPlanner` objective/region local steering.
- `BattleObjectiveAdvancePlanner` region/objective movement contexts if needed for steering hints.
- Target battle architecture regressions.

## Tests

- Add a regression where a move-first actor must follow a long static wall upward before rejoining objective progress.
- Add a regression that verifies static obstacle steering keeps a consistent side instead of alternating around a blocker.
- Add a regression that verifies the same obstacle-follow intent cannot renew forever after its progress budget is exhausted.
- Keep existing local-neighbor and performance regressions green, especially zero flow-field hot-path expectations.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Runtime diagnostics should keep reporting path failures only when local steering, route hints, and queue/hold degradation cannot provide a legal move.
- Manual QA: multi-company Bonefield battle with top/middle/bottom deployments should show units routing around authored blockers without global stutter or long formation-wandering.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-10. The run includes the long-wall rejoin regression, the dead-wall budget regression, retained local-target greedy movement, and performance checks reporting `flowBuilds=0 flowHits=0 flowMisses=0`.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed on 2026-06-10.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed on 2026-06-10 with 0 warnings and 0 errors.
