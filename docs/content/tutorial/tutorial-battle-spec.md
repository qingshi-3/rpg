# Tutorial Battle Spec

This is the implementation-facing specification for the tutorial battle.

For high-level goals and scope, read `tutorial-battle.md` first.

## Battlefield Coordinates

- Grid size: 6x6.
- Columns use `x = 0..5` from left to right.
- Rows use `y = 0..5` from player side to enemy side.
- Coordinates are written as `(x, y)`.

Suggested layout:

```text
y5  .  .  T  .  .  .
y4  .  .  .  .  S  .
y3  .  .  X  X  .  .
y2  .  .  X  X  .  .
y1  .  W  .  A  .  .
y0  .  .  .  .  .  .
	x0 x1 x2 x3 x4 x5
```

Legend:

- `W`: Warrior hero start.
- `A`: Archer minion start.
- `S`: Skeleton enemy start.
- `T`: Thrower enemy start.
- `X`: blocking obstacle.
- `.`: walkable cell.

Design notes:

- The central `2x2` obstacle creates a clear line-of-sight and pathing lesson.
- The skeleton starts close enough to threaten after movement, but far enough that push or block can matter.
- The thrower starts where ranged Intent is visible before it resolves.
- Do not add traps, height, cards, or alternate objectives in this battle.

## Player Units

### Warrior

Role:

- Demonstrates direct control and battlefield intervention.

Phase 1 abilities:

- Move: reposition within basic movement range.
- Push: move an adjacent enemy by one cell if the destination is valid.
- Block: reduce or cancel one incoming enemy attack if positioned correctly.
- Strike: simple melee attack for confirming damage feedback.

### Archer

Role:

- Demonstrates predictable automatic minion behavior.

Phase 1 rule priority:

1. Attack the nearest enemy in range.
2. Prefer the enemy currently threatening the Warrior if tied.
3. Hold position if no target is in range.

## Enemies

### Skeleton

Role:

- Teaches melee Intent and push counterplay.

Intent examples:

- Move toward the Warrior if not in range.
- Attack the Warrior if adjacent.
- Retarget the Archer only if the Warrior is unreachable.

### Thrower

Role:

- Teaches ranged Intent and positioning.

Intent examples:

- Target the Warrior if line of sight exists.
- Target the Archer if the Warrior is blocked by the central obstacle.
- Reposition only when no valid target exists.

## Tutorial Beat Script

This script is a design target, not a hard requirement for exact AI implementation.

### Round 1

Intent setup:

- Skeleton intends to move toward the Warrior.
- Thrower intends a ranged attack if line of sight exists.

Expected player lesson:

- Select the Warrior.
- See available movement and AP cost.
- Move or block to reduce the next enemy outcome.

Expected system result:

- Archer either attacks the nearest valid enemy or holds if no target is valid.
- Enemies resolve the Intent that was already displayed.

### Round 2

Intent setup:

- Skeleton threatens melee if it reached adjacency.
- Thrower continues showing a clear ranged target.

Expected player lesson:

- Use Push or Block to change the Skeleton outcome.
- Notice that AP spent on prevention limits other actions.

Expected system result:

- If Push makes the Skeleton unable to attack, the shown outcome changes only after the Push Effect resolves.
- Archer behavior remains explainable from its rule priority.

### Round 3

Intent setup:

- At least one enemy should be damaged enough that focus fire matters.

Expected player lesson:

- Combine Warrior positioning with Archer automatic damage.
- Confirm that Intent, AP, and minion rules form one readable loop.

Expected system result:

- The battle can conclude soon after this point without introducing new mechanics.

## Required Previews

- Current AP and action cost before confirmation.
- Warrior movement range.
- Push target and destination validity.
- Block target or protected direction.
- Archer selected target before automatic action resolves, if practical.
- Skeleton melee Intent target.
- Thrower ranged Intent target and affected cell or unit.

Preview vocabulary should follow `docs/design/systems/targeting-and-preview.md`.
