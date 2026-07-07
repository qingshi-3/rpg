# Battle Beacon Flowfield Command Proposal

Status: Archived

## Relationship Metadata

Requirement Id: BATTLE-BEACON-001

Parent Proposal: none

Supersedes: none

Superseded By: none

Amends:

- `2026-05-23-battle-plan-state-machine`
- `2026-06-07-battle-preparation-drag-deployment-ux`
- `2026-06-10-battle-local-neighbor-navigation`
- `2026-06-10-battle-local-steering-navigation`
- `2026-06-10-battle-hierarchical-route-hints`
- `2026-06-16-player-tactical-intent-migration`

Amended By: none

Affected Authority Documents:

- `gameplay-design/content-systems-long-term-design.md`
- `gameplay-design/details/combat-command/README.md`
- `gameplay-design/vertical-slices/first-playable-slice.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-tactical-intent-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/semantic-map-marker-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`

Related Implementation Proposals:

- pending: `gameplay-alignment/implementation-proposals/2026-07-07-battle-beacon-flowfield-command.md`

## Current Design

Battle preparation requires more player planning than the desired first playable flow. Current authority still requires or assumes selected objective areas and engagement rules before battle start in several player-facing, UI, command, Runtime, and navigation documents. Navigation authority also rejects runtime flow-field construction in the first-slice hot path, favoring route hints and local neighbor scoring.

This creates a slow operation chain:

```text
deploy group
-> choose objective region
-> choose engagement rule
-> start battle
```

The result is cumbersome for a hero-led light RTS battle where the player should redirect groups during live play or tactical pause.

## Expected Design

Battle preparation is reduced to deployment and current-battle formation. Battle starts in the default attack posture after at least one carried battle group is deployed. Objective-region and engagement-rule selection no longer block launch.

During battle, the player selects one or more battle groups and right-clicks a reachable destination cell. Runtime accepts this as a destination beacon command. Multi-selected groups share the same beacon. Non-selected groups keep their current command, local-combat, fallback, or autonomous state. The same command can be submitted while battle is running or while tactical pause is active; pause-time commands update intent but do not advance simulation until unpaused.

Destination beacons are runtime command target objects, not authored semantic markers and not UI-only pings. They carry command source, owner group references, destination anchor, revision, validity, and reportable failure reasons.

Navigation may use destination-beacon flow fields. A beacon flow field is cache-keyed by beacon id, topology version, height, and footprint/passability profile. It is rebuilt only when the beacon, topology, or profile changes. It must not include living-unit occupancy, same-tick reservations, actor targets, local combat slots, damage state, or Presentation facts. Runtime still commits only neighboring movement steps and validates topology, footprint, occupancy, and reservations before emitting movement events.

Presentation displays selection, invalid-input feedback, accepted beacons, and Runtime rejection reasons. It must not sample flow fields, move actors along uncommitted routes, or create movement truth outside Runtime events.

## Scope

In scope:

- remove mandatory battle-preparation objective and engagement-rule choices;
- define live and tactical-pause destination beacon commands;
- support multi-selected battle groups sharing one destination beacon;
- add command-scoped beacon flow fields to navigation architecture;
- preserve Runtime movement validation and Presentation movement-event authority;
- keep deployment zones as battle-preparation placement constraints;
- keep objective markers as optional tactical semantics for scenarios, AI, reports, and future planning modes.

Out of scope:

- individual soldier micro-control;
- high-frequency per-unit A* or per-unit whole-map optimization;
- dynamic occupancy inside flow-field generation;
- partial multi-select beacon acceptance;
- persistent campaign state for beacons;
- final UI art or icon treatment for beacon visuals.

## Acceptance

This proposal is accepted when authority documents state that:

- battle launch requires deployment, not target-area or engagement-rule selection;
- selected battle groups can receive destination beacon commands during live battle or tactical pause;
- multi-selected battle groups can share one destination beacon;
- non-selected battle groups are unaffected by a beacon command;
- beacon flow fields are allowed only as command-scoped static topology/profile caches;
- Runtime movement commits, occupancy, reservations, attack slots, local combat, and movement events remain authoritative;
- Presentation displays beacons and feedback without owning pathfinding or movement truth.
