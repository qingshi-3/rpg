# TechnicalArchitecture

## Core System Architecture

The battle architecture is built from stable, decoupled systems. Feature work should extend system data and extension points before changing core flow.

Project-level dependency direction and layering are defined in `project-architecture.md`.

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

## CommandSystem

- Each unit has 2 to 3 rules.
- Rules execute by priority.
- Flow: Rule to Decision to Action.

## IntentSystem

- Intent is generated in advance.
- Intent must be visualized.
- Intent is not dynamically modified during normal flow.
- Intent preview vocabulary is shared with targeting and action preview rules in `targeting-and-preview.md`.

## CardSystem

- Cards consume AP.
- Cards modify rules, behavior, or battlefield state.
- Cards are not the main damage source.

## GridSystem

- Start with a 6x6 grid.
- Tile-based.
- Supports obstacles and traps.

## Battle Scene Architecture

Detailed battle scene and map-layer rules live in `battle-scene-architecture.md`.

Stable summary:

- `BattleRoot` is the generic runtime shell.
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
