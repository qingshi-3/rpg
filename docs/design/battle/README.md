# Battle Design Index

This directory stores battle runtime, combat interaction, unit, card, intent, and battle-demo design.

## Start Here

- Battle technical architecture: `technical-architecture.md`
- Battle scene and map runtime: `battle-scene-architecture.md`
- Battle action and extension architecture: `battle-action-architecture.md`
- Mechanism battle slice: `mechanism-battle-slice.md`

## Focused Documents

- Battle runtime responsibility review: `battle-runtime-responsibility-review.md`
- Battle input and command architecture: `battle-input-command-architecture.md`
- Battle UI interaction review: `battle-ui-interaction-review.md`
- Enemy intent runtime contract: `intent-system.md`
- Enemy intent gameplay design: `enemy-intent-design.md`
- Targeting and preview vocabulary: `targeting-and-preview.md`
- Unit system: `unit-system.md`
- Unit authoring: `unit-authoring.md`
- Unit animation system: `unit-animation-system.md`
- Cross-system unit rank/control design: `../character/unit-talent-rank-control.md`
- Card system: `card-system.md`
- Battle demo simulation: `battle-demo-undead-commander.md`

## Rule

Do not change Battle flow, AP, or TurnSystem casually. Prefer extending Effect, Condition, TargetRule, Ability, Card, or Rule definitions.
