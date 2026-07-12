# Battle Navigation Topology Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by separating map topology compilation from runtime pathfinding.

The accepted direction is:

```text
Godot TileMapLayer and authored map topology
-> immutable battle navigation topology data
-> runtime pathfinding, occupancy, and reservations
```

## Responsibility

Battle navigation has two deliberately separate systems.

Map topology compilation owns static map interpretation:

- reading or receiving the final top walkable battle-map surfaces;
- excluding non-final covered surfaces, such as underground water under land;
- turning same-level adjacency into explicit topology edges;
- turning authored height links into explicit topology edges;
- recording topology node and edge origin for diagnostics.

Runtime pathfinding owns live movement decisions:

- reading immutable `BattleNavigationTopology` nodes and edges;
- actor footprint static placement legality against topology nodes;
- dynamic occupancy and same-tick reservations;
- A* or equivalent next-step search;
- low-noise runtime path failure diagnostics.

## Does Not Own

Runtime pathfinding must not parse or reinterpret:

- `TileMapLayer`;
- `BattleGridMap`;
- `GridCellSurface`;
- `LayerRole`;
- raw water, bridge, land, or underground-layer concepts;
- raw height-link authoring nodes.

Map topology compilation does not own dynamic actor occupancy, command intent, target choice, attack legality, damage, settlement, or presentation movement.

## Data Contract

`BattleNavigationTopology` is the immutable data layer between map authoring and Runtime.

It should expose:

- topology nodes keyed by battle grid coordinate and height;
- explicit topology edges between nodes;
- edge origin such as same-level adjacency or authored height link;
- topology version or diagnostic identity;
- compact graph summary for diagnostics.

Legacy raw `NavigationSurfaces` and `NavigationConnections` may remain only as compatibility input to the compiler during migration. Runtime graph construction consumes compiled topology, not those raw authoring snapshots.

## Movement Rules

- Deployment places actors on valid square-grid cells.
- During live battle, an actor may reserve and move to a valid neighbor cell.
- First implementation uses 8-neighbor movement unless topology data forbids diagonal transitions.
- Occupancy and reservation prevent multiple living actors from owning the same committed cell.
- Dynamic living-unit occupancy is layered over topology: immediate next-cell occupancy and same-tick reservations are hard blockers, while future projected occupancy is soft route cost.
- Runtime may evaluate several cells ahead, but it commits movement across neighboring cells only through Runtime-owned movement progress, occupancy, and reservation boundaries.
- Every actor state-machine movement decision boundary validates pathfinding from current actor, retained target, topology, footprint, occupancy, reservations, and command facts.
- Default assault target acquisition is attack-opportunity first only when the actor has no live retained target or has an immediate attack opportunity. A moving actor retains a live target while marching so each movement boundary does not rescore every enemy and rebuild flow fields.
- Retained targets must be dropped when they die, become invalid, or a command/runtime interrupt explicitly changes targeting. Immediate valid attack opportunities may still interrupt a retained far target so units do not walk past an enemy already in range.
- Actors cannot perform basic attacks while moving between cells.

Runtime replanning is a simulation concern, not a render-frame concern. Fixed simulation ticks may advance an actor's existing movement, attack, or recovery phase without creating a new path query. Godot `_Process`, animation frames, or Presentation playback must not call pathfinding to create new combat truth.

Actors that are already in `Moving`, `AttackRecovery`, casting, interruption, or another non-decision phase do not replan just because another render frame elapsed. Moving actors may refresh the next legal neighbor at Runtime movement-continuation boundaries; attack recovery, casting, interruption, and other locked phases replan only when the Runtime state machine reaches the next valid anchored decision boundary or when a valid interrupt explicitly cancels the current action.

## Continuous Movement And Flow Fields

Mature RTS movement is a continuous runtime state over discrete navigation authority.

- Flow fields, cached placement maps, and attack-slot fields provide movement direction or next-anchor ranking for groups of actors with compatible goals.
- Runtime actors move by speed over fixed simulation ticks, not by treating each cell transition as a complete independent action.
- Target acquisition and movement pathing are separate costs: target acquisition should be sticky and low-frequency, while movement continuation validates the retained target's next legal neighbor at runtime boundaries.
- Cell occupancy remains authoritative at committed cell boundaries. Reservations protect immediate continuation targets and prevent same-tick overlap or direct edge swaps.
- Runtime may continue a moving actor through multiple adjacent cells over time while still validating each cell boundary against topology, footprint, occupancy, reservations, target state, and command facts.
- Presentation may interpolate between the last committed anchor and the current runtime movement target. It must not compute alternative paths, bypass reservations, or move an actor into visual attack range without Runtime facts.

## Footprint Legality

- The actor anchor is a compact position key. It is not enough for only the anchor cell to be walkable.
- Static placement legality is footprint-aware: a candidate anchor is valid only if every covered cell in the actor's footprint is present in topology.
- Runtime pathfinding may use an anchor graph, cached placement map, clearance map, or flow field, but that navigation data must be derived from full-footprint placement legality for the actor size.
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

## Path Invalidation

Authoritative movement intent is not a full precomputed battle path. Cached path data, if any, is advisory only.

Invalidate cached path data when any of these facts change:

- command id or command posture;
- movement intent kind or revision;
- actor anchor;
- target actor id or target anchor;
- destination cell;
- topology version;
- dynamic occupancy or reservation revision;
- target defeat or target invalidation;
- reservation rejection or path failure.

This protects the RTS expectation that a unit pursuing an enemy, responding to an attack command, switching from move-to-point to chase, or reacting to a target changing route replans from current facts at the next decision boundary.

Path invalidation changes the next movement decision. It does not let pathfinding bypass an in-progress action lock unless the command or runtime rule explicitly interrupts that action.

## Diagnostics

Diagnostics should distinguish topology failures from runtime pathfinding failures.

Topology diagnostics answer:

- how many nodes and edges were compiled;
- which surfaces were excluded because they were covered or non-final;
- which height links became explicit edges;
- whether disconnected components exist before Runtime starts.

Runtime pathfinding diagnostics answer:

- whether the actor start footprint is legal;
- whether the target or destination exists in topology;
- whether a reachable attack anchor exists;
- whether failure came from missing covered cells, diagonal side clearance, dynamic occupancy, same-tick reservation, or no route.

Diagnostics must be low-noise: log important state transitions and one failure reason per actor-target/path state, not per frame or per search node.

## Inputs

- authored map surfaces and height-link authoring data;
- semantic map facts routed through Application where relevant;
- actor footprint width and height from snapshots;
- current runtime occupancy, reservations, target facts, and command facts.

## Outputs

- immutable `BattleNavigationTopology`;
- runtime next-step movement decisions;
- movement failure diagnostics;
- movement events emitted by Runtime after successful reservations and mutations.

## Acceptance

This architecture is acceptable when:

- a land surface over underground water exports only the land node into topology;
- same-level neighbors and authored height links appear as explicit topology edges before Runtime starts;
- Runtime pathfinding consumes only topology plus actor footprint, dynamic occupancy, and reservations;
- Runtime never parses TileMapLayer, water, bridge, LayerRole, or raw height-link authoring concepts;
- large footprints cannot path through missing covered topology cells;
- diagonal movement cannot cut across blocked corners unless an explicit authored connection allows that transition;
- pathfinding is recalculated at actor state-machine movement decision boundaries from current actor, target, occupancy, reservation, topology, footprint, and command facts;
- movement replanning cannot bypass in-progress movement, attack, recovery, cast, or interruption phases unless a valid runtime interrupt cancels that phase.
