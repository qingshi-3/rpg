# Battle Action Menu

## Context

The battle HUD previously used a bottom radial `ActionWheel`. That interaction was rejected because it constrained future tactical UX work and was too costly to maintain for the current Fire Emblem / Wargroove-style command flow.

## Change

- Removed the radial wheel command surface from the battle HUD.
- Removed stale `BattleActionDock.tscn` and `ActionWheelSlot.tscn` resources so the rejected radial wheel does not remain as a broken parallel HUD path.
- Added `BattleActionMenu` as the selected-unit command surface.
- Added `BattleActionMenuButton` as the reusable command button scene.
- Kept HUD commands on the existing `BattleCommandController` route, so UI, map input, and future hotkeys still converge on the same command ids.
- Concrete abilities are shown directly in the action menu. There is no secondary skill wheel in the first version.
- Refined the battle HUD toward a Wargroove-style tactical layout: compact top turn bar, unit-side popup action menu, and non-blocking floating hints.
- Reworked the selected unit card as a resource-authored `PanelContainer` with HP bar and AP pips instead of a C# hand-drawn panel.
- Split command buttons into icon, label, and AP badge nodes so disabled, hover, and active states remain readable without changing battle rules.

## Current Runtime Contract

- `BattleHudRoot` owns HUD layout, positions the action menu beside the selected unit, and forwards selected command ids.
- `BattleActionMenu` owns command button rendering and active/disabled visual state.
- `CommandInfoPanel` remains available as a HUD resource, but selected-unit action choice currently shows only the enabled command list beside the unit.
- `BattleCommandController` remains the only owner of selection stack, targeting state, and action resolution.
- The HUD layout may move or restyle `BattleActionMenu`, `UnitStatusCard`, `CommandInfoPanel`, and `FloatingActionHint`, but it must not consume AP, resolve actions, or advance turns.
- Disabled commands are filtered out of the unit-side popup. Unimplemented commands such as cards or corps must not appear until they are actually selectable.

## Follow-Up

- Replace temporary flat-color battle UI resources with final authored battle UI art when the HUD skin is available.
- Add keyboard/controller navigation after the command vocabulary stabilizes.
- Manual playtest still needs to verify selected-unit popup placement, right-click/Esc cancel, and queue readability in an actual battle.
