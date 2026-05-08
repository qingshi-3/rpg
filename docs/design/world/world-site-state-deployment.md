# WorldSite State Deployment Authority

This document records the long-term ownership rule for units and deployment inside a `WorldSite`.

## Rule

`WorldSiteState` is the authority for units and deployment positions inside a persistent site.

Resident garrison units, incoming assault armies, Raid forces, and field-intervention forces must be represented as site placement state before battle entities are spawned. `WorldSiteRoot` then projects those placement records into battle nodes.

## BattleStartRequest Boundary

`BattleStartRequest` carries context:

- `TargetSiteId`
- `SourceArmyId`
- `ThreatId`
- `AttackerFactionId`
- `DefenderFactionId`
- `AttackDirection`
- roster-style `PlayerForces` and `EnemyForces`

It must not become the long-term authority for unit coordinates. During the transition, force requests may receive `PreferredPlacements`, but those placements must be copied from `WorldSiteState.UnitPlacements`.

## Deployment Cache Boundary

The site map can produce a session cache of walkable surfaces and direction-sorted deployment candidates. That cache is derived data from authored map resources. The chosen deployment result must be written back to `WorldSiteState.UnitPlacements`.

Long term, this cache should become versioned prepared site data so the strategic world can inspect site deployment before opening the site runtime scene.

## Current Flow

```text
BattleStartRequest context
-> WorldSiteRoot loads site map
-> build session deployment cache from BattleGridMap
-> WorldSiteDeploymentService writes incoming forces to WorldSiteState.UnitPlacements
-> request forces receive placements copied from WorldSiteState
-> BattleUnitFactory instantiates battle entities
```

## Site Management Projection

When a site is not running battle logic, `WorldSiteRoot` still projects
`WorldSiteState.UnitPlacements` into animated unit presentation nodes. The
presentation is not authoritative: dragging a unit updates the matching placement
record, and re-entering the site rebuilds presentation from the state again.

Resolved battle placements must be cleared or converted before site management is
shown. A peacetime or aftermath site should not keep stale `Attacker`,
`Defender`, or `FieldArmy` placement records beside garrison records.

## Battle-End Writeback

Before applying a battle result, the runtime captures surviving battle entities'
grid positions. After world result writeback, those snapshots update matching
garrison placements and can seed newly created owner garrison placements, such as
an assault army that becomes resident after capturing a site. Temporary battle
placements are then removed from the resolved site state.

## Acceptance Checks

- Entering a site from the east uses east-side deployment candidates for incoming attackers.
- Resident garrison positions come from `WorldSiteState.UnitPlacements`.
- Request-side dynamic coordinate guessing is not used as the authoritative deployment path.
- Returning to a site after battle rebuilds animated units from `WorldSiteState.UnitPlacements` without extra default placement markers.
