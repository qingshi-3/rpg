# Battle Group Tactical Region Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by making non-engaged movement region-directed and engaged combat locally intelligent without turning every unit into an independent global target optimizer.

## Responsibility

This document owns the architecture contract for battle-group-owned tactical regions:

- fixed target regions selected from authored deployment, objective, or semantic marker regions;
- temporary target regions generated from opposing unit clusters when fixed regions no longer contain relevant units;
- local combat regions built from a battle group's own perception coverage while engaged;
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

No persistent campaign state is required for V1 tactical regions. Target-region choices, temporary regions, local combat regions, perception summaries, and engagement state are battle-runtime facts and are discarded after the battle result is emitted.

## Runtime State

Each battle group owns a `BattleGroupRuntimeTacticalState`-like record keyed by battle-group id. The exact implementation name may differ, but the owned facts are:

- battle-group id and faction id;
- side policy kind, such as enemy offense, enemy active defense, enemy hold defense, or player-command controlled;
- engagement state, such as region moving, engaged, hold defending, or active assault;
- current fixed target-region reference;
- current temporary target-region reference, if fixed regions have no relevant opposing units;
- current local combat-region reference, when engaged;
- last perception tick, last damage/attack tick, disengage counter, and next region-replan tick.

Global caches may store `BattleRegionSnapshot` values by battle-group id. These caches are query and diagnostic helpers only. They do not own intent, do not mutate group state, and do not create shared mutable global regions.

## Inputs

Inputs include:

- battle-start snapshot groups and initial side policy metadata;
- authored deployment, objective, and semantic marker regions;
- Runtime actor positions, footprints, factions, current phases, damage events, attack events, and perception facts;
- player command or battle-plan facts for player-controlled groups;
- refresh tunables such as temporary-region refresh interval and disengage confirmation ticks.

## Outputs

Outputs include:

- battle-group-owned target-region updates;
- battle-group-owned local combat-region snapshots;
- region movement requests for non-engaged movement;
- local target and slot selection requests for engaged combat;
- low-noise diagnostic events explaining region selection, temporary-region builds, engagement start/end, hold-defense activation, and region unreachable failures.

## Contracts

Tactical regions are battle-group-owned. A region without an owner battle-group id is not valid decision input.

The owner id is the battle-group commander id, not a presentation actor id, force-count row id, or temporary adapter row id. Multiple runtime actors that belong to the same hero-led company share tactical-region ownership and contribute to the same perception coverage, local-combat region, and engagement state.

Non-engaged movement targets regions, not moving units. A group may keep moving toward a fixed or temporary region while no opposing unit is perceived by any member and no recent damage or attack event keeps it engaged.

Engaged behavior targets units only inside a bounded local combat region. The local region is built from the battle group's member perception coverage, capped by a performance-safe maximum. Overlapping perception cells carry higher weight; for example, a cell perceived by two group members counts more than a cell perceived by one member. The selected local region should cover as much useful group perception and as many relevant opposing units as possible without exceeding the cap.

Enemy policy owns enemy target-region selection:

- offense selects from player defensive deployment regions first;
- active defense selects from player offensive deployment regions first;
- hold defense remains in its held region until any member takes damage, attacks, or perceives a player unit, then the whole group switches to active assault;
- V1 does not require activated defenders to return to their original defensive region.

If fixed target regions contain no relevant opposing units, enemy policy may generate temporary target regions from opposing unit clusters. Temporary regions are cached per battle group and refreshed only at configured replan intervals. The default refresh interval is 5 Runtime ticks.

Player policy is separate. Player groups may reuse perception facts, local combat-region building, local target/slot solving, and Runtime validation, but player target regions and posture are changed only by player commands or accepted player battle plans. Enemy region policy must never rewrite player group intent.

Player-commanded groups may still enter a player-scoped engaged state from perception, damage, attack, or route-blocking facts when the active plan or engagement rule allows local response. That transition authorizes local-combat facts and slot solving inside the current player command scope; it does not convert the group to enemy offense, replace the selected objective, or override the player plan.

Local-combat joining must degrade through named roles. If an attack slot is statically reachable but the actor has no executable next step because of current occupancy, footprint, or reservation facts, the group should request a support, queue, line-hold, flank, or regroup role rather than repeatedly reporting generic path failure.

## Failure Rules

- Missing owner battle-group id: reject the region and emit a diagnostic.
- Region unreachable: keep Runtime state explicit, emit a reason, and let the owning policy choose a replacement region at a valid replan boundary.
- No fixed region contains opposing units: build or reuse a temporary region if allowed by refresh timing; otherwise keep the current valid region until replan.
- No perceived opposing units for the whole group for the configured disengage period: exit engaged state, clear unit target locks, and replan region movement.
- Player command conflict: player command wins; automatic local combat or region policy degrades to a request inside that command scope.
- Static reachability but blocked dynamic entry: keep the local combat assignment explicit, emit the blocked entry reason, and prefer a named support or queue role before idle hold.

## Acceptance

This architecture is acceptable when:

- every tactical region has one battle-group owner;
- global caches are read/query helpers, not tactical authorities;
- non-engaged movement is region-directed;
- engaged combat is locally optimized inside bounded group-owned local combat regions;
- player-commanded groups can enter player-scoped local combat without enemy policy overwriting player intent;
- enemy offense, active defense, and hold-defense activation are defined without affecting player command authority;
- temporary regions are per-group, cached, and refreshed no more often than the configured interval;
- diagnostics can explain region selection, engagement transitions, and local combat target/slot decisions.
