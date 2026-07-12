# Battle Group Layered Runtime Proposal

Status: Archived

## Requirement Id

BG-LAYERED-RUNTIME-2026-06-03

## Parent Proposal

None

## Supersedes

None

## Superseded By

None

## Amends

- `2026-05-23-battle-plan-state-machine`
- `2026-05-27-local-combat-situation-ai`
- `2026-05-29-enemy-region-directed-combat-ai`

## Amended By

None

## Affected Authority Documents

- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-command-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-03-battle-group-layered-runtime.md`

## Current Architecture

The accepted architecture already names battle groups, actor phases, tactical regions, and player-intent precedence. The implementation investigation showed that those contracts need sharper separation:

- runtime action phases are actor-level and valid;
- perception facts are already independent observations;
- group plan state can still be interpreted or implemented as actor-level state;
- local combat can become fragmented when runtime grouping follows individual force-count rows instead of the player-visible hero company;
- player-commanded groups can reuse tactical facts but need a clear player-scoped engagement path that does not let enemy policy overwrite player intent;
- local combat reachability must distinguish a statically reachable attack slot from an executable next step.

## Expected Architecture

Runtime battle behavior is explicitly layered:

```text
observation facts
-> battle-group commander state
-> actor action state machine
-> runtime validation
```

Observation runs continuously and produces perception, contact, local-combat, and reachability facts without owning intent. Battle-group commander state owns plan progression, engagement, target-lock scope, local-combat assignment, regroup, and retreat decisions. Actor action state machines own only low-level movement, attack, recovery, holding, interruption, and defeat phases. Runtime validators remain the only movement, attack, damage, occupancy, reservation, and event authority.

Runtime grouping must align with the selectable hero company or accepted battle-group command identity. Visible force counts may create multiple runtime actors, but they must not silently create independent commander states when the player-facing model is one hero-led group.

## Acceptance

Accepted by the user on 2026-06-03 with the instruction to optimize around this layering and prevent cross-module coupling.
