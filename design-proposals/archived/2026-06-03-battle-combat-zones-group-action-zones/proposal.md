# Battle Combat Zones And Group Action Zones Proposal

Status: Archived

## Requirement Id

BCZ-GAZ-2026-06-03

## Parent Proposal

`design-proposals/archived/2026-06-03-battle-group-layered-runtime/`

## Supersedes

None.

## Superseded By

None.

## Amends

`design-proposals/archived/2026-06-03-battle-group-layered-runtime/`

## Amended By

None.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-03-battle-combat-zones-group-action-zones.md`

## Current Architecture

Runtime observation can group actors by commander id and can move engaged actors through group-owned local combat regions. In practice, the local-combat region is still attached to a battle group and each actor can fall back to actor-local target selection and objective movement. A group may enter engagement while rear members continue ordinary objective advance or later stall with no named combat role.

Diagnostics log state transitions and blocked ingress reasons, but they do not emit one complete area snapshot that shows all combat zones, deployment zones, group-owned movement intent, and unit positions together.

## Expected Architecture

Runtime owns two distinct area concepts:

- `CombatZone`: a global battlefield observation fact. It is computed from all living units, factions, footprints, contact/perception/attack relations, and clustering rules. It is not owned by any unit or battle group.
- `GroupActionZone`: a commander-group-owned intent fact. It describes where that group is currently moving, joining, holding, supporting, retreating, or regrouping. Other groups and units may observe it, but only the owning commander mutates it.

Units outside a relevant `CombatZone` use region movement toward a selected zone or group objective. Units inside a `CombatZone` use local combat assignment to choose attack slots, support slots, queue positions, flank/regroup positions, or attack actions. Actor state machines execute typed intents only; they do not decide whether a group joins a combat zone.

Every `CombatZone` build and every periodic `GroupActionZone` rebuild emits a low-noise full area snapshot: all combat-zone bounds, all authored deployment-zone bounds, all group action-zone bounds, and all living unit positions with footprints and current high-level state.

## Acceptance

The design is accepted by user direction in the 2026-06-03 battle debugging session:

- global combat zones are independent battlefield facts;
- group action zones are commander-group-owned intent regions;
- ordinary movement and combat-entry movement share Runtime validation but have separate intent semantics;
- area rebuild diagnostics must print complete zone and unit distribution snapshots.
