# Battle Runtime Action Clock Contract

Status: Accepted
Created: 2026-05-20
Accepted: 2026-05-20
Merged: 2026-05-20
Archived: 2026-05-20

Requirement Id: REQ-BATTLE-RUNTIME-ACTION-CLOCK-CONTRACT
Parent Proposal: `design-proposals/archived/2026-05-20-battle-navigation-topology-decoupling`
Supersedes: None
Superseded By: None
Amends:
- `design-proposals/archived/2026-05-20-battle-navigation-topology-decoupling`
- `design-proposals/archived/2026-05-20-battle-system-authority-doc-split`
Amended By:
Affected Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`
Related Implementation Proposals:
- `docs/50-production/technical-changes/2026-05-20-battle-navigation-runtime-refactor.md`

## Reason

The navigation/topology split correctly requires pathfinding to replan from live facts, but it did not state the action-clock boundary strongly enough. Implementation could read "every tick recalculates" as permission to reset actors to `AnchoredDecision`, emit repeated attacks on consecutive ticks, and precompute the full battle outcome before Presentation plays the first visible actions.

This proposal closes that gap by making actor action completion the decision boundary for movement, attacks, and command consumption.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`

## Current Design Or Architecture

Accepted docs already say Runtime owns live battle truth, actor phases, command state, semantic events, and pathfinding decisions. Navigation docs already say Runtime commits only one neighbor move per actor action and replans at decision boundaries. Command docs already say commands influence Runtime but do not own pathfinding.

The missing contract is that a simulation tick is not itself an action decision boundary. Actors in movement, attack recovery, casting, interruption, or another non-decision phase must remain locked until Runtime reaches the action completion boundary.

## Expected Design Or Architecture

Runtime execution is driven by actor state-machine action boundaries.

- Presentation-backed battle must not simulate the whole battle to completion before playback starts.
- Runtime advances in deterministic slices bounded by movement completion, attack impact or recovery completion, command interruption, path failure, or battle termination.
- Actors in non-decision phases do not replan movement, reacquire targets, or start another attack just because another tick elapsed.
- Basic attacks emit at most one damage application per attack action.
- Presentation may acknowledge action playback completion, but Runtime owns the semantic action boundary.
- Commands are consumed at valid actor decision boundaries or explicit interrupt boundaries.

## Follow-Up Implementation Scope

- Update the active runtime/navigation implementation proposal to require action-clock regressions.
- Refactor `BattleRuntimeSession` and `BattleRuntimeTickResolver` so presentation-backed battle advances by action slices instead of precomputing a full completed event stream.
- Update `BattleRuntimeActorStateMachine` so phase advancement does not blindly reset living actors to `AnchoredDecision`.
- Add tests for one damage per attack action, recovery persistence, no replanning while action-locked, and presentation-backed runtime not starting from a fully completed battle outcome.

## Acceptance

- Authority docs state that action state-machine completion, not raw tick elapsed, creates movement and attack decision boundaries.
- Authority docs state that Presentation-backed runtime cannot precompute the full battle outcome before playback.
- The related implementation proposal references this contract and includes tests for action-clock behavior.

## Merge Plan

- Merge `expected/system-design/battle-runtime-architecture.md` to `system-design/battle-runtime-architecture.md`.
- Merge `expected/system-design/battle-navigation-topology-architecture.md` to `system-design/battle-navigation-topology-architecture.md`.
- Merge `expected/system-design/battle-command-architecture.md` to `system-design/battle-command-architecture.md`.
