# Battle Player Autonomous Target Regions Proposal

Status: Archived

## Requirement Id

REQ-BATTLE-PLAYER-AUTONOMOUS-TARGET-REGIONS-2026-06-10

## Parent Proposal

None.

## Supersedes

None.

## Superseded By

None.

## Amends

- `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai/`
- `design-proposals/archived/2026-06-03-battle-group-layered-runtime/`
- `design-proposals/archived/2026-06-10-battle-hierarchical-route-hints/`

## Amended By

None.

## Affected Authority Documents

- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-10-battle-player-autonomous-target-regions.md`

## Current Architecture

Runtime already separates player-commanded battle groups from enemy region policy. Enemy offense and active defense may replace empty fixed regions with temporary target regions built from opposing clusters. Player-commanded groups are protected from that mutation so enemy policy cannot rewrite player intent.

That protection is correct, but it leaves a missing state for player groups whose authored or player-selected objective has been completed and no new player command exists. They may hold or keep stale objective facts instead of starting a new purposeful advance toward the next relevant fight.

The current state model also uses one selected region as both effective movement target and intent memory. It does not explicitly distinguish player-authored command intent from a self-calculated fallback target.

## Expected Architecture

Battle-group tactical state distinguishes three command layers:

```text
current execution command
-> player command
-> self-calculated command
```

The current execution command is the state-machine action being consumed now, such as combat join, attack, support, movement, hold, cast, or return. It may temporarily replace what the UI or diagnostics show during combat, but it is not long-term player intent.

The player command is created only by player input or an accepted player battle plan. It has the highest priority and is cleared only when its objective is completed. Automatic combat response may ignore its display while the group is engaged, but must not erase it merely because combat started.

The self-calculated command is a safe fallback. It may be generated only when the battle group has no active player command and the group is allowed to act autonomously. It selects an opposing cluster as a temporary target region, using enemy count as the primary score, total HP as the next tie-breaker, and distance as a later tie-breaker. Runtime then consumes the region through the existing route topology and local movement validation.

When a group using a self-calculated target enters combat, the self-calculated target is cleared and current execution becomes combat-local. After combat ends, if no player command exists and no hostile remains in local scope, the group may compute a new self-calculated target. When a self-calculated target is reached and no relevant opposing unit remains there, it is cleared and the group may compute the next one.

## User Acceptance

The user accepted this direction on 2026-06-10 and clarified that player commands are created only by player action, self-calculated commands are allowed only without a player command, and combat should display combat-step commands while preserving player command truth until completion.
