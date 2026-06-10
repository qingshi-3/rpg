# Battle Hierarchical Route Hints Implementation Proposal

Status: Implemented - Automated Verification Passed

## Originating Design Proposal

- `design-proposals/archived/2026-06-10-battle-hierarchical-route-hints/`

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Scope

- Add immutable static route topology derived from `BattleNavigationTopology`.
- Split battle topology into route regions, chunks, or sectors and expose portal-style route anchors.
- Record route clearance by supported footprint profile so large actors are not routed through narrow-only corridors.
- Add low-frequency group-scoped route-hint queries for objective, region, and group-action-zone movement.
- Feed the current route hint into local neighbor movement so actors still commit only validated neighboring cells.
- Simplify or demote local steering so long river/wall solving is handled by route hints, not deeper local BFS or obstacle-follow loops.
- Keep local combat slot, support slot, queue, and pressure behavior local to combat-zone execution.
- Add diagnostics that distinguish static route-hint failure, local movement failure, and dynamic occupancy/reservation congestion.

## Non-Goals

- No Runtime flow-field restoration.
- No per-actor whole-map A* in movement-decision hot paths.
- No Presentation path prediction or visual-only movement authority.
- No dynamic living-unit blockers inside the static route graph.
- No local-combat attack/support slot global routing in this slice.
- No new editor authoring workflow is required for the first implementation; route topology may be derived from compiled navigation topology.
- No deployment, settlement, skill, battle UI, or world-expedition behavior changes.

## Cleanup Before New Code

- Keep `BattleNavigationTopology`, footprint legality, dynamic occupancy, same-tick reservations, actor state-machine action locks, and Runtime movement validation as authority.
- Keep local neighbor ranking as the final movement executor, but make it consume a selected route anchor instead of pretending the final objective is always locally reachable.
- Keep `BattleCombatSlotIntentResolver`, support slots, queue, and combat pressure behavior for local combat only.
- Demote long-distance `ObstacleFollow` and shallow local avoidance from map-scale routing to short-range steering and recovery.
- Remove or rewrite tests and comments that imply the first slice may never have a static global route-hint layer. Preserve the real invariant: no Runtime flow fields and no per-actor whole-map hot-path search.
- Do not revive deleted `BattleFlowField*` or `BattlePathfinder` implementation files.

## Touched Systems

- Battle navigation route topology compilation.
- Battle Runtime group route intent state.
- Battle objective and region advance planning.
- Battle local movement planner.
- Battle movement intent invalidation and diagnostics.
- Target battle architecture and performance regression tests.

## Implementation Direction

1. Add route topology value types for route region id, portal id, route profile, route anchor, route edge, and route query result.
2. Build `BattleRouteTopology` from the compiled navigation graph once per battle topology version.
3. Start with deterministic fixed-size chunking or equivalent sectoring over static topology, then identify passable portal runs between neighboring chunks.
4. Compute portal clearance from footprint placement legality, not from anchor-cell walkability alone.
5. Add a route planner that searches the compact portal graph for a group profile and returns a short corridor of route anchors.
6. Store the selected route hint on commander-owned movement state or a group route-intent cache keyed by group id, objective/region id, profile, topology version, and action-zone revision.
7. Let objective and region movement select the next route anchor before calling the local movement planner.
8. Keep actor-local movement validation unchanged: topology, footprint, occupancy, reservations, backtrack guards, action locks, and command facts still win.
9. Reduce local steering reliance for map-scale barriers after route hints are available, keeping it only for short local obstacle handling and recovery.

## Tests

- Add a Bonefield-style static barrier regression where topology reports the target reachable but pure local greedy/steering cannot cross a river-like obstacle; route hints should select a portal route and produce an executable first step.
- Add a group-shared route regression where multiple actors from the same battle group reuse the same route hint instead of running independent whole-map searches.
- Add a footprint-clearance regression where a `2x2` group is not routed through a `1x1`-only portal, while `1x1` units may use that portal.
- Add an invalidation regression for command/objective/region/profile/topology-version changes clearing stale route hints.
- Keep performance regressions expecting `FlowFieldBuildCount`, `OpenAttackFlowFieldBuildCount`, and deleted `BattleFlowField*` / `BattlePathfinder` source files at zero or absent.
- Keep local-combat regressions proving attack/support slot selection remains target-local and does not start global route planning.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Route topology diagnostics should report region count, portal count, profile coverage, disconnected route regions, and route-query failure reasons.
- Runtime route diagnostics should identify group id, route profile, source region, target region, selected portal or anchor, invalidation reason, and whether failure came from static route absence or local dynamic blockers.
- Existing movement failure diagnostics should keep using named reasons rather than generic `path_not_found` when route topology exists but dynamic congestion blocks execution.
- Manual QA: Bonefield multi-company battle with top/middle/bottom deployments should show units routing around river/water barriers through sensible passages, then switching into local combat without back-and-forth route jitter.

## Acceptance Evidence

- 2026-06-10 RED: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed before implementation because route topology/cache files did not exist and the long static-barrier movement had no group route hint.
- 2026-06-10 GREEN: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. New coverage includes group route hints moving away from the final objective toward the real passage, route topology source shape for footprint clearance, group/region route-hint cache ownership, and no `BattleFlowField*` / `BattlePathfinder` restoration.
- 2026-06-10 RED/GREEN follow-up: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` first failed on `route topology scores portal corridor travel cost`, then passed after route search began scoring source-to-portal, portal-to-portal, and portal-to-target travel inside each route region. This keeps hints corridor-cost based instead of selecting the nearest cross-region edge after a source-region boundary change.
- 2026-06-10: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed.
- 2026-06-10: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
