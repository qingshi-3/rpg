# Battle Player Action Order

## Context

The battle HUD shows a turn queue, but player control previously behaved as a
free-selection phase. Clicking `结束` on one player unit called
`EndPlayerTurn`, which advanced directly to the enemy phase even when other
player units were still available.

## Change

- `BattleTurnController` now owns the player action order for the current round.
- Ending or waiting a unit completes only that unit, then advances to the next
  actionable player unit.
- The enemy phase starts only after the player action queue is exhausted.
- `BattleCommandController` asks `BattleTurnController` whether a clicked entity
  can become the active unit. Non-current units are blocked with a short hint.

## Runtime Contract

- `BattleTurnController` is the authority for active player unit and remaining
  player action order.
- `BattleCommandController` still owns targeting state and command dispatch, but
  it does not decide player unit order.
- The HUD turn queue reflects the current unit first, then remaining player
  units, then enemy units during player phase.

## Manual Test Points

- With two player units, clicking `结束` on the first unit should auto-select the
  second unit instead of starting enemy phase.
- Clicking another player unit before the current one ends should show a hint and
  should not switch the action menu.
- Enemy phase should start only after all actionable player units have completed.
