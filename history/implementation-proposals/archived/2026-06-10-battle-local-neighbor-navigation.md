# Battle Local Neighbor Navigation Implementation Proposal

Status: Implemented - Automated Verification Passed

## Originating Design Proposal

- `design-proposals/archived/2026-06-10-battle-local-neighbor-navigation/`

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Scope

- Remove Runtime flow-field construction from first-slice battle combat hot paths.
- Replace target, combat-slot, support-slot, and objective movement next-step selection with bounded local neighbor scoring.
- Replace reachable attack-slot checks with target-local slot enumeration and executable local-step checks.
- Keep attack slot, support slot, queue, hold pressure, and combat-zone semantics as state-machine behavior.
- Keep low-noise diagnostics and performance counters useful after flow-field counters stop moving.

## Non-Goals

- No new player commands, UI, deployment behavior, settlement behavior, or content definitions.
- No global A* replacement and no new flow-field-like global route cache.
- No attempt to solve maze-quality pathfinding for the first slice.
- No Presentation prediction or movement authority changes.

## Touched Systems

- Battle Runtime target selection.
- Battle Runtime movement continuation.
- Battle objective and region advance planning.
- Battle navigation local movement planner.
- Battle combat-slot intent resolution.
- Runtime performance and navigation regression tests.
- Focused system-design authority already updated through the originating design proposal.

## Tests

- Update performance regressions so first-slice Runtime hot paths expect zero flow-field builds in many-vs-many and combat-zone join scenarios.
- Update source-shape regressions so local-combat position selection requires local neighbor resolution instead of shared goal fields.
- Keep large-topology combat-slot scans bounded near the target.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Runtime spike diagnostics should continue to report flow-field counters for backward log readability, but expected first-slice hot-path values should remain zero.
- Manual QA: run a multi-company battle with separated objective regions. Units should advance, locally join fights, queue or hold when blocked, and avoid visible back-and-forth caused by repeated field rebuilds.

## Acceptance Evidence

- 2026-06-10: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Performance evidence included `flowBuilds=0`, `flowHits=0`, `flowMisses=0`, and first-slice local-combat regressions passed without goal-field/pathfinder files.
- 2026-06-10: Added and passed `runtime objective movement uses bounded local obstacle avoidance`, covering an objective-move unit that must take a short non-improving first step around authored topology before it can keep closing distance.
- 2026-06-10: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed with the existing Godot source-generator warning outside the runtime navigation change.
- 2026-06-10: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors.
- 2026-06-10: Source check confirms first-slice Runtime no longer keeps `BattleFlowField*` or `BattlePathfinder` implementation files. Flow-field counters remain only as backward-compatible diagnostics and are expected to stay zero on these hot paths.
