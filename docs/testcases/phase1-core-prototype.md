# Phase 1 Core Prototype Test Cases

Applies to:

- `docs/roadmap/development-priority.md`
- `docs/content/tutorial/tutorial-battle.md`
- `docs/design/systems/technical-architecture.md`

## Focus Checks

- [ ] Grid readability: the tutorial battle clearly presents a 6x6 playable grid.
- [ ] Cell occupancy: heroes, minions, enemies, obstacles, and empty cells are visually distinguishable.
- [ ] Turn order: the battle advances through player, troops, and enemies without ambiguous ownership.
- [ ] AP visibility: the player can see current AP before choosing an action.
- [ ] AP cost clarity: action costs are visible before confirmation.
- [ ] Intent readability: each enemy shows its next planned action before it resolves.
- [ ] Intent counterplay: the player can use a hero action to change at least one enemy outcome.
- [ ] Minion predictability: the automatic archer chooses a target in a way the player can infer.
- [ ] Basic ability resolution: move, push, block, and attack effects produce visible results.

## Regression Checks

- [ ] Intent does not change during normal flow unless modified by an Effect.
- [ ] AP remains a shared team resource, not a per-unit resource.
- [ ] Repeated action cost increases remain understandable and visible.
- [ ] Minion rule execution still follows priority order.
- [ ] Enemy actions still resolve from the previously displayed Intent.

## Unverified Checks

- [ ] Whether the current tutorial map scene exactly matches the documented 6x6 coordinate plan.
- [ ] Whether the final UI treatment makes Intent understandable without reading debug text.
- [ ] Whether AP decisions feel meaningful after real playtest pacing is available.

