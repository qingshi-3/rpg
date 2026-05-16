# Strategic World V1 Battle Contract

This document defines how the strategic world enters battle and how battle writes results back. It is a legacy `docs/` contract that remains useful for handoff and writeback boundaries, but it does not override the current `gameplay-design/` or future `system-design/` authority.

## Stable Boundary

```text
WorldActionRequest
-> WorldActionResolver
-> WorldBattleRequestBuilder
-> BattleStartRequest
-> BattleSessionHandoff
-> Battle runtime
-> BattleResult
-> WorldBattleResultApplier
-> StrategicWorldState / WorldSiteState changes
```

World owns:

- deciding where conflict happens;
- selecting factions, armies, garrison, entrances, threats, and site state;
- converting facilities, garrison, intel, and world context into battle request data;
- consuming `BattleResult` and mutating strategic/world-site state.

Battle owns:

- resolving local conflict;
- reading map, units, terrain, objectives, abilities, modifiers, and behavior rules;
- producing structured results and force survival counts;
- presenting battle playback and reports.

Battle must not:

- mutate `StrategicWorldState`;
- decide enemy strategic-map policy;
- persist `WorldSiteState`;
- hide world writeback behind scene-node side effects.

World must not:

- directly operate battle scene internals;
- spend battle resources or execute battle actions from UI callbacks;
- choose deployment coordinates outside the site-local placement authority.

## Runtime Direction

The legacy manual turn/AP runtime has been deleted or detached from active scenes. New battle work must not restore AP, player phases, action menus, or the old command controllers.

The accepted player-facing direction is hero-led light RTS:

```text
pre-battle location operation / build / deployment
-> authored battle map
-> hero, corps, and combined commands at medium frequency
-> automatic soldier behavior under player command
-> readable battle report
-> structured result writeback
```

Backend auto-resolve/report code may remain as infrastructure for low-value conflicts or temporary fallback, but pure no-command post-deployment playback is not the product battle identity. The handoff contract does not change when the runtime changes.

## BattleStartRequest

`BattleStartRequest` carries context. It must not become the long-term authority for site-local unit coordinates.

Required fields:

```text
RequestId
ContextId
BattleKind
EncounterId
SourceSiteId?
TargetSiteId?
SourceArmyId?
TargetArmyId?
ThreatId?
AttackerFactionId
DefenderFactionId
AttackDirection
MapDefinitionId
ObjectiveIds[]
AvailableEntrances[]
PlayerForces[]
EnemyForces[]
BattleModifiers[]
SiteStateSnapshot
ReturnScenePath
SiteScenePath
```

Site-local deployment authority is:

```text
WorldSiteState.UnitPlacements
```

Force requests may carry `PreferredPlacements`, but those placements must be copied from `WorldSiteState.UnitPlacements` or from a service that writes there first.

## Battle Kinds

Current V1 kinds:

| Kind | Use |
|---|---|
| `AssaultSite` | Player attacks a hostile site. |
| `DefenseRaid` | Player defends a held site from an enemy raid. |
| `FieldIntercept` | Moving armies meet outside a normal site battle. |

Reserved future kinds:

```text
SearchAndExtract
Rescue
Sabotage
BossAssault
SiteCrisis
```

## Entrances And Deployment

`AvailableEntrances` expresses where a faction may enter or defend from. Player-side non-garrison entrances should respect current site intel.

`WorldSiteState.UnitPlacements` remains authoritative for:

- resident garrison units;
- incoming assault armies;
- visiting infiltration armies;
- raid forces;
- field-intervention forces.

Deployment caches derived from a runtime grid are not authoritative state. They are candidate lists used by services to write placement rows.

## Site State Snapshot

`SiteStateSnapshot` gives battle enough local context to instantiate relevant presentation and rules:

```text
SiteId
ControlState
DamageLevel
ActiveFacilityIds[]
DamagedFacilityIds[]
GarrisonSummary[]
ActiveTags[]
```

It is a snapshot, not persistent state.

## BattleResult

Battle must return structured data:

```text
BattleResult
  RequestId
  ContextId
  BattleKind
  Outcome
  ObjectiveResults[]
  ForceResults[]
  ResourceChanges[]
  SiteStateChanges[]
  FacilityStateChanges[]
  ThreatStateChanges[]
  TagsAdded[]
  TagsRemoved[]
```

`ForceResults` is required for automated battles because the strategic layer needs surviving counts for garrison, assault army transfer, retreat, and losses.

Allowed outcomes:

```text
Victory
Withdraw
Defeat
Disaster
```

## Result Application

`WorldBattleResultApplier.Apply(state, definition, request, result)` owns strategic writeback.

It should:

- validate request/result identity;
- apply objective results by `BattleKind`;
- update site ownership, damage, facilities, threats, and army status;
- update garrison counts from `ForceResults`;
- advance world tick when appropriate;
- emit low-noise `GameEvent` records.

It should not:

- play UI;
- inspect battle scene nodes;
- infer casualties from presentation-only state when `ForceResults` exists.

## Battle Report Requirement

Battles must produce a readable report, even if the report is not fully persisted in V1.

Minimum report facts:

- outcome and objective state;
- surviving and defeated force counts;
- hero contribution;
- corps contribution;
- facility/modifier contribution;
- top failure reason when defeated;
- command contribution where available;
- site/world changes applied.

If the player cannot understand why the battle result happened, the build and command loop is not valid.

## Acceptance

- Strategic world can launch assault, defense, and field-intercept battle requests.
- Site deployment comes from `WorldSiteState.UnitPlacements`.
- Battle returns `BattleResult.ForceResults`.
- World result writeback preserves surviving garrison and army counts.
- Battle does not reference or mutate `StrategicWorldState` directly.
- The retired manual AP runtime stays removed from active architecture.
