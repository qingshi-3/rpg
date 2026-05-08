# WorldSite State Driven Deployment

## Background And Goal

`WorldSite` is persistent world state, not a one-off battle scene. A battle should be loaded as:

```text
WorldSiteState + runtime site map cache + BattleStartRequest context -> battle entities
```

The previous path let `BattleStartRequest` and `WorldSiteRoot` choose deployment cells at battle load time. That made the request act like the position authority and caused side-specific entry, such as entering Bonefield from the east, to behave inconsistently.

## Implemented Direction

- `WorldSiteUnitPlacement` now records faction, placement kind, source, army, threat, entrance, direction, cell, and height.
- `WorldSiteDeploymentService` owns garrison and battle placement writes into `WorldSiteState.UnitPlacements`.
- `WorldSiteRoot` builds a session deployment cache from the loaded site grid once per map load.
- Before spawning battle entities, `WorldSiteRoot` writes non-resident forces from the request into `WorldSiteState.UnitPlacements`.
- Battle entities are instantiated from placement records copied back from `WorldSiteState`; request-side dynamic deployment is no longer the authority.
- Battle-end site management now rebuilds animated unit presentation from `WorldSiteState.UnitPlacements` instead of keeping battle entities plus separate circular placement markers.
- Surviving unit positions are captured before world result writeback, then applied to matching garrison placements or newly converted owner garrisons before temporary battle placements are cleared.

## Non-Goals

- This change does not modify Battle flow, AP, TurnSystem, action execution, or turn order.
- This change does not serialize the derived grid cache. The cache is rebuilt from the authored site map during the current session.
- This change does not add detailed casualty lists to `BattleResult`; survivor position capture is still runtime-side and scoped to the resolved site.

## Affected Files

- `src/Domain/World/WorldSiteUnitPlacement.cs`
- `src/Domain/World/WorldSiteUnitPlacementKind.cs`
- `src/Application/World/WorldSiteDeploymentCell.cs`
- `src/Application/World/WorldSiteDeploymentService.cs`
- `src/Application/World/WorldBattleRequestBuilder.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`

## Risks And Follow-Up

- The deployment cache currently belongs to the loaded `WorldSiteRoot` session. Long term, map import or site preparation should expose a versioned cache so strategic-world code can inspect deployment points before opening the site scene.
- `BattleStartRequest.PlayerForces` and `EnemyForces` remain as roster context during the transition. They should not regain position authority.
- More complex contested-site states may need rules for keeping non-garrison placements during an unresolved `Wartime` site.

## Verification

- `dotnet build .\rpg.sln` passes.
