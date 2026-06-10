# Battle Player Autonomous Target Regions Implementation Proposal

Status: Implemented - Automated Verification Passed

## Originating Design Proposal

- `design-proposals/archived/2026-06-10-battle-player-autonomous-target-regions/`

## Authority

- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

## Scope

- Add explicit selected-region command source state for player command, self-calculated fallback, and enemy policy.
- Preserve current execution as actor/group state-machine output rather than long-term command truth.
- Allow player-commanded groups to compute a self-calculated temporary target only after the player command is absent or completed.
- Use the existing opposing-cluster temporary-region builder and existing route/region movement execution path.
- Clear self-calculated temporary targets on engagement entry, completion, or player-command override.
- Clear player-command regions only when completed with no relevant opposing actor in the region.

## Non-Goals

- No new player command UI.
- No campaign persistence changes.
- No flow-field restoration.
- No per-actor whole-map A* hot-path search.
- No change to damage, settlement, skill, deployment, or Presentation animation authority.

## Touched Systems

- Battle group tactical state and mutation store.
- Tactical observation refresh at tick start.
- Region movement-goal resolution.
- Target battle architecture regression tests.
- Low-noise Runtime tactical diagnostics through existing event stream.

## Implementation Direction

1. Add a command-source value to battle-group tactical state and clone it through snapshots.
2. Keep existing enemy-policy region mutation behavior, including rejection when it tries to overwrite player command.
3. Add a separate self-calculated temporary-region mutation path for player-commanded groups.
4. Resolve region movement goals for any selected fixed/temporary region that is valid for its command source, not only enemy policy.
5. At tactical observation refresh, clear completed player-command and self-calculated regions when the group has reached the region and no opposing actor remains there.
6. At engagement entry, clear only self-calculated regions; keep player-command regions stored.
7. When a player-commanded group is not engaged, has no player command, and has autonomous fallback enabled, build or refresh a self-calculated temporary region from opposing clusters.

## Tests

- Add a regression proving an active player command is not overwritten by autonomous fallback before completion.
- Add a regression proving a completed empty player target clears, then selects a self-calculated temporary target toward the densest opposing cluster.
- Add a regression proving a self-calculated target is cleared when the group enters combat.
- Keep existing enemy temporary-region tests passing.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Event reasons should distinguish enemy temporary target selection from player self-calculated selection.
- Manual QA: deploy multiple player groups to top/middle/bottom initial regions. Groups that finish an empty objective should pick a sensible next enemy cluster and move through route hints; groups in combat should show combat-local behavior and not keep stale autonomous movement.

## Acceptance Evidence

- 2026-06-10 RED: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed before implementation because `BattleGroupTacticalReasonCode.PlayerAutonomousTemporaryRegionCreatedCluster` and the player autonomous temporary target behavior did not exist.
- 2026-06-10 GREEN: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. New coverage proves active player commands block autonomous fallback, completed empty player commands create self-calculated temporary targets toward the denser hostile cluster, and self-calculated targets clear on engagement.
- 2026-06-10: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-10: `git diff --check` reported no whitespace errors; it only warned that `system-design/battle-group-tactical-region-architecture.md` will normalize from CRLF to LF when Git touches it.
