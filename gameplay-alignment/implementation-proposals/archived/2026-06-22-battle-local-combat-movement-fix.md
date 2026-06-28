# Battle Local Combat Movement Fix

Status: Accepted

## Origin

- Requirement: BATTLE-LOCAL-COMBAT-MOVE-001
- User Report: Enemies do not move after battle starts; player middle-lane units reach the enemy target region but run around region vertices instead of moving toward enemy units.
- Design Proposal: None required. This implements current accepted battle Runtime, AI, tactical-region, and navigation architecture.
- Authority:
  - `system-design/battle-runtime-architecture.md`
  - `system-design/battle-ai-boundary-architecture.md`
  - `system-design/battle-tactical-intent-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`
  - `system-design/battle-group-tactical-region-architecture.md`

## Scope

- Fix Runtime enemy movement when a target and reachable attack slots exist but the local first step is not a strictly improving target step.
- Fix player-commanded groups that overlap an active combat zone so they enter group-owned local combat promptly instead of continuing objective-region steering.
- Fix committed combat-join refresh so a player group already moving to an active fight does not briefly fall back to its fixed objective when allied participants in that fight are defeated.
- Preserve Runtime authority for movement, engagement, combat zones, group action zones, local combat slots, occupancy, and reservations.
- Add low-noise comments or diagnostics only where they explain state transition or failure semantics.

## Non-Goals

- No new battle gameplay identity or engagement rules.
- No Presentation movement, animation, or scene-node authority changes.
- No flow-field or whole-map pathfinding hot path.
- No enemy/player command priority change beyond consuming existing combat-zone and local-combat facts.

## GodotPrompter Skills

- `csharp-godot`
- `ai-navigation`
- `godot-debugging`
- `godot-testing`

## Tests

- Add a regression that an enemy on the right edge with a reachable attack slot starts moving instead of reporting `path_not_found`.
- Add a regression that a player-commanded group overlapping a close hostile combat zone produces a `CombatJoin` group action zone instead of remaining `ObjectiveMove`.
- Add a regression that a previously committed `CombatJoin` remains selected while its combat zone still exists, instead of clearing target state and returning to the old fixed objective during a no-perception refresh window.
- Run the target battle architecture regression suite.

## Acceptance

- Enemies with valid static topology, reachable target, and reachable attack slots produce executable movement or a named local-combat degradation, not generic `path_not_found`.
- Player middle-lane groups entering a hostile combat zone switch to combat-zone/local-combat execution at the next tactical refresh.
- Player groups already committed to a live combat zone do not emit `ObjectiveMove / region_fixed_advance` toward an old objective before the next valid combat target is selected.
- Existing battle architecture regression tests pass.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: pass with pre-existing nullable/Godot source-generator warnings.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: pass with pre-existing nullable/Godot source-generator warnings.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: pass.
