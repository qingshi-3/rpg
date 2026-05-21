# Battle Footprint Navigation And Attack Slots

Status: Archived
Created: 2026-05-21
Accepted: 2026-05-21
Merged: 2026-05-21
Archived: 2026-05-21

Requirement Id: REQ-BATTLE-FOOTPRINT-NAV-ATTACK-SLOTS-2026-05-21
Parent Proposal: `design-proposals/archived/2026-05-20-battle-navigation-topology-decoupling`
Supersedes:
Superseded By:
Amends: `design-proposals/archived/2026-05-20-battle-navigation-topology-decoupling`
Amended By:
Affected Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
Related Implementation Proposals:
- Pending focused implementation proposal under `gameplay-alignment/implementation-proposals/`.

## Reason

The accepted battle navigation split correctly separates map topology compilation from Runtime pathfinding, but the current authority still contains conflicting footprint rules.

The gameplay command detail says large units default to anchor-cell terrain legality, while the runtime/navigation documents say movement, occupancy, reservations, attack range, and diagnostics use covered footprint cells. Recent battle testing also exposed two design-level needs:

- large units must not move as if only their anchor exists;
- large units should support being surrounded by multiple smaller units as a core body-size experience.

This proposal amends the battle navigation requirement chain to make the footprint model explicit across movement and attack.

## Affected Authority Documents

- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Current Design Or Architecture

Current accepted documents already define square-grid anchored realtime battle, per-actor rectangular footprints, dynamic occupancy, same-tick reservations, and footprint-to-footprint attack range.

However, current authority is inconsistent in these places:

- `gameplay-design/details/combat-command/README.md` still says default navigation and terrain legality evaluate only the anchor cell for large units.
- `system-design/battle-navigation-topology-architecture.md` requires covered footprint legality but does not fully name the static placement graph / clearance layer.
- Attack range is footprint-based, but combat-slot generation for large targets is not stated as a general rule.
- AI target choice says "fastest attack opportunity", but does not explicitly define the opportunity as any valid attack slot for the attacker's footprint.

## Expected Design Or Architecture

Expected authority uses one footprint abstraction across movement and attack.

- The actor anchor remains the top-left cell and is the stored runtime position.
- The anchor is not the legality rule by itself. It represents a full rectangular footprint state.
- Static movement legality is based on footprint placement: every covered cell required by the candidate anchor must be present in topology.
- Same-level movement edges are valid only when the actor's footprint can move from source anchor to target anchor without corner cutting or passing through missing terrain.
- Runtime pathfinding may use an anchor graph or flow field, but that graph is already footprint-aware for the actor size.
- Same-tick movement reservation reserves the full candidate footprint, not only the anchor.
- Dynamic living actors are hard blockers for immediate committed movement unless an earlier accepted same-tick mover has already reserved its destination and released its old footprint.
- Released same-tick cells may be entered only after that actor's move is accepted; duplicate reservations, direct opposite-edge swaps, and cells occupied by actors that have not accepted a move remain blocked.
- Future projected occupancy remains soft cost.
- Basic attack range is shortest square-grid distance between attacker footprint and target footprint.
- A valid basic-attack opportunity is any legal attacker anchor whose footprint is within range of the target footprint without overlapping it.
- Large targets naturally expose more valid attack slots and can be surrounded by multiple smaller units, subject to terrain, footprint placement, and reservation rules.

## Follow-Up Implementation Scope

Actual code work must wait for a focused implementation proposal under `gameplay-alignment/implementation-proposals/` after this design proposal is accepted, merged, and archived.

Likely implementation scope:

- compile or cache footprint-aware placement legality / clearance from battle topology;
- validate movement candidates with full covered cells and diagonal swept-footprint rules;
- ensure reservation maps reserve full target footprints and reject duplicate cells, swaps, and invalid overlaps;
- preserve deterministic same-tick follow movement into already released footprints while keeping unresolved tick-start occupants blocked;
- generate combat slots from footprint-to-footprint range instead of hardcoded 8-neighbor cells;
- make ordinary assault target scoring choose the fastest reachable valid attack slot;
- add diagnostics that distinguish static footprint illegality, diagonal clearance failure, dynamic occupancy, reservation rejection, and no reachable attack slot;
- add regression tests for `1x1`, `2x2`, and mixed-size units around terrain, chokepoints, and surround attacks.

## Acceptance

This design proposal is acceptable when:

- the old anchor-only default terrain legality rule is removed from gameplay authority;
- all affected documents agree that anchor is a state identifier while legality uses the full footprint;
- movement rules distinguish static footprint placement from dynamic reservation;
- same-tick release rules explicitly allow follow movement only into footprints vacated by already accepted movers;
- attack rules define combat slots generically for different attacker and target footprint sizes;
- AI target choice references fastest reachable valid attack slot, not just nearest target actor;
- implementation is explicitly deferred to a later technical-change proposal.

## Merge Plan

- `expected/gameplay-design/details/combat-command/README.md` -> `gameplay-design/details/combat-command/README.md`
- `expected/system-design/battle-navigation-topology-architecture.md` -> `system-design/battle-navigation-topology-architecture.md`
- `expected/system-design/battle-runtime-architecture.md` -> `system-design/battle-runtime-architecture.md`
- `expected/system-design/battle-ai-boundary-architecture.md` -> `system-design/battle-ai-boundary-architecture.md`
