# Site Grid Exploration Implementation Note

## Scope

Add the first grid-based `WorldSite` exploration slice: one party marker moves on the authored site grid, resolves deterministic interest-point actions, can raise alert, and can request battle without using battle turns.

## Acceptance

- `WorldSiteState` persists exploration memory and party cell.
- Movement uses `BattleGridMap` / `MovementRangeFinder`, not physics collisions.
- Exploration input is active only outside battle runtime.
- Battle trigger carries exploration context into `BattleStartRequest`.
- Battle `TurnSystem` and AP do not advance exploration time.

## Out Of Scope

- Multi-unit RTS control.
- Realtime guard AI or vision cones.
- Physics collision movement.
- Randomized stealth checks.
- Full site fog visual polish.

