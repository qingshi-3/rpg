# Battle Runtime Behavior Tree Executor Implementation Proposal

Status: Archived - accepted; remaining presentation-backed bonefield manual QA waived by user request on 2026-06-06

## Requirement Id

BT-RUNTIME-2026-06-05

## Originating Design Proposal

None. This slice implements the accepted LimboAI decision boundary already recorded in `system-design/battle-ai-boundary-architecture.md`.

## Accepted Authority

- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

## Goal

Replace the default Runtime AI executor's hardcoded decision chain with a behavior-tree-shaped executor that reads typed Runtime facts, selects a target through reusable behavior-tree nodes, evaluates condition/action nodes at actor decision boundaries, and outputs typed Runtime action requests without owning movement, attack, damage, occupancy, or settlement truth.

## Scope

- Add a small Runtime behavior-tree execution layer under `src/Runtime/Battle/AI` using Selector, Sequence, Condition, and Action semantics compatible with LimboAI's authored tree model.
- Keep `IBattleRuntimeAiExecutor` as the Runtime decision boundary.
- Make `DefaultBattleRuntimeAiExecutor` delegate to the behavior-tree executor instead of embedding tactical policy as a flat service chain.
- Move default target selection into behavior-tree facts and nodes:

```text
immediate in-range enemy
-> valid retained target
-> reachable local-combat enemy
-> plan/region-scoped nearest enemy
-> no target
```

- Runtime may build target candidate facts from tick-start actor facts, command scope, combat-zone scope, and cached navigation observations. It must not preselect the ordinary target before invoking the behavior-tree executor.
- Preserve the first-slice action decision order after target selection:

```text
invalid actor -> hold
no target -> hold
outside local-combat leash -> hold with rejection reason
target already in attack range + charged -> attack
target already in attack range + not charged -> wait for charge
out of attack range + reachable attack slot -> join local combat
out of attack range + reachable support slot -> hold support without continuous re-slotting once the support anchor is reached
out of attack range + no local-combat slot -> explicit local-combat hold
out of attack range -> advance toward target
```

- Keep combat-zone construction, local-combat slot solving, movement validation, attack validation, damage, and events in existing Runtime services.
- Add behavior-level tests that prove the Runtime executor is a behavior tree and preserves the current decision semantics.
- Keep Godot/LimboAI resources as the authored Presentation-facing surface; do not require headless Runtime tests to instantiate Godot nodes or LimboAI resources.

## Non-Goals

- Do not move HP, movement, attack legality, pathfinding, occupancy, reservations, or settlement into behavior-tree nodes.
- Do not route headless Runtime tests through Godot scene nodes or GDScript tasks.
- Do not redesign attack-position generation in this slice.
- Do not change combat-zone or group-action-zone algorithms.
- Do not add formation-wide target assignment, focus-fire coordination, threat tables, or per-unit advanced targeting strategies in this slice. The default strategy is nearest valid enemy with immediate attack and retained-target priority.
- Do not add new player-visible UI.

## Touched Systems

- Runtime AI executor and behavior-tree node model.
- Runtime target candidate facts and action request construction boundary.
- Runtime AI behavior regression tests.
- Existing Runtime session injection of `IBattleRuntimeAiExecutor`.

## Tests

- Runtime AI behavior tree structure regression: the default executor source must delegate to a tree executor and must not reintroduce a flat tactical if/else chain.
- Behavior-node semantics regression: Selector stops at the first successful child; Sequence fails at the first failed condition/action.
- Runtime decision regressions:
  - invalid or targetless actors hold with named reasons;
  - line-contact actors select immediate or nearest frontage targets through behavior-tree facts instead of all falling through to actor-id tie-breaking;
  - target selection inside the default Runtime flow is not performed by `BattleRuntimeTickResolver` before the behavior-tree executor;
  - local-combat leash rejection wins before slot decisions;
  - actors already inside attack range attack or wait before asking for a local-combat slot;
  - reachable attack slot requests `JoinLocalCombat`;
  - reachable support slot requests `HoldSupport`, and an actor already at a support anchor holds instead of continuously selecting new support movement;
  - no reachable local-combat slot holds with `local_region_degrade_no_reachable_slot`;
  - out-of-range non-local combat advances toward the retained target;
  - in-range charged actors attack;
  - in-range uncharged actors wait for charge.
- Runtime boundary regression: executor facts still expose only primitive/immutable decision facts and not mutable Runtime authority objects.

## Diagnostics

No new high-frequency logs are required for this slice. Existing action-result diagnostics should still show the selected target, request kind, and reason emitted by the executor. If a behavior node cannot produce a request, the executor must return an explicit hold reason rather than a null request.

## Manual QA

- Run the bonefield presentation-backed battle after automated tests.
- Confirm the battle no longer stalls from per-tick decision churn.
- Confirm units at immediate attack range attack at the next anchored decision boundary instead of wandering due to AI request selection.
- Use existing area snapshots and action-result logs to confirm behavior-tree decisions remain explainable by request kind and reason.

## Acceptance Evidence

- 2026-06-05 TDD RED: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` failed because `Rpg.Runtime.Battle.AI.BehaviorTree` did not exist after behavior-tree boundary tests were added.
- 2026-06-05 runtime behavior-tree verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed after `DefaultBattleRuntimeAiExecutor` delegated to `BattleRuntimeBehaviorTreeExecutor` and the new Selector/Sequence/Condition/Action tests passed.
- 2026-06-05 build: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-05 patch check: `git diff --check` passed.
- 2026-06-05 TDD RED: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` failed because `BattleRuntimeAiDecisionFacts.TargetCandidates` and `BattleRuntimeAiTargetCandidateFacts` did not exist after behavior-tree target-selection tests were added.
- 2026-06-05 target-selection boundary verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed after Runtime tick resolution switched from preselected targets to scoped target candidate facts consumed by behavior-tree target selection.
- 2026-06-05 build: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-05 patch check: `git diff --check` passed.
- 2026-06-06 archive decision: user requested archiving this proposal and waived the remaining presentation-backed bonefield manual QA. No new runtime/code changes were made for this archival step.
