# LimboAI AI Runtime Boundary Proposal

Status: Implementing

## Purpose

Introduce LimboAI as the maintained behavior-authoring surface for tactical AI decisions without moving battle truth, settlement, navigation, occupancy, or world persistence into behavior trees.

This proposal covers the first migration slice: Phase 0 plugin boundary stabilization and Phase 1 battle presentation intent planning. It does not replace `BattleRuntimeSession.ResolveAutonomousCombat` yet.

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
- The Facade writes only decision variables such as `target_id`, `ability_id`, and `intent_power`; it does not mutate battle runtime or domain state.
- `BattleIntentResolver` remains the only path that validates and resolves intent into `BattleActionRequest`.
- Runtime AI migration is deferred until the presentation planner slice has regression coverage.

Initial behavior must be equivalent to the old greedy planners. The value of the first slice is maintainable structure and testable boundary, not smarter enemies.

## Non-Goals

- No direct mutation of `BattleRuntimeState`, `StrategicWorldState`, or `WorldSiteState` from LimboAI tasks.
- No replacement of battle pathfinding, footprint, occupancy, reservation, damage, outcome, settlement, or world writeback.
- No direct `BattleRuntimeSession.ResolveAutonomousCombat` migration in this slice.
- No dependency on LimboAI C# custom task classes until the GDExtension + C# path is proven stable.

## Acceptance Criteria

- Existing enemy and allied intent choices are preserved by regression tests.
- The first behavior-tree-style planner boundary exists in C# and can later be driven by LimboAI Facade calls.
- A C# `BattleAiFacade` exists as the single GDScript task boundary for LimboAI battle decisions.
- LimboAI tasks pass the blackboard to the Facade instead of storing project truth in script-local state.
- LimboAI battle behavior resources and task scripts are present as authored assets, but they do not own runtime truth.
- `BattleIntentResolver` stays the only action-resolution authority.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` and the relevant battle regression project pass.
