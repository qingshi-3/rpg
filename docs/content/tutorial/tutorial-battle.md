# Tutorial Battle

This is the first vertical-slice battle. It should prove the Phase 1 combat loop before the project adds cards, progression, or larger content scale.

Detailed coordinates, unit behavior, beat scripts, and preview requirements live in `tutorial-battle-spec.md`.

## Goals

- Teach Intent.
- Teach positioning.
- Teach direct hero control.
- Teach automatic minion behavior.
- Teach that AP is a shared team resource.

## Battlefield Summary

- Compact grid-based encounter.
- Central obstacle.
- Warrior starts on the player side.
- Automatic Archer is planned for the RuleSystem/minion phase, not the current minimal loop.
- Skeleton and Thrower start on the enemy side.
- Full coordinate plan lives in `tutorial-battle-spec.md`.

## Player Side

- Warrior: directly controlled.
- Archer: planned automatic minion.

## Enemies

- Skeleton: melee.
- Thrower: ranged.

## Key Experiences

- Move to change enemy Intent outcomes.
- Attack and defeat enemies.
- Later RuleSystem work will add automatic minion behavior.
- Spend AP on either direct damage or prevention, not both every turn.
- Read enemy Intent before committing the Warrior action.

## Success Checks

- The player understands what each enemy is about to do.
- The player can change at least one enemy outcome with Warrior positioning.
- Future Archer automatic behavior is predictable from its rule priority.
- AP creates a visible tradeoff between attack, movement, and prevention.
- The battle teaches only the Phase 1 loop and does not introduce cards or progression.

## Excluded From This Battle

- Cards.
- Complex rules.
- Attribute system.
- Equipment.
- Progression rewards.
- Multi-wave encounters.
- Hidden information.
