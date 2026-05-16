# World Battle Contract And Writeback

## Purpose

This document details how the auto battle runtime must preserve the existing world/battle contract while replacing the manual battle executor.

Authoritative contract: `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`.

## Stable Flow

```text
Strategic world action
-> WorldBattleRequestBuilder
-> BattleStartRequest
-> site deployment preparation from WorldSiteState.UnitPlacements
-> AutoBattleRuntimeController
-> BattleResult
-> WorldBattleResultApplier
-> StrategicWorldState / WorldSiteState mutation
```

## Request Rules

`BattleStartRequest` carries context, not persistent authority.

It should include:

- battle kind;
- source and target context;
- attacker and defender factions;
- attack direction;
- map definition;
- available entrances;
- player and enemy forces;
- battle modifiers;
- site snapshot;
- return scene path and site scene path.

Preferred placements may exist on force requests, but they must come from `WorldSiteState.UnitPlacements` or from a service that writes there first.

## Placement Rules

`WorldSiteState.UnitPlacements` remains authoritative for:

- resident garrison;
- incoming assault forces;
- defense raid forces;
- exploration-triggered forces;
- field-intervention forces when represented on a site grid.

Runtime grid caches can only help choose or validate placements. They must not become persistent state.

## Result Rules

`BattleResult` must include:

- `RequestId`
- `ContextId`
- `BattleKind`
- `Outcome`
- `ObjectiveResults`
- `ForceResults`
- resource, site, facility, threat, and tag changes when relevant

`ForceResults` is required for auto battle because the world layer needs survivor counts.

## Writeback Rules

`WorldBattleResultApplier` owns writeback.

It should:

- validate request/result identity;
- apply battle-kind-specific consequences;
- update site ownership, damage, facilities, threats, armies, and garrison;
- use `ForceResults` for survivors and losses;
- emit low-noise `GameEvent` records.

It must not:

- inspect battle scene nodes;
- infer casualties from visual state when `ForceResults` exists;
- apply presentation-only events as persistent state;
- hide missing `ForceResults` behind guessed survivor counts for auto battles.

## Migration Checks

Each auto battle slice should check:

- request identity is preserved;
- placement source is `WorldSiteState.UnitPlacements`;
- `ForceResults` covers player and enemy forces;
- world writeback preserves surviving garrison and assault/raid counts;
- no auto battle class references or mutates `StrategicWorldState` directly.

## Acceptance

- Assault, defense raid, and field-intercept requests remain launchable.
- Auto battle returns structured results for the applier.
- World state changes remain centralized in `WorldBattleResultApplier`.
- Missing or invalid contract data fails loudly with a clear log.
