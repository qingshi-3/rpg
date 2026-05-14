# Battle Intent System

## Background And Goal

The product direction depends on readable enemy Intent. Enemy behavior should be previewed during the player turn, then resolved during the enemy phase from a committed high-level intent.

The implementation should make enemy behavior feel planned without freezing enemies into a dumb exact move chosen before the player acts.

## Non-Goals

- Do not implement cards, complex rules, hidden information, or full behavior trees.
- Do not redesign AP or the turn system.
- Do not add animation configuration.
- Do not make hover threat range the primary enemy preview.

## Affected Systems

- `BattleRoot`: generates, stores, previews, and resolves enemy intents.
- `GreedyEnemyIntentPlanner`: chooses the first set of high-level intent templates.
- `BattleIntentResolver`: turns the stored high-level intent into current-state preview and execution requests.
- `BattleIntentMarker`: displays the committed Intent above enemy units.
- `BattleGridHighlightOverlay`: remains an auxiliary hover preview layer for paths and target cells.
- `BattleActionExecutor`: continues to be the only state mutation path.

## Shared Rule Impact

- Intent stores tactical posture, target policy, preferred ability, and a display value.
- Intent does not store a `BattleActionRequest`.
- Hover preview resolves the stored intent against the current battlefield state.
- Enemy phase resolves the same stored intent again against the current battlefield state, then submits the resulting request to `BattleActionExecutor`.
- If the player changes the battlefield, the predicted path or target may change within the intent's policy. The enemy should not reroll a totally unrelated strategy.

## Implementation Steps

1. Add high-level intent templates such as pressure, strike, ranged pressure, and hold.
2. Generate enemy intents at the start of each player phase.
3. Show committed intent as a simple overhead marker with an icon and key value.
4. Resolve hover details with path, target, and affected cells from the current state.
5. Resolve stored intents again during the enemy phase.
6. Keep all final mutation through `BattleActionExecutor`.

## Risks And Rollback

- The first pass uses simple target policies and may need richer templates later.
- Rollback is local: bypass `IEnemyIntentPlanner` and `BattleIntentResolver`, then submit enemy `BattleActionRequest` directly through `BattleActionExecutor`.

## Manual Acceptance Checks

- At player phase start, logs show enemy intents generated.
- Hovering an enemy shows the stored high-level intent plus current predicted result.
- Enemy phase resolves the same stored intent and submits exactly one resulting request per enemy.
- Moving, blocking, or killing a target can alter the prediction inside the same intent policy without causing a fresh unrelated strategy.
