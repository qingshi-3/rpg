# Battle Navigation Topology Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by separating map topology compilation from runtime movement resolution.

The accepted direction is:

```text
Godot TileMapLayer and authored map topology
-> immutable battle navigation topology data
-> immutable battle route topology and coarse route hints
-> runtime local movement resolution, occupancy, and reservations
```

## Responsibility

Battle navigation has three deliberately separate systems.

Map topology compilation owns static map interpretation:

- reading or receiving the final top walkable battle-map surfaces;
- excluding non-final covered surfaces, such as underground water under land;
- turning same-level adjacency into explicit topology edges;
- turning authored height links into explicit topology edges;
- recording topology node and edge origin for diagnostics.

Static route topology compilation owns reusable map-scale route hints:

- splitting the immutable topology into chunks, sectors, or equivalent route regions;
- detecting portal edges between neighboring route regions;
- recording compact inter-portal costs and route anchors such as entrances, gates, chokepoints, lanes, or bridge approaches;
- recording footprint clearance profiles for route regions and portal edges;
- exposing a topology version or diagnostic identity so Runtime can invalidate stale route hints.

Runtime movement resolution owns live movement decisions:

- reading immutable `BattleNavigationTopology` nodes and edges;
- reading immutable `BattleRouteTopology` route regions, portals, and clearance profiles when available;
- actor footprint static placement legality against topology nodes;
- objective-zone and region next-step ranking from accepted battle plans;
- dynamic occupancy and same-tick reservations;
- bounded local obstacle avoidance through neighboring anchor checks;
- local steering memory for short static obstacle following, rejoin, queue/hold, and stuck recovery;
- group-scoped coarse route hints such as objective entrances, lanes, gates, chokepoints, portals, or group-owned route anchors;
- low-noise runtime movement failure diagnostics.

## Does Not Own

Runtime movement resolution must not parse or reinterpret:

- `TileMapLayer`;
- `BattleGridMap`;
- `GridCellSurface`;
- `LayerRole`;
- raw water, bridge, land, or underground-layer concepts;
- raw height-link authoring nodes.

Map topology compilation does not own dynamic actor occupancy, command intent, target choice, attack legality, damage, settlement, or presentation movement.

Static route topology compilation does not own dynamic actor occupancy, command intent, target choice, attack legality, damage, settlement, or presentation movement. It also must not include living units, same-tick reservations, current targets, local combat slots, or behavior-tree state in route topology data.

## Data Contract

`BattleNavigationTopology` is the immutable data layer between map authoring and Runtime.

It should expose:

- topology nodes keyed by battle grid coordinate and height;
- explicit topology edges between nodes;
- edge origin such as same-level adjacency or authored height link;
- topology version or diagnostic identity;
- compact graph summary for diagnostics.

Legacy raw `NavigationSurfaces` and `NavigationConnections` may remain only as compatibility input to the compiler during migration. Runtime graph construction consumes compiled topology, not those raw authoring snapshots.

`BattleRouteTopology` is the optional immutable route-hint layer derived from `BattleNavigationTopology`.

It should expose:

- route regions, chunks, or sectors keyed by compact ids;
- portal or entrance edges between route regions;
- representative route anchors for portals, gates, bridge approaches, lanes, chokepoints, and objective entrances;
- compact inter-portal costs inside a route region;
- footprint clearance profiles or `passableFor` masks for supported footprints such as `1x1`, `2x1`, `1x2`, `2x2`, and `3x3`;
- route topology version and graph summary for diagnostics.

`BattleRouteTopology` is not a flow field. It stores reusable static corridor structure, not per-target distance gradients, per-actor paths, current occupancy, combat slots, target state, or Presentation facts.

## Movement Rules

- Deployment places actors on valid square-grid cells.
- During live battle, an actor may reserve and move to a valid neighbor cell.
- First implementation uses 8-neighbor movement unless topology data forbids diagonal transitions.
- Occupancy and reservation prevent multiple living actors from owning the same committed cell.
- Dynamic living-unit occupancy is layered over topology: immediate next-cell occupancy and same-tick reservations are hard blockers, while future projected occupancy is soft route cost.
- Runtime may evaluate several cells ahead, but it commits movement across neighboring cells only through Runtime-owned movement progress, occupancy, and reservation boundaries.
- Every actor state-machine movement decision boundary validates movement from current actor, active objective or retained target, topology, footprint, occupancy, reservations, and command facts.
- Default assault movement follows the actor's battle-group objective and engagement rule. Target acquisition is attack-opportunity first only when the actor has no live retained target, has an immediate attack opportunity, or the active engagement rule permits reacquisition. A moving actor retains a live target while marching so each movement boundary does not rescore every enemy or rebuild whole-region navigation fields.
- Retained targets must be dropped when they die, become invalid, or a command/runtime interrupt explicitly changes targeting. Immediate valid attack opportunities may still interrupt a retained far target so units do not walk past an enemy already in range.
- Actors cannot perform basic attacks while moving between cells.

Runtime replanning is a simulation concern, not a render-frame concern. Fixed simulation ticks may advance an actor's existing movement, attack, or recovery phase without creating a new movement query. Godot `_Process`, animation frames, or Presentation playback must not call movement resolution to create new combat truth.

Actors that are already in `Moving`, `AttackRecovery`, casting, interruption, or another non-decision phase do not replan just because another render frame elapsed. Moving actors may refresh the next legal neighbor at Runtime movement-continuation boundaries; attack recovery, casting, interruption, and other locked phases replan only when the Runtime state machine reaches the next valid anchored decision boundary or when a valid interrupt explicitly cancels the current action.

## Intent-Directed Region Movement

Battle-group movement goals are resolved from command, battle plan, or AI intent into target objects and tactical regions. Player groups normally use authored objective zones carried by accepted battle-group plans or live player commands. Enemy groups use battle-group-owned target objects selected by configurable AI intent plans. Runtime movement resolution treats all of them as region goals or route constraints, not as hidden target actors.

Intent-directed region movement should be resolved before local attack-slot movement:

```text
active battle-group objective, selected target object, hold, protect, retreat, or intent-derived region
-> region candidate anchors
-> route hints from lanes, chokepoints, flank routes, reserve points, or defend points
-> footprint-aware next-step ranking
-> local occupancy and reservation validation
```

When no enemy is locked, a battle group advances, holds, or withdraws toward its selected stable target object or region. When a local target is locked, movement may temporarily switch to attack-slot approach inside the group-owned local combat region. When that target dies, becomes invalid, exceeds pursuit limits, or cannot be reached under the engagement rule, movement returns to the current objective zone, AI-intent target object, hold area, protect-hero area, fallback target, or retreat path.

Runtime-observed clusters and temporary regions are volatile tactical observations. They may support local combat or explicit cluster-hunting intents, but ordinary non-engaged enemy movement must not rebuild its long-term navigation goal from those observations at high frequency.

For the first slice, region-directed movement is resolved through a group-scoped route hint plus local neighbor scoring. The route hint gives a coarse next anchor such as a portal, entrance, gate, chokepoint, lane anchor, or bridge approach. Local movement still commits only neighboring cells and validates every step against topology, footprint, occupancy, reservations, command facts, and actor state.

Route hints are low-frequency coarse anchors, not a field or full route. They may come from map-authored markers or from a static `BattleRouteTopology` query over chunks, sectors, portals, and clearance profiles. Runtime must not build route fields for every objective, target, or combat zone. Target movement, target death, combat-zone membership changes, command target changes, actor capability changes, route profile changes, topology version changes, or route-hint invalidation invalidate the current movement intent and let the state machine choose a new local step at the next valid boundary.

The first route-topology implementation should route a battle group by the largest footprint profile needed by that group. Smaller actors may consume the same coarse route when it is legal for the group profile. Larger actors must not consume a smaller-footprint route that crosses narrow-only portals.

This separation is a product rule as much as a performance rule: ordinary movement should read as executing the group plan or AI intent, not as every unit independently searching the whole battlefield for the globally best attack anchor.

## Continuous Movement And Local Neighbor Resolution

Mature RTS movement is a continuous runtime state over discrete navigation authority.

- First-slice Runtime hot paths use local neighbor resolution. The state machine selects the active objective, target, attack slot, support slot, or hold role; the movement resolver ranks nearby anchors against that selected intent.
- Local obstacle avoidance is a stateful local steering algorithm, not a stateless one-step score and not a map-scale pathfinder. A unit starts in `SeekGoal`; when short static topology blocks useful progress it may enter `FollowObstacle`, keep a fixed obstacle side, track the best goal distance reached, and switch back through `RejoinSeek` when normal goal progress becomes available.
- Local steering memory is advisory. It can bias neighbor ranking and prevent left/right jitter, but it cannot authorize movement that fails topology, footprint, occupancy, reservation, target, region, or command validation.
- Static obstacles and dynamic blockers are different cases. Static topology blockers may use obstacle following. Living-unit occupancy and same-tick reservation blockers should degrade to queue, support, hold pressure, or retry rather than sending units on wide detours around their own formation.
- `StuckRecovery` is a bounded escape rule: if obstacle following does not improve within its configured progress budget, Runtime may switch obstacle side, return to the current coarse route hint, hold, or emit an explicit movement failure. It must not silently start a whole-map search.
- Join/pressure movement inside a combat zone uses the same local resolver toward selected attack, support, queue, or region anchors. Exact attack-position assignment is a low-frequency local-combat decision, not a per-frame movement calculation.
- Runtime actors move by speed over fixed simulation ticks, not by treating each cell transition as a complete independent action.
- Target acquisition and movement pathing are separate costs: target acquisition should be sticky and low-frequency, while movement continuation validates the retained target's next legal neighbor at runtime boundaries.
- Cell occupancy remains authoritative at committed cell boundaries. Reservations protect immediate continuation targets and prevent same-tick overlap or direct edge swaps.
- Runtime may continue a moving actor through multiple adjacent cells over time while still validating each cell boundary against topology, footprint, occupancy, reservations, target state, and command facts.
- Presentation may interpolate between the last committed anchor and the current runtime movement target. It must not compute alternative paths, bypass reservations, or move an actor into visual attack range without Runtime facts.

## Footprint Legality

- The actor anchor is a compact position key. It is not enough for only the anchor cell to be walkable.
- Static placement legality is footprint-aware: a candidate anchor is valid only if every covered cell in the actor's footprint is present in topology.
- Runtime movement resolution may use an anchor graph, cached placement map, or clearance map, but first-slice combat hot paths must not construct flow fields. Any navigation data must be derived from full-footprint placement legality for the actor size.
- Candidate committed movement is valid only if every covered cell in the actor's next footprint is present in topology, unreserved, and unoccupied by other living actors at tick start.
- Occupancy and reservation are stored per covered cell.
- Tick-start occupied covered cells remain hard blockers for immediate movement for the whole tick. A cell released by another actor's same-tick movement may be entered only after Runtime reaches a later decision boundary with updated occupancy.
- Projected future route cells must be statically legal by footprint, but may include occupied cells as extra cost because those units may move before the actor reaches that part of the route.
- Direct same-tick edge swaps are rejected.
- Missing covered topology nodes make that anchor illegal even when the actor's anchor cell itself exists.
- When a reservation rejects the preferred neighbor, Runtime may try lower-ranked legal movement candidates produced by the same footprint-aware decision before choosing to hold.

## Diagonal And Height Links

For generated same-level diagonal movement, the diagonal target and the relevant orthogonal side anchors must be legal for the actor footprint. This is the square-grid swept-footprint rule that prevents large or small units from cutting through blocked corners, missing terrain, water gaps, walls, or narrow diagonal slits.

Authored height transitions are explicit topology edges. Runtime may use them when the compiled topology exposes them; Runtime must not infer height transitions from raw authoring nodes.

## Attack Slots

Runtime movement toward an enemy does not path to the enemy anchor. It paths toward a valid attack slot.

A valid basic-attack slot is an attacker anchor that satisfies all of these facts:

- the attacker footprint can legally stand on that anchor;
- the attacker footprint does not overlap the target footprint;
- the shortest square-grid distance from attacker footprint to target footprint is within the attack range;
- dynamic occupancy and same-tick reservation rules allow the committed next movement when moving toward that slot.

Large targets naturally expose more potential attack slots than `1x1` targets. This is an intended body-size rule: a larger unit is more exposed to being surrounded by smaller units, while its own footprint may make movement through tight terrain harder.

Attack-position search must be target-local. Runtime must not score a local-combat candidate list by building a full flow field or A* query once per candidate anchor. The first slice enumerates legal attack anchors around the target footprint, filters them by topology and occupancy, and lets the local neighbor resolver step toward the selected slot or nearest executable slot.

## Support Slots

Runtime may derive support slots for a local combat situation when direct attack slots are occupied, blocked, reserved, outside command scope, or lower priority than maintaining local pressure.

A support slot is not an arbitrary waiting cell. It is a legal anchor that:

- can place the actor footprint on topology;
- does not overlap occupied or reserved cells for the committed next step;
- does not block an existing occupied attack slot;
- improves the actor's ability to enter an attack slot, support a front line, or maintain pressure near the local fight;
- respects the actor's objective, held area, defense leash, protect target, or retreat constraint;
- can be ordered deterministically so multiple actors do not choose the same support position.

Support slots are fallback and staging positions. They do not authorize attacks by themselves, and Presentation must not move a unit into visual attack range without Runtime movement facts.

First-slice support slots should be limited to named, readable roles:

| Support Slot | Purpose |
|---|---|
| Melee queue | Stand in a second-line anchor that can enter an attack slot within one or two legal steps when it opens. |
| Line hold | Preserve a chokepoint or front-line shape without blocking an occupied attack slot. |
| Ranged hold | Preserve distance or a future firing lane without blocking melee movement. |

Generic support-slot scoring is not a first-slice requirement. The first implementation should prefer deterministic, explainable slot rules that can be diagnosed when a unit joins, waits, returns, or refuses to join.

## Movement Intent Invalidation

Authoritative movement intent is not a full precomputed battle path. Cached target, slot, region, or local scoring data is advisory only.

Local steering state belongs to the movement intent. It may store a steering mode, obstacle-follow side, best observed distance, last useful route hint, and a small progress budget. It must be cleared or rebuilt when the active intent changes.

Invalidate cached path data when any of these facts change:

- command id or command posture;
- movement intent kind or revision;
- actor anchor;
- target actor id or target anchor;
- destination cell;
- route hint id or route hint anchor;
- route topology version, route profile, or route corridor id;
- topology version;
- dynamic occupancy or reservation revision;
- target defeat or target invalidation;
- reservation rejection or path failure.

This protects the RTS expectation that a unit pursuing an enemy, responding to an attack command, switching from move-to-point to chase, or reacting to a target changing route replans from current facts at the next decision boundary.

Intent invalidation changes the next movement decision. It does not let movement resolution bypass an in-progress action lock unless the command or runtime rule explicitly interrupts that action.

First-slice movement does not wait for dirty navigation fields. A unit may keep its current selected target or slot while it remains valid, choose a new local neighbor at a movement-continuation boundary, or hold with an explicit reason when every useful neighbor is blocked. Runtime attack legality and movement commits still validate against current actor positions, occupancy, reservations, and attack range, so stale intent cannot authorize invalid attacks or overlapping movement.

## Diagnostics

Diagnostics should distinguish topology failures from runtime movement-resolution failures.

Topology diagnostics answer:

- how many nodes and edges were compiled;
- which surfaces were excluded because they were covered or non-final;
- which height links became explicit edges;
- whether disconnected components exist before Runtime starts.

Runtime movement diagnostics answer:

- whether the actor start footprint is legal;
- whether the target or destination exists in topology;
- whether a local executable attack or support entry exists;
- whether failure came from missing covered cells, diagonal side clearance, dynamic occupancy, same-tick reservation, local congestion, or no useful neighbor.

Diagnostics must be low-noise: log important state transitions and one failure reason per actor-target/path state, not per frame or per search node.

## Inputs

- authored map surfaces and height-link authoring data;
- semantic map facts routed through Application where relevant;
- objective-zone markers and route-hint markers routed through Application snapshots;
- compiled static route topology derived from battle navigation topology when available;
- actor footprint width and height from snapshots;
- current runtime occupancy, reservations, target facts, and command facts.

## Outputs

- immutable `BattleNavigationTopology`;
- immutable `BattleRouteTopology` when route hints are available;
- runtime local next-step movement decisions;
- local movement failure diagnostics;
- movement events emitted by Runtime after successful reservations and mutations.

## Acceptance

This architecture is acceptable when:

- a land surface over underground water exports only the land node into topology;
- same-level neighbors and authored height links appear as explicit topology edges before Runtime starts;
- Runtime movement resolution consumes only topology plus actor footprint, dynamic occupancy, reservations, and state-machine intent;
- static route topology, when present, is derived from immutable navigation topology and exposes only chunks, portals, route anchors, costs, clearance profiles, version, and diagnostics;
- Runtime route-hint queries are low-frequency and group-scoped rather than per-actor movement-decision whole-map searches;
- Runtime never parses TileMapLayer, water, bridge, LayerRole, or raw height-link authoring concepts;
- large footprints cannot path through missing covered topology cells;
- diagonal movement cannot cut across blocked corners unless an explicit authored connection allows that transition;
- local movement is recalculated at actor state-machine movement decision boundaries from current actor, target or region intent, occupancy, reservation, topology, footprint, and command facts;
- movement replanning cannot bypass in-progress movement, attack, recovery, cast, or interruption phases unless a valid runtime interrupt cancels that phase.
