# Battle Group Tactical Region Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by making non-engaged movement beacon-directed or region-directed, and engaged combat locally intelligent, without turning every unit into an independent global target optimizer.

## Responsibility

This document owns the architecture contract for battlefield tactical areas:

- global combat zones computed from all living units, factions, footprints, perception/contact, attacks, and recent damage;
- group action zones owned by commander groups and exposed as observable movement or tactical intent;
- selected target objects resolved from tactical intent produced by group command, destination beacon command, enemy configuration, or fallback;
- destination beacons selected from accepted player runtime commands;
- fixed target regions selected from authored deployment, objective, or semantic marker regions when a scenario or future planning mode uses them;
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

No persistent campaign state is required for V1 tactical areas. Combat-zone choices, group action-zone choices, destination beacons, target-region choices, temporary regions, perception summaries, and engagement state are battle-runtime facts and are discarded after the battle result is emitted.

## Runtime State

Runtime stores global combat-zone state separately from group-owned action state.

Global combat-zone state is battle-owned and not keyed by a commander group. A `CombatZone`-like record stores stable zone id, bounds, center cell, height, version, reason, last built tick, participating actor ids, and the commander-group ids observed inside the cluster.

Each battle group owns a `BattleGroupRuntimeTacticalState`-like record keyed by battle-group id. The exact implementation name may differ, but the owned facts are:

- battle-group id and faction id;
- side policy kind, such as enemy offense, enemy active defense, enemy hold defense, or player-command controlled;
- active tactical intent plan and resolved target object id for the battle group, when present;
- engagement state, such as region moving, engaged, hold defending, or active assault;
- command-source state for the selected beacon, target object, or region, distinguishing player command, self-calculated fallback, and enemy intent;
- current destination beacon reference and beacon flow-field profile, when player command uses one;
- current fixed target-region reference;
- current temporary target-region reference, if fixed regions have no relevant opposing units;
- current group action-zone reference, such as beacon move, combat join, support, hold, retreat, or regroup;
- current selected combat-zone id, when the commander is joining or fighting inside a global combat zone;
- last perception tick, last damage/attack tick, disengage counter, and next region-replan tick.

Global caches may store immutable combat-zone and region snapshots for query and diagnostics. These caches are observation helpers only. They do not own command intent, do not mutate group state, and do not become a second commander.

## Inputs

Inputs include:

- battle-start snapshot groups and initial side policy metadata;
- authored deployment, optional objective, and semantic marker regions;
- accepted destination beacon command facts;
- Runtime actor positions, footprints, factions, current phases, damage events, attack events, and perception facts;
- tactical intent plans produced by player commands, destination beacon commands, enemy configuration, or scenario defaults;
- refresh tunables such as temporary-region refresh interval and disengage confirmation ticks.

## Outputs

Outputs include:

- global combat-zone updates;
- battle-group-owned group action-zone updates;
- battle-group-owned destination-beacon and target-region updates;
- beacon or region movement requests for non-engaged movement;
- local target and slot selection requests for engaged combat;
- low-noise diagnostic events explaining combat-zone builds, group action-zone builds, deployment-zone bounds, region selection, engagement start/end, hold-defense activation, and region unreachable failures.

## Contracts

Combat zones are global battlefield facts. They must not carry an owner battle-group id and must not mutate any group intent directly.

Group action zones are battle-group-owned. A group action zone without an owner battle-group id is not valid decision input.

The owner id is the battle-group commander id, not a presentation actor id, force-count row id, or temporary adapter row id. Multiple runtime actors that belong to the same battle group share tactical-region ownership and contribute to the same perception coverage, local-combat region, and engagement state.

Non-engaged movement targets destination beacons, group action zones, selected target objects, or fixed/temporary target regions, not moving units. A group may keep moving toward a stable destination beacon, target object, or region while no relevant combat zone, opposing unit, recent damage, or attack event keeps it engaged.

Engaged behavior targets units only inside a relevant bounded combat zone. The combat zone is built from all living units, clustered contact/perception/attack facts, participant footprints, and configured hot-area padding. Overlapping contacts and larger footprints increase the area bounds. The selected zone must cover the current fight and immediate join space without becoming a whole-map optimizer; performance budgets belong to zone splitting and local slot/search evaluation, not to clipping member footprint facts.

The commander group chooses whether its members move toward a combat zone, join it, hold, support, retreat, or regroup. That choice is expressed as a group action zone and member combat assignments. Actors consume the resulting typed intent and do not decide combat-zone participation by themselves.

Tactical intent owns target-object selection inside source policy:

- encounter configuration may provide explicit enemy group intent;
- enemy group or archetype definitions may provide default intent;
- battle scenario defaults provide fallback intent and battlefield target objects;
- safe fallback intent is used when no configured intent resolves.
- accepted player destination beacon commands and other runtime commands may provide player-sourced intent;
- player-sourced intent outranks player-scoped autonomous fallback and enemy policy.

Scenario does not equal intent. A siege-defense scenario may default to a defense intent, but a group in that same battle may sally out, harass, protect, retreat, or assault if its explicit intent plan says so.

Temporary regions generated from opposing unit clusters are volatile tactical observations. They may be cached per battle group and refreshed only at configured replan intervals, but they must not replace a stable non-engaged movement target unless the active intent explicitly allows cluster pursuit or fallback policy selects them. The default enemy movement target should be a stable target object such as an authored region, map feature, defensive line, deployment region, or scenario-provided objective.

Player policy is separate at the source boundary, not at the movement algorithm boundary. Player groups reuse perception facts, combat-zone building, local target/slot solving, target object catalogs, selected-beacon and selected-region storage, group action zones, and Runtime validation. Player destination beacons, target regions, and posture are changed only by player-sourced tactical intent from accepted player commands. Enemy intent must never rewrite player group intent.

Player-commanded groups may still enter a player-scoped engaged state from perception, damage, attack, combat-zone overlap, or route-blocking facts when the active tactical intent or default attack posture allows local response. That transition authorizes combat-zone participation and slot solving inside the current player command scope; it does not convert the group to enemy offense, replace the selected beacon, or override player-sourced intent.

Player-commanded groups also support a self-calculated fallback target when no player command is active and autonomous fallback is allowed. The fallback selects a temporary target region from opposing clusters, with enemy count as the primary score, total hit points as the next tie-breaker, and distance as a later tie-breaker. This is not an enemy-policy mutation and does not create persistent campaign intent.

The tactical state distinguishes three layers:

```text
current execution command
-> player command
-> self-calculated command
```

The current execution command is the state-machine action being consumed now, such as beacon advance, region advance, combat join, attack, support, hold, cast, or return. The player command is highest priority and is cleared only when its beacon or objective is completed, superseded, rejected, or invalidated. The self-calculated command exists only while there is no player command; it is cleared on combat entry, objective completion, or player-command override.

Combat-zone joining must degrade through named roles. If an attack slot is statically reachable but the actor has no executable next step because of current occupancy, footprint, or reservation facts, the group should request a support, queue, line-hold, flank, or regroup role rather than repeatedly reporting generic path failure.

Combat-zone and group action-zone rebuilds must log complete area snapshots at low frequency. A diagnostic snapshot includes all current combat-zone bounds, all authored deployment-zone bounds known to the battle snapshot, all group action-zone bounds, and all living unit anchors, footprints, group ids, factions, high-level states, and zone membership.

## Failure Rules

- Missing owner battle-group id on a group action zone: reject the region and emit a diagnostic.
- Owner battle-group id on a combat zone: reject the combat-zone fact and emit a diagnostic.
- Region unreachable: keep Runtime state explicit, emit a reason, and let the owning policy choose a replacement region at a valid replan boundary.
- No stable enemy intent target resolves: keep the retained valid target if allowed; otherwise run fallback intent.
- No fixed region contains opposing units: do not automatically chase a volatile cluster. Build or reuse a temporary region only if active intent or fallback policy explicitly allows it and refresh timing permits it.
- No relevant combat zone, perceived opposing units, recent attack, or recent damage for the configured disengage period: exit engaged state, clear unit target locks, and replan beacon or region movement.
- Player command conflict: player-sourced tactical intent wins; automatic local combat or enemy intent degrades to a request inside that command scope.
- Player-commanded group with no active player command and no relevant local threat: if autonomous fallback is allowed, build or reuse a self-calculated temporary region from opposing clusters.
- Self-calculated target enters combat or reaches an empty objective: clear the self-calculated target before the next fallback selection.
- Static reachability but blocked dynamic entry: keep the local combat assignment explicit, emit the blocked entry reason, and prefer a named support or queue role before idle hold.

## Acceptance

This architecture is acceptable when:

- combat zones are global facts without group owners;
- every group action zone has one commander-group owner;
- global caches are read/query helpers, not tactical authorities;
- non-engaged movement is beacon-directed or region-directed;
- engaged combat is locally optimized inside bounded global combat zones selected by group commanders;
- player-commanded groups can enter player-scoped local combat without enemy tactical intent overwriting player intent;
- player-commanded groups consume accepted destination beacon commands through the same tactical intent and movement path as enemy groups;
- player-commanded groups can generate self-calculated temporary regions only when no player command is active;
- self-calculated targets clear on combat entry, completion, or player-command override;
- enemy intent selection is driven by configurable intent plans, with scenario defaults only as fallback;
- temporary regions are per-group, cached, refreshed no more often than the configured interval, and unable to override stable intent targets unless authorized by intent;
- diagnostics can explain combat-zone builds, group action-zone builds, deployment-zone bounds, unit positions, engagement transitions, and local combat target/slot decisions.
