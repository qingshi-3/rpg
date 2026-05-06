# Intent System

This document defines enemy Intent as a stable gameplay contract.

For player-facing intent presentation rules, use `enemy-intent-design.md`.

## Purpose

Intent exists so the player can read and change the future.

Enemy Intent is not a cached concrete action. It is a committed tactical posture that can be previewed and later resolved against the current battlefield state.

## Runtime Model

The current runtime model is:

```text
IEnemyIntentPlanner
  -> BattleIntent
  -> BattleIntentController
  -> BattleIntentResolver.Preview(...)
  -> BattleIntentResolver.Resolve(...)
  -> BattleActionRequest
  -> BattleActionExecutor
```

Responsibilities:

- `IEnemyIntentPlanner` chooses a high-level intent template for each enemy.
- `BattleIntent` stores tactical posture, target policy, preferred ability, and display value.
- `FlowRoot/BattleIntentController` owns enemy intent generation, stored intent bookkeeping, marker setup/cleanup, hover-preview lookup, and enemy phase execution entry.
- `BattleIntentResolver` produces current-state previews and final action requests from the same stored intent.
- `BattleActionExecutor` remains the only gameplay mutation path.

## Intent Versus Action

Intent may say:

- Pressure the nearest hostile unit.
- Strike the nearest hostile unit.
- Apply ranged pressure.
- Hold because no useful action exists.

Intent should not directly store:

- Exact movement destination.
- Exact path.
- Exact attack request.

Those are resolved from current battlefield state when previewed or executed.

## Preview Rules

- Overhead markers show the committed high-level intent.
- Markers render semantic icon keys through `BattleIntentIcons`; AI and resolver logic should not reference texture paths directly.
- Hover preview may show the current predicted path, target, affected cells, and damage estimate.
- Preview can change after the player changes the battlefield, but only inside the stored intent's policy.
- Normal player movement should not make the enemy reroll a totally unrelated strategy.
- Effects may explicitly modify intent; that is a gameplay event and should refresh preview after resolution.

## Icon Catalog

Intent marker art lives under `assets/textures/ui/intent-icons/`.

The main intent icon catalog covers these semantic keys:

`attack`, `advance`, `defense`, `support`, `control`, `mobility`, `summon`, `charge`, `retreat`, `unknown`.

Details such as poison, armor break, lifesteal, area targeting, and multi-target behavior are not head marker intent categories. They should be represented in hover details or future secondary tags.

Use semantic keys on templates so later art replacement does not affect AI planning or intent resolution.

## Behavior Tree Boundary

Future behavior trees may choose or update intent templates.

Behavior tree actions must not directly mutate HP, AP, grid position, death state, or turn flow. They should choose intent or submit action requests through the shared battle action boundary.

## Phase 1 Templates

- `melee_pressure`: move toward the nearest hostile unit if not in range.
- `direct_strike`: attack the nearest hostile unit when already in range.
- `ranged_pressure`: attack if possible, otherwise reposition toward a valid ranged pressure position.
- `hold`: no useful action under current policy.
