# TechnicalArchitecture

## Core System Architecture

The battle architecture is built from stable, decoupled systems. Feature work should extend system data and extension points before changing core flow.

Project-level dependency direction and layering are defined in `../architecture/project-architecture.md`.

## CombatSystem

- Turn-based.
- Uses three execution phases: player, troops, enemies.
- Intent must not change mid-flow unless modified by an Effect.

## ResourceSystem

- AP is shared by the whole team.
- All actions consume the same AP resource.
- Repeated actions have increasing cost.

## UnitSystem

- Hero: directly controlled.
- Minion: rule-driven.
- Enemy: Intent-driven.

Long-term control authority is defined outside battle in `../character/unit-talent-rank-control.md`.

Battle should treat direct control as an input authority, not as proof that the unit is important or high rank. Non-direct allied units can still be important characters, officers, elite units, or promoted actors. Their battle behavior should be rule-driven, Intent-driven, or behavior-package-driven and should remain readable to the player.

## CommandSystem

- Each unit has 2 to 3 rules.
- Rules execute by priority.
- Flow: Rule to Decision to Action.
- Non-direct allied units may accept macro commands such as defend, focus fire, conserve, charge, retreat, or wait for charge, but macro commands must modify rules or intent instead of granting full manual control.

## IntentSystem

- Intent is generated in advance.
- Intent must be visualized.
- Stored high-level Intent is not dynamically modified during normal flow unless modified by an Effect.
- Current-state preview and final action resolution are derived from the same stored high-level Intent.
- Intent preview vocabulary is shared with targeting and action preview rules in `targeting-and-preview.md`.
- Detailed runtime rules live in `intent-system.md`.
- The same readability principle should apply to important non-direct allied units. Automatic behavior is acceptable only when the player can understand the plan, influence it through supported commands or effects, and understand the result.

## CardSystem

- Cards consume AP.
- Cards modify rules, behavior, or battlefield state.
- Cards are not the main damage source.

## GridSystem

- Tile-based battle maps.
- Runtime grid state is derived from battle map layers and connection data.
- Supports terrain, height, obstacles, and future traps.

## Battle Scene Architecture

Detailed battle scene and map-layer rules live in `battle-scene-architecture.md`.
Runtime responsibility split after the first closed loop lives in `battle-runtime-responsibility-review.md`.
Battle input and command routing rules live in `battle-input-command-architecture.md`.

Stable summary:

- `WorldSiteRoot` is the generic runtime shell.
- Concrete battle maps are swappable content scenes.
- Presentation may use many `TileMapLayer` nodes.
- Combat logic should collapse map data into one `GridState`.
- Future map loading should prefer `BattleMapDefinition` as the data-driven entry point.

## StatusSystem

Status categories:

- Control.
- Numeric.
- Behavior.
- Command.

## DamageSystem

- Damage uses DamageType plus Tag.
- Damage relationships should avoid cyclic counters.

## Extension Architecture

Detailed action, extension point, and `BattleContext` rules live in `battle-action-architecture.md`.

Stable summary:

- `BattleAction` is composed from Cost, TargetRule, Conditions, and Effects.
- Actions can come from Ability, Card, Rule, or Intent.
- Effects are the main gameplay extension point.
- Effects may use `BattleContext`, but must not directly mutate core system internals.
