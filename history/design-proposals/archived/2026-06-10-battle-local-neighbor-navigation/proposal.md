# Battle Local Neighbor Navigation Proposal

Status: Archived

## Requirement Id

REQ-BATTLE-LOCAL-NEIGHBOR-NAVIGATION-2026-06-10

## Parent Proposal

None.

## Supersedes

None.

## Superseded By

None.

## Amends

Existing accepted battle navigation architecture that allowed first-slice Runtime combat hot paths to use shared route fields and attack-position flow fields.

## Amended By

None.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-10-battle-local-neighbor-navigation.md`

## Current Architecture

The accepted documents still allow Runtime to build shared route fields, attack-position fields, objective fields, and combat-zone goal fields. The code also keeps flow-field construction in combat movement, support-slot movement, objective movement, and reachable-attack-slot checks.

## Expected Architecture

The first playable battle Runtime uses state-machine-owned intent plus bounded local neighbor movement. The state machine chooses objective, target, attack slot, support slot, queue, hold, or return intent. Runtime movement resolution checks nearby legal anchors and chooses a step that reduces distance, respects topology, occupancy, and reservations, and avoids obvious immediate backtracking. If every useful local step is blocked, the actor degrades to queue, support, hold pressure, or an explicit failure reason instead of constructing a flow field.

Obstacle avoidance is a local neighbor-resolution rule, not a global flow-field or whole-map pathfinding authority.

## User Acceptance

The user accepted replacing Runtime flow-field combat movement with state-machine intent plus local neighbor checks on 2026-06-10 with the instruction: "执行，而且绕障其实是单独的算法，和寻路、流场都没关系吧".
