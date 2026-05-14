# Battle Action Architecture

This document defines the stable execution abstraction for battle gameplay.

## BattleAction

The stable abstraction for gameplay execution is `BattleAction`.

`BattleAction` is composed from:

- Cost.
- TargetRule.
- Conditions.
- Effects.

`BattleAction` can come from:

- Ability: hero actions.
- Card: card actions.
- Rule: minion actions.
- Intent: enemy actions.

## Runtime Request Boundary

Runtime actors do not directly mutate battle state.

The stable runtime boundary is:

- AI, behavior trees, UI, cards, and abilities create or submit a `BattleActionRequest`.
- `BattleActionExecutor` validates and applies the request.
- `WorldSiteRoot` coordinates turn flow, HUD feedback, highlights, and scene placement.

Behavior tree Action nodes may initiate gameplay actions, but they should call the shared action executor instead of directly changing HP, AP, grid position, visibility, or death state.

This keeps future behavior tree plugins replaceable: a plugin can own decision flow, while battle rules remain in the project action/rule layer.

Current lightweight implementation:

- `BattleActionRequest`: requested action such as Move or Attack.
- `BattleActionExecutor`: shared validation and state mutation for player and enemy actions.
- `BattleRuleQueries`: shared read-only rule checks such as hostility, defeat, AP, and attack legality.
- `IEnemyIntentPlanner`: enemy high-level intent decision interface; can later be backed by behavior trees.
- `BattleIntentResolver`: converts stored high-level intent into preview data and final `BattleActionRequest`.

## Extension Points

Targeting, preview, and final resolution should use the shared vocabulary defined in `targeting-and-preview.md`.

### Effect

Effect is the core extension point. Complex gameplay logic should live in Effect implementations, not in core systems.

Supported Effect categories:

- Damage.
- Move.
- Push.
- ApplyStatus.
- ModifyRule.
- ModifyIntent.
- ModifyAP.

### TargetRule

Supported TargetRule categories:

- Range.
- Adjacent.
- Area.
- Line.
- Self.

### Condition

Supported Condition categories:

- Sufficient AP.
- Target exists.
- Status check.

## BattleContext

`BattleContext` is the service layer exposed to Effects.

It provides:

- UnitService.
- GridService.
- APService.
- StatusService.
- RuleService.
- IntentService.

Effects may call `BattleContext`, but should not directly modify core system internals.
