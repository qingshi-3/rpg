# Battle Technical Index

This directory stores implementation-facing battle architecture, contracts, and extension rules.

## Start Here

- Battle technical architecture: `technical-architecture.md`
- Battle scene and map runtime: `battle-scene-architecture.md`
- Battle action and extension architecture: `battle-action-architecture.md`

## Focused Documents

- Battle runtime responsibility review: `battle-runtime-responsibility-review.md`
- Battle input and command architecture: `battle-input-command-architecture.md`
- Enemy intent runtime contract: `intent-system.md`
- Targeting and preview vocabulary: `targeting-and-preview.md`
- Unit system: `unit-system.md`
- Unit authoring: `unit-authoring.md`
- Unit animation system: `unit-animation-system.md`
- Card system: `card-system.md`

## Rule

Do not change Battle flow, AP, or TurnSystem casually. Prefer extending Effect, Condition, TargetRule, Ability, Card, or Rule definitions.

## Gameplay Counterpart

Player-facing tactical-battle design lives in `../../20-game-design/tactical-battle/`.
