# LimboAI AI Runtime Boundary Proposal

Status: Archived

## Purpose

Introduce LimboAI as the maintained behavior-authoring surface for tactical AI decisions without moving battle truth, settlement, navigation, occupancy, or world persistence into behavior trees.

This proposal covers the first migration slice: Phase 0 plugin boundary stabilization and Phase 1 battle presentation intent planning. It does not replace `BattleRuntimeSession.ResolveAutonomousCombat` yet.

The accepted integration model is driver-first: LimboAI may decide what an agent wants to do, sequence request/wait/fallback behavior, and react to reported action results, but C# runtime services remain the authority for whether an action is legal and how battle facts change.

## Current Architecture

The accepted battle architecture already assigns tactical AI ownership to local target choice, path following, protect/pursuit/retreat decisions, and safe fallback attribution. The implementation is still scattered:

- `GreedyEnemyIntentPlanner` hardcodes nearest-hostile, range, and strike/pressure intent selection.
- `GreedyAlliedIntentPlanner` hardcodes `Assault`, `FocusFire`, and `HoldLine` intent selection.
- `BattleIntentResolver` remains the C# authority that turns intent into validated action requests.
- `BattleRuntimeSession.ResolveAutonomousCombat` still owns minimal runtime autonomous combat and is not part of the first migration slice.

LimboAI `v1.6.0` is present as a GDExtension under `addons/limboai/`. Because the project is Godot C#, the first implementation must not depend on direct C# custom LimboAI task classes.

## Expected Architecture

Add an AI decision boundary shaped for LimboAI:

- Behavior tree resources live under `assets/ai/battle/`.
- LimboAI custom GDScript tasks live under `scripts/ai/limbo_tasks/battle/`.
- The first C# migration introduces behavior-tree-style decision runners that preserve existing `BattleIntent` output.
- GDScript tasks call a C# `BattleAiFacade` agent and pass the LimboAI blackboard through that Facade.
- The Facade writes only decision and observation variables such as `target_id`, `ability_id`, `intent_power`, `last_action_status`, and `failure_reason`; it does not mutate battle runtime or domain state.
- `BattleIntentResolver` remains the only path that validates and resolves intent into `BattleActionRequest`.
- Runtime AI migration is deferred until the presentation planner slice has regression coverage. After that coverage exists, the next allowed runtime slice is a boundary-only executor injection: Runtime may ask an `IBattleRuntimeAiExecutor` for typed action requests, but Runtime still validates and applies movement, attack, events, and outcomes.

Initial behavior must be equivalent to the old greedy planners. The value of the first slice is maintainable structure and testable boundary, not smarter enemies.

## Migration Boundary Decision

LimboAI is the behavior driver and orchestration surface, not the result authority.

Behavior trees may own:

- Target selection, tactical posture selection, and short-term action sequencing.
- Request/wait/fallback flow such as `select target -> request move -> wait for result -> request attack`.
- Local retry or fallback choice after C# reports `target_lost`, `move_failed`, `attack_resolved`, or similar result facts.
- Blackboard values that describe current decision context or last reported action status.

C# runtime and application services own:

- Movement legality, pathfinding, footprint, dynamic occupancy, and reservation commits.
- Ability legality, cooldown/cost checks, damage, death, event emission, and battle outcome.
- Settlement, battle report facts, persistent state, and world writeback.

Therefore the next migration step should not make LimboAI mutate actor position, HP, occupancy, or outcome. It should introduce a runtime AI executor boundary that lets LimboAI submit typed action requests and consume typed result observations emitted by the existing runtime authority.

## Non-Goals

- No direct mutation of `BattleRuntimeState`, `StrategicWorldState`, or `WorldSiteState` from LimboAI tasks.
- No replacement of battle pathfinding, footprint, occupancy, reservation, damage, outcome, settlement, or world writeback.
- No direct LimboAI replacement of `BattleRuntimeSession.ResolveAutonomousCombat` in this slice. A default C# executor may be injected as a typed request boundary if it preserves the existing runtime behavior and keeps Runtime as the only mutation authority.
- No dependency on LimboAI C# custom task classes until the GDExtension + C# path is proven stable.
- No blackboard persistence of HP, position, occupancy, battle outcome, settlement facts, or world state.

## Acceptance Criteria

- Existing enemy and allied intent choices are preserved by regression tests.
- The first behavior-tree-style planner boundary exists in C# and can later be driven by LimboAI Facade calls.
- A C# `BattleAiFacade` exists as the single GDScript task boundary for LimboAI battle decisions.
- LimboAI tasks pass the blackboard to the Facade instead of storing project truth in script-local state.
- LimboAI battle behavior resources and task scripts are present as authored assets, but they do not own runtime truth.
- `BattleIntentResolver` stays the only action-resolution authority.
- The proposal records the driver-first migration model before the runtime AI executor slice begins.
- The runtime AI executor slice exposes typed request/fact/result boundaries without giving LimboAI or Presentation mutable runtime state.
- Standalone site exploration runtime is removed from the current game direction, so this proposal keeps no patrol-migration phase for that removed subsystem.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` and the relevant battle regression project pass.
