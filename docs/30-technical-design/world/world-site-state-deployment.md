# WorldSite State Deployment Authority

This document records the long-term ownership rule for units and deployment inside a `WorldSite`.

## Rule

`WorldSiteState` is the authority for units and deployment positions inside a persistent site.

Resident garrison units, incoming assault armies, visiting infiltration armies, Raid forces, and field-intervention forces must be represented as site placement state before battle entities are spawned. `WorldSiteRoot` then projects those placement records into battle nodes.

An army that has entered a site is no longer only a strategic-map object for site-local logic. The army remains in `StrategicWorldState.ArmyStates` for world identity and roster ownership, but the site-local row is `WorldSiteState.UnitPlacements` with `SourceKind = PlayerArmy`, `ArmyId`, and `PlacementKind = VisitingArmy` or `Attacker`. Exploration, management presentation, battle deployment, and result writeback must resolve the site-local unit through this placement row instead of scene fields or request-side coordinate guesses.

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

`WorldSiteRoot` and other presentation code must not delete
`WorldSiteState.UnitPlacements` during UI refresh, mode switching, battle handoff,
or rollback. Unit placement rows may only be removed or converted by explicit
domain events such as death, retreat, transfer into garrison, army disband, or
scripted departure. `VisitingArmy` placements are site-local army association
rows and must survive non-battle refreshes until one of those explicit events
resolves them.

## Battle-End Writeback

Before applying a battle result, the runtime captures surviving battle entities'
grid positions and force survival counts. `BattleResult.ForceResults` is the
unit-count writeback contract: world systems should use it to remove defeated
site garrison units and transfer only surviving army units into a captured
site. If an older result has no force results, the legacy full-force fallback is
allowed only for compatibility.

After world result writeback, live placement snapshots update matching
placements and can seed newly created owner garrison placements, such as an
assault army that becomes resident after capturing a site. Cleanup must not
bulk-delete placement rows by source or placement kind; it must be expressed as
explicit domain conversion/removal.

Exploration encounter cleanup must use explicit site-local identity. A patrol
with `SourcePlacementId` may only remove that exact placement. If the source
placement is missing, the result path should log and skip removal instead of
falling back to "first unit of the same type".

## Runtime State Boundary

World-site state should stay resident in memory while the strategic run is
active. This includes mutable garrison counts, facilities, control state,
pending threats, and placement state. These values are save-serializable
authoritative state, not disposable UI cache and not data that should be
reloaded from authored definitions after every scene transition.

Use `WorldGarrisonMutationService` for garrison count changes in application
services. Presentation code may project or inspect garrison state, but should
not become a second owner for count mutation rules.

## Acceptance Checks

- Entering a site from the east uses east-side deployment candidates for incoming attackers.
- Resident garrison positions come from `WorldSiteState.UnitPlacements`.
- Visiting infiltration army positions come from `WorldSiteState.UnitPlacements`.
- Request-side dynamic coordinate guessing is not used as the authoritative deployment path.
- UI refresh, battle startup, battle rollback, and generic deployment cleanup do not remove unit placement rows.
- Returning to a site after battle rebuilds animated units from `WorldSiteState.UnitPlacements` without extra default placement markers.
- Battle result application preserves surviving garrison and assault-army unit counts from `BattleResult.ForceResults`.
