# Enemy Source Garrison Production

## Background

World threats already spawned visible `WorldArmyState` units, but the source site was not the authority for those forces. The user clarified the intended direction: threats must come from enemy troops, and enemy source sites should auto-produce those troops.

## Goal

- Make enemy source sites the authoritative origin of raid forces.
- Let hostile sites auto-produce reserve garrison over world time.
- Require raid dispatch to consume a real force package from the source site before creating a threat army.

## Non-Goals

- Do not add a full enemy economy, upkeep, or construction loop.
- Do not change Battle flow, AP, `TurnSystem`, or world-battle projection rules.
- Do not introduce hidden fallback spawns as a parallel authority.

## Affected Systems

- World definition data: `WorldSiteDefinition`, new `SiteAutoGarrisonProductionDefinition`.
- Content setup: `StrategicWorldV1DefinitionFactory` graveyard source-site reserve and reinforcement batch.
- World progression: `WorldTickService` production, threat dispatch, and force consumption.

## Rules

- `WorldSiteDefinition.AutoGarrisonProductions[]` defines periodic reserve growth for a site.
- `ThreatRuleDefinition.EnemyForces` remains the dispatch package for a threat, but dispatch now requires the source site to already hold that package in `WorldSiteState.Garrison`.
- `WorldTickService` first applies player production, then hostile auto-production, then threat generation.
- Threat creation consumes the required units from the source-site garrison and builds the marching `WorldArmyState` from the consumed package.

## Risks

- If a source site has no valid production config or cannot accumulate the required package, that threat rule now correctly stops firing.
- Source sites without usable deployment zones still store reserve garrison logically; tactical presentation for those sites remains out of scope for this change.

## Rollback Plan

- Remove `AutoGarrisonProductions` content from the affected site definition.
- Restore the old direct `ThreatRuleDefinition -> WorldArmyState` spawn path if this reserve model proves too restrictive.

## Documentation Updates

- This note records the authority shift immediately.
- Stable world data-model and content-authoring docs can absorb `AutoGarrisonProductions[]` after the current documentation reorganization settles.

## Manual Acceptance Checks

- Build the solution and confirm the new world definition field compiles.
- Advance world ticks with the graveyard hostile and verify it emits reserve garrison before or between raids.
- Capture the bonefield and confirm a raid only launches when the graveyard has the required five-unit package.
- Inspect the graveyard after a raid dispatch and confirm its reserve garrison was reduced by the dispatched package.
