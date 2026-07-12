# Battle Local Steering Navigation Proposal

Status: Archived

## Requirement Id

REQ-BATTLE-LOCAL-STEERING-NAVIGATION-2026-06-10

## Parent Proposal

None.

## Supersedes

None.

## Superseded By

None.

## Amends

- `design-proposals/archived/2026-06-10-battle-local-neighbor-navigation/`

## Amended By

None.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-10-battle-local-steering-navigation.md`

## Current Architecture

Accepted Runtime movement uses state-machine intent plus bounded local neighbor scoring instead of flow-field construction. This removes the previous performance spike, but the architecture still describes obstacle avoidance mostly as stateless neighbor selection. In practice, a unit can still look dumb near longer static obstacles because it may not remember that it is following an obstacle boundary, and objective movement has no formal route-hint layer for map-authored gates, lanes, or chokepoints.

## Expected Architecture

First-slice movement remains local and state-machine-owned, with no Runtime flow fields and no per-unit whole-map A*. Runtime adds local steering memory for static obstacle avoidance:

```text
SeekGoal
-> FollowObstacle
-> RejoinSeek
-> QueueOrHold
-> StuckRecovery
```

The steering state is advisory Runtime memory attached to the active movement intent. It records the selected side of an obstacle, the best distance reached while following it, a short progress budget, and the selected coarse route hint when one is available. It never authorizes movement by itself; every committed neighbor still validates topology, footprint, occupancy, reservations, target or region intent, and command facts.

Coarse route hints are low-frequency region hints, not pathfinding fields. They may come from authored objective-zone entrances, chokepoints, route lanes, or group action-zone hints. A unit may steer toward the current hint before the final region when that hint is inside the same command scope. If no hint exists, local steering still works against ordinary nearby obstacles, but maze-like terrain is allowed to degrade to hold, queue, or explicit movement failure.

Dynamic unit blockers are not static obstacles. When the preferred route is blocked by living-unit occupancy or same-tick reservations, Runtime should queue, support, hold pressure, or retry at the next movement boundary instead of wandering far around the formation.

## User Acceptance

The user accepted this direction on 2026-06-10 after reviewing the tradeoff: local steering state plus coarse route hints, still without restoring global flow fields.
