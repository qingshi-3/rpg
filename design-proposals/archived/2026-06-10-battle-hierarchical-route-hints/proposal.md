# Battle Hierarchical Route Hints Proposal

Status: Archived

## Requirement Id

REQ-BATTLE-HIERARCHICAL-ROUTE-HINTS-2026-06-10

## Parent Proposal

None.

## Supersedes

None.

## Superseded By

None.

## Amends

- `design-proposals/archived/2026-06-10-battle-local-neighbor-navigation/`
- `design-proposals/archived/2026-06-10-battle-local-steering-navigation/`

## Amended By

None.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-10-battle-hierarchical-route-hints.md`

## Current Architecture

Accepted first-slice movement removed Runtime flow fields and per-unit whole-map A* from combat hot paths. Units now use state-machine intent, local neighbor scoring, local steering memory, and short obstacle following. This is fast and keeps ordinary movement readable, but it cannot reliably solve map-scale static barriers such as rivers, long walls, or bridge-like chokepoints when the first correct move temporarily moves away from the target.

The current route-hint concept is permitted by authority documents, but it is not backed by a compiled static route topology or a clear runtime owner. As a result, local steering is tempted to become a second pathfinding system.

## Expected Architecture

Battle navigation adds a static hierarchical route-hint layer between immutable map topology and local actor movement:

```text
BattleNavigationTopology
-> BattleRouteTopology chunks, portals, clearance profiles
-> low-frequency battle-group route hint query
-> actor local neighbor movement toward the current hint
-> Runtime topology, footprint, occupancy, and reservation validation
```

`BattleRouteTopology` is compiled from static topology once per battle snapshot or topology version. It splits the walkable map into chunks or sectors, identifies portal edges between neighboring chunks, records compact inter-portal costs, and stores footprint clearance profiles such as `1x1`, `2x1`, `1x2`, `2x2`, and `3x3`. It does not include live unit occupancy, reservations, target state, damage, Presentation facts, or behavior-tree state.

Runtime route planning is low-frequency and group-scoped. A battle group may request a route hint from its current region or action zone toward a target region, objective, retreat zone, or selected combat zone. The query returns a corridor of coarse anchors such as next portal, entrance, gate, chokepoint, or route anchor. It does not return a per-actor full path, does not authorize movement by itself, and is invalidated by command, objective, region, route profile, topology version, or group action-zone changes.

Actors still execute movement locally. At each movement continuation boundary, the actor state machine chooses the current tactical intent, then local movement ranks nearby legal anchors toward the current route hint or final region. Runtime remains the final authority for topology, footprint, dynamic occupancy, same-tick reservations, attack range, action locks, damage, defeat, and events.

Local steering remains useful, but only for short-range execution: small static edge following, local congestion response, queue/hold, support, pressure, and stuck recovery. It must not grow into long-distance river/wall solving. Dynamic living-unit blockers are handled through queue, support, hold pressure, or retry, not by rebuilding the static route graph.

## User Acceptance

The user accepted this direction on 2026-06-10 after confirming that the change is a new optimization plan and should first clarify cleanup responsibilities before implementation.
