# Battle Group Tactical Region Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by making non-engaged movement region-directed and engaged combat locally intelligent without turning every unit into an independent global target optimizer.

## Responsibility

This document owns the architecture contract for battlefield tactical areas:

- global combat zones computed from all living units, factions, footprints, perception/contact, attacks, and recent damage;
- group action zones owned by commander groups and exposed as observable movement or tactical intent;
- fixed target regions selected from authored deployment, objective, or semantic marker regions;
- temporary target regions generated from opposing unit clusters when fixed regions no longer contain relevant units;
- battle-group engagement state transitions between region movement, local combat, hold defense, and active assault;
- separation between reusable tactical solvers and side-specific policy.

## Does Not Own

This document does not own:

- movement legality, occupancy, reservations, damage, defeat, settlement, or report truth;
- player command validation or UI command surfaces;
- semantic marker authoring rules;
- global tactical direction for all groups;
- campaign persistence or strategic AI.

## Persistent State

No persistent campaign state is required for V1 tactical areas. Combat-zone choices, group action-zone choices, target-region choices, temporary regions, perception summaries, and engagement state are battle-runtime facts and are discarded after the battle result is emitted.

## Runtime State

Runtime stores global combat-zone state separately from group-owned action state.

Global combat-zone state is battle-owned and not keyed by a commander group. A `CombatZone`-like record stores stable zone id, bounds, center cell, height, version, reason, last built tick, participating actor ids, and the commander-group ids observed inside the cluster.

Each battle group owns a `BattleGroupRuntimeTacticalState`-like record keyed by battle-group id. The exact implementation name may differ, but the owned facts are:

- battle-group id and faction id;
- side policy kind, such as enemy offense, enemy active defense, enemy hold defense, or player-command controlled;
- engagement state, such as region moving, engaged, hold defending, or active assault;
- current fixed target-region reference;
- current temporary target-region reference, if fixed regions have no relevant opposing units;
- current group action-zone reference, such as objective move, combat join, support, hold, retreat, or regroup;
- current selected combat-zone id, when the commander is joining or fighting inside a global combat zone;
- last perception tick, last damage/attack tick, disengage counter, and next region-replan tick.

Global caches may store immutable combat-zone and region snapshots for query and diagnostics. These caches are observation helpers only. They do not own command intent, do not mutate group state, and do not become a second commander.

## Inputs

Inputs include:

- battle-start snapshot groups and initial side policy metadata;
- authored deployment, objective, and semantic marker regions;
- Runtime actor positions, footprints, factions, current phases, damage events, attack events, and perception facts;
- player command or battle-plan facts for player-controlled groups;
- refresh tunables such as temporary-region refresh interval and disengage confirmation ticks.

## Outputs

Outputs include:

- global combat-zone updates;
- battle-group-owned group action-zone updates;
- battle-group-owned target-region updates;
- region movement requests for non-engaged movement;
- local target and slot selection requests for engaged combat;
- low-noise diagnostic events explaining combat-zone builds, group action-zone builds, deployment-zone bounds, region selection, engagement start/end, hold-defense activation, and region unreachable failures.

## Contracts

Combat zones are global battlefield facts. They must not carry an owner battle-group id and must not mutate any group intent directly.

Group action zones are battle-group-owned. A group action zone without an owner battle-group id is not valid decision input.

The owner id is the battle-group commander id, not a presentation actor id, force-count row id, or temporary adapter row id. Multiple runtime actors that belong to the same hero-led company share tactical-region ownership and contribute to the same perception coverage, local-combat region, and engagement state.

Non-engaged movement targets group action zones or fixed/temporary target regions, not moving units. A group may keep moving toward a fixed or temporary region while no relevant combat zone, opposing unit, recent damage, or attack event keeps it engaged.

Engaged behavior targets units only inside a relevant bounded combat zone. The combat zone is built from all living units and clustered contact/perception/attack facts, capped by a performance-safe formula. Overlapping contacts and larger footprints increase the area bounds. The selected zone should cover the current fight and immediate join space without becoming a whole-map optimizer.

The commander group chooses whether its members move toward a combat zone, join it, hold, support, retreat, or regroup. That choice is expressed as a group action zone and member combat assignments. Actors consume the resulting typed intent and do not decide combat-zone participation by themselves.

Enemy policy owns enemy target-region selection:

- offense selects from player defensive deployment regions first;
- active defense selects from player offensive deployment regions first;
- hold defense remains in its held region until any member takes damage, attacks, or perceives a player unit, then the whole group switches to active assault;
- V1 does not require activated defenders to return to their original defensive region.

If fixed target regions contain no relevant opposing units, enemy policy may generate temporary target regions from opposing unit clusters. Temporary regions are cached per battle group and refreshed only at configured replan intervals. The default refresh interval is 5 Runtime ticks.

Player policy is separate. Player groups may reuse perception facts, combat-zone building, local target/slot solving, and Runtime validation, but player target regions and posture are changed only by player commands or accepted player battle plans. Enemy region policy must never rewrite player group intent.

Player-commanded groups may still enter a player-scoped engaged state from perception, damage, attack, combat-zone overlap, or route-blocking facts when the active plan or engagement rule allows local response. That transition authorizes combat-zone participation and slot solving inside the current player command scope; it does not convert the group to enemy offense, replace the selected objective, or override the player plan.

Combat-zone joining must degrade through named roles. If an attack slot is statically reachable but the actor has no executable next step because of current occupancy, footprint, or reservation facts, the group should request a support, queue, line-hold, flank, or regroup role rather than repeatedly reporting generic path failure.

Combat-zone and group action-zone rebuilds must log complete area snapshots at low frequency. A diagnostic snapshot includes all current combat-zone bounds, all authored deployment-zone bounds known to the battle snapshot, all group action-zone bounds, and all living unit anchors, footprints, group ids, factions, high-level states, and zone membership.

## Failure Rules

- Missing owner battle-group id on a group action zone: reject the region and emit a diagnostic.
- Owner battle-group id on a combat zone: reject the combat-zone fact and emit a diagnostic.
- Region unreachable: keep Runtime state explicit, emit a reason, and let the owning policy choose a replacement region at a valid replan boundary.
- No fixed region contains opposing units: build or reuse a temporary region if allowed by refresh timing; otherwise keep the current valid region until replan.
- No relevant combat zone, perceived opposing units, recent attack, or recent damage for the configured disengage period: exit engaged state, clear unit target locks, and replan region movement.
- Player command conflict: player command wins; automatic local combat or region policy degrades to a request inside that command scope.
- Static reachability but blocked dynamic entry: keep the local combat assignment explicit, emit the blocked entry reason, and prefer a named support or queue role before idle hold.

## Acceptance

This architecture is acceptable when:

- combat zones are global facts without group owners;
- every group action zone has one commander-group owner;
- global caches are read/query helpers, not tactical authorities;
- non-engaged movement is region-directed;
- engaged combat is locally optimized inside bounded global combat zones selected by group commanders;
- player-commanded groups can enter player-scoped local combat without enemy policy overwriting player intent;
- enemy offense, active defense, and hold-defense activation are defined without affecting player command authority;
- temporary regions are per-group, cached, and refreshed no more often than the configured interval;
- diagnostics can explain combat-zone builds, group action-zone builds, deployment-zone bounds, unit positions, engagement transitions, and local combat target/slot decisions.
