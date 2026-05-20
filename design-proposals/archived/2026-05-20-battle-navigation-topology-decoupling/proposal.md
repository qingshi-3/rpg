# Battle Navigation Topology Decoupling Proposal

Status: Archived
Workflow Role: Design/architecture proposal only. The accepted rule is merged into `system-design/battle-navigation-topology-architecture.md`.
Requirement Id: REQ-BNAV-TOPOLOGY-DECOUPLING
Parent Proposal: `design-proposals/archived/2026-05-19-bridge-spatial-battle-v0`
Supersedes: None
Superseded By: None
Amends: `REQ-BRIDGE-SPATIAL-BATTLE-V0`
Amended By: None
Affected Authority Documents:
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/hero-led-light-rts-system-architecture.md`
Related Implementation Proposals: None yet.

## Scope

This proposal narrows the V0 battle navigation repair by separating two runtime concerns that were coupled during the bridge battle work:

```text
Godot TileMapLayer and authored map topology
-> battle navigation topology data
-> runtime pathfinding, occupancy, and reservations
```

The accepted gameplay direction does not change. This is an implementation architecture repair for hero-led light RTS combat.

## Current Architecture

The current implementation exports walkable navigation surfaces from `BattleGridMap`, but `BattleNavigationGraph` still turns those surfaces and authored height connections into the final same-level and height-transition graph inside Runtime.

That means Runtime navigation still owns too much static topology knowledge:

- final walkable surface selection;
- same-level edge generation;
- authored height-link interpretation;
- component summary diagnostics for authored surfaces.

This makes TileMapLayer and multi-level map changes hard to debug because a pathfinding failure can come from either map topology compilation or A* movement logic.

## Expected Architecture

Introduce an explicit `BattleNavigationTopology` data layer.

Map topology compilation owns:

- reading or receiving top walkable map surfaces;
- excluding non-final surfaces such as underground water covered by land;
- turning same-level adjacency into explicit topology edges;
- turning authored height links into explicit topology edges;
- recording edge origin for diagnostics.

Runtime pathfinding owns:

- reading immutable topology nodes and edges;
- actor footprint static legality against topology nodes;
- dynamic occupancy and same-tick reservations;
- A* next-step search and low-noise runtime failure diagnostics.

Runtime pathfinding must not parse `TileMapLayer`, `BattleGridMap`, `GridCellSurface`, `LayerRole`, water, bridge, or raw height-link authoring concepts.

## Implementation Slice

First slice:

1. Add `BattleNavigationTopology` DTOs and a compiler in Application.
2. Populate topology when battle navigation snapshots are built or copied into `LocationBattleContext`.
3. Change `BattleNavigationGraph` to consume `BattleNavigationTopology` instead of raw surfaces/connections.
4. Keep legacy `NavigationSurfaces` and `NavigationConnections` only as compatibility input for the compiler during this migration.
5. Add regression tests that prove Runtime consumes topology data and no longer compiles raw surface/connection lists.

## Acceptance

- A land surface over underground water exports only the land node into topology.
- Same-level neighbors and authored height links appear as explicit topology edges before Runtime starts.
- `BattleNavigationGraph` does not reference `BattleNavigationSurfaceSnapshot` or `BattleNavigationConnectionSnapshot`.
- Runtime can still route, block missing covered footprint surfaces, reject corner cutting, and use authored height transitions.
- Diagnostics distinguish topology graph summary from pathfinding failure.
