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
- actor footprint static legality against topology nodes;
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
- Runtime may evaluate several cells ahead, but it commits only one neighbor move per actor action.
- Every runtime decision boundary recalculates pathfinding from current actor, target, topology, footprint, occupancy, reservations, and command facts.
- Ordinary assault keeps the acquired live target as runtime intent while recalculating the path from live positions. Target loss, invalidation, or explicit command rules may force reacquisition.
- If a target is already engaged by a same-faction actor, support actors should prefer reinforcing with a nearer support step over taking a far-side route whose first step moves away from the target.
- Actors cannot perform basic attacks while moving between cells.

Runtime replanning is a simulation concern, not a render-frame concern. Godot `_Process`, animation frames, or Presentation playback must not call pathfinding to create new combat truth.

## Footprint Legality

- Candidate committed movement is valid only if every covered cell in the actor's next footprint is present in topology, unoccupied by other living actors, and unreserved.
- Occupancy and reservation are stored per covered cell.
- Projected future route cells must be statically legal by footprint, but may include occupied cells as extra cost because those units may move before the actor reaches that part of the route.
- Direct same-tick edge swaps are rejected.
- Missing covered topology nodes make that anchor illegal even when the actor's anchor cell itself exists.

## Diagonal And Height Links

For generated same-level diagonal movement, the diagonal target and the relevant orthogonal side anchors must be legal for the actor footprint. This prevents corner cutting through blocked or missing terrain.

Authored height transitions are explicit topology edges. Runtime may use them when the compiled topology exposes them; Runtime must not infer height transitions from raw authoring nodes.

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
- pathfinding is recalculated at runtime decision boundaries from current actor, target, occupancy, reservation, topology, footprint, and command facts.
