# Battle Beacon Flowfield Command Acceptance

Status: Accepted

Accepted On: 2026-07-07

Accepted By: user confirmation in Codex thread.

## Accepted Requirement

Replace the cumbersome battle operation chain that requires deployment, unit-state selection, and target-region selection.

The accepted flow is:

```text
deploy battle groups
-> start battle in default attack posture
-> select one or more battle groups during live battle or tactical pause
-> right-click a reachable destination cell
-> place or move one shared destination beacon for the selected groups
-> drive those selected groups through beacon flow-field pathing
```

Only selected groups update their destination. Other groups keep their current command or runtime state.

## Accepted Architecture Boundary

Destination-beacon flow fields are allowed, but they are command-scoped static navigation caches. They are rebuilt only when beacon, topology, height, or footprint/passability profile inputs change.

Flow fields must not include dynamic unit occupancy, same-tick reservations, attack slots, local-combat decisions, target selection, damage state, or Presentation state.

Runtime still owns movement commits, dynamic blockers, reservations, action locks, target acquisition, local combat, event emission, and failure reasons. Presentation remains display-only and consumes Runtime events for movement visuals.

## Merge Record

Authority merge: completed on 2026-07-07.

Archived: completed on 2026-07-07.
