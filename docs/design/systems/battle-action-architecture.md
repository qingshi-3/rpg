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
