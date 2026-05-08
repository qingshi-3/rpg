# Battle Input And Command Architecture

This document defines the target architecture for battle input, command interpretation, and command dispatch.

It exists because battle input needs a stable route from raw device events to semantic commands and command business handling. Raw left-click parsing has moved to `InputRoot/BattleInputRouter`, and command interpretation plus selection stack ownership has moved to `FlowRoot/BattleCommandController`.

## Design Goal

Battle input should be:

- Context-aware: the same click can mean select, move confirm, ability confirm, or UI interaction depending on mode.
- Deterministic: input creates explicit commands, not hidden side effects.
- Device-agnostic: mouse, keyboard, controller, and UI buttons should converge into the same command vocabulary.
- UI-safe: UI focus and action-menu interaction must consume input before map interaction sees it.
- Debuggable: key transitions are logged, but high-frequency hover and per-frame input are not logged by default.
- Replaceable: future input rebinding, controller navigation, and accessibility settings should not require rewriting battle rules.

## Non-Goals

- Do not redesign `BattleActionExecutor`.
- Do not move damage, AP spending, or movement validation into input code.
- Do not make UI controls directly mutate battle state.
- Do not make `WorldSiteRoot` the long-term owner of input state.
- Do not implement a global input framework for the entire game in this document. This is the battle-local architecture.

## Industry Pattern

Mature game input usually separates these layers:

```text
Device Input
  -> Action Map
  -> Input Context / Mode
  -> World Picking
  -> Semantic Command
  -> Flow / Controller
  -> Rules / Executor
  -> Presentation Feedback
```

The important rule is that raw input is not gameplay. A mouse click, key press, or controller confirm should become a semantic command before it can affect battle state.

Examples:

- Left mouse button is raw input.
- `Confirm` is an input action.
- `ConfirmMove(destination)` is a battle command.
- `BattleActionRequest.Move(actor, destination)` is a gameplay action request.

## Target Node Layout

The battle scene should move toward this shape:

```text
WorldSiteRoot
├─ MapRoot
│  └─ BattleMapView
├─ UnitRoot
│  ├─ BattleEntity nodes
│  ├─ UnitMotionController
│  └─ IntentMarkerHost / unit-attached intent markers
├─ OverlayRoot
│  ├─ GridHighlightOverlay
│  ├─ PathPreviewOverlay
│  └─ TargetPreviewOverlay
├─ InputRoot
│  └─ BattleInputRouter
├─ FlowRoot
│  ├─ BattleCommandController
│  ├─ BattleTurnController
│  ├─ BattleIntentController
│  └─ BattlePreviewController
├─ Camera2D
└─ CanvasLayer
   └─ BattleHudRoot
```

`WorldSiteRoot` remains the composition root. It wires dependencies, owns shared context creation, and coordinates scene lifetime. It should not stay the long-term owner of raw input handling.

## Core Components

### BattleInputRouter

Owns raw Godot input for battle.

Responsibilities:

- Listen to `_UnhandledInput` or a Godot input callback at `InputRoot`.
- Normalize Godot events into battle input actions.
- Respect UI focus and consumed events.
- Track pointer position in screen/global coordinates.
- Emit normalized input events to the command interpreter.

It should not:

- Select units.
- Spend AP.
- Execute movement.
- Mutate interaction state directly.
- Know ability rules.

Example normalized actions:

```text
PointerMove
PointerPrimaryPressed
PointerSecondaryPressed
PointerMiddleDrag
Confirm
Cancel
OpenActionMenu
CameraMove
CameraZoom
SelectNextUnit
EndTurn
```

### BattleInputContextStack

Owns input mode priority.

Battle needs a stack because commands nest:

```text
Neutral
  -> UnitSelected
    -> ActionMenu
    -> MoveTargeting
    -> AbilityTargeting
```

Cancel behavior should pop the stack:

- Right click or `Esc` in `MoveTargeting` returns to `UnitSelected`.
- Right click or `Esc` in `UnitSelected` clears selection and returns to `Neutral`.

Only the top context should interpret confirm/cancel. Lower contexts may observe hover updates if they are explicitly allowed.

Suggested context types:

```text
NeutralContext
UnitSelectedContext
ActionMenuContext
MoveTargetingContext
AbilityTargetingContext
CardTargetingContext
MinionCommandContext
EnemyIntentInspectContext
ModalUiContext
```

### BattlePointerPicker

Translates pointer position into battle-world targets.

Responsibilities:

- Convert pointer position to `GridPosition`.
- Find top relevant `BattleEntity` at a grid position.
- Prefer unit/entity selection by battle logic, not physics collision.
- Return a stable pick result object.

Example:

```csharp
public sealed class BattlePickResult
{
    public GridPosition? Cell { get; }
    public BattleEntity Entity { get; }
    public bool IsUiBlocked { get; }
}
```

It should not:

- Decide what clicking means.
- Execute actions.
- Show highlights.

### BattleCommand

The semantic command vocabulary.

Input and UI should both produce these commands instead of calling battle methods directly.

Examples:

```text
HoverCell(cell)
HoverEntity(entity)
SelectEntity(entity)
ClearSelection
OpenActionMenu(entity)
ChooseCommand(commandId)
CancelCurrentLayer
ConfirmMove(destination)
ConfirmAbilityTarget(target, abilityId)
WaitUnit(entity)
EndPlayerTurn
InspectEnemyIntent(enemy)
CameraPan(delta)
CameraZoom(delta)
ToggleDebugOverlay(debugId)
```

Command objects should be small and serializable enough for logging/debugging.

### BattleCommandController

Interprets commands and owns interaction flow.

Responsibilities:

- Maintain or delegate selection state.
- Maintain the battle interaction stack.
- Decide whether a command is legal in the current context.
- Call preview controller when entering targeting states.
- Submit final gameplay requests to `BattleActionExecutor`.
- Update HUD through presenter-style methods.

It should not:

- Directly calculate pathfinding internals.
- Directly draw highlights.
- Directly mutate HP/AP except through action execution.

### BattlePreviewController

Owns preview state and overlay updates.

Responsibilities:

- Movement range preview.
- Movement path preview.
- Ability target preview.
- Intent hover preview.
- Clearing previews when context changes.
- Avoiding per-frame recomputation when hovered target and battle state are unchanged.

It talks to:

- `MovementRangeFinder`.
- `BattleAbilityQueries`.
- `BattleIntentResolver`.
- `GridHighlightOverlay`.
- HUD hint presenter.

It should not execute actions.

### BattleTurnController

Owns turn flow and phase transitions.

Responsibilities:

- Begin player phase.
- Begin enemy phase.
- Restore turn resources.
- Ask intent controller to generate intents.
- Sequence enemy resolution.
- Evaluate victory/defeat.
- Handle high-level death/outcome presentation after action results.

`WorldSiteRoot` may call into this controller, but should not own all phase details long term.

### BattleIntentController

Owns enemy intent lifecycle.

Responsibilities:

- Generate intents at player phase start.
- Store enemy intent state.
- Create/remove/update intent markers through the unit presentation layer.
- Resolve stored intents during enemy phase.
- Provide intent preview data to `BattlePreviewController`.

Intent presentation belongs close to units. The marker can remain attached to each `BattleEntity`, or live in a `UnitRoot/IntentMarkerHost` that follows unit positions. Intent rule resolution should remain system/controller logic, not marker logic.

### UnitMotionController

Owns unit movement presentation.

Responsibilities:

- Convert grid path to world positions.
- Run movement tweens.
- Report whether unit animation is active.
- Keep movement visuals out of command and turn flow code.

The gameplay position is still changed through action execution. The motion controller only presents that change.

## UI And Input Relationship

HUD input and map input must converge into the same command path.

```text
BattleActionMenu button click
  -> BattleCommand.ChooseCommand("move")
  -> BattleCommandController

Keyboard hotkey M
  -> BattleInputRouter
  -> BattleCommand.ChooseCommand("move")
  -> BattleCommandController
```

This prevents UI and keyboard/mouse from becoming two separate gameplay paths.

Rules:

- HUD controls emit command requests.
- HUD controls do not call `BattleActionExecutor`.
- HUD controls do not directly change selection stack.
- UI focus consumes confirm/cancel where appropriate.
- Right click and `Esc` should produce the same `CancelCurrentLayer` command unless a modal UI consumes them first.

## Camera Input

Camera input should be separated from battle command input.

Recommended model:

- `BattleCameraController` owns camera movement and zoom behavior.
- `BattleInputRouter` or Godot InputMap provides normalized camera actions.
- Camera input has lower priority than modal UI but can remain active during neutral/unit-selected states.
- Middle mouse drag is a camera command unless a modal UI or drag operation captures it.

Camera movement should not depend on selected unit or targeting context unless a future feature explicitly requests camera lock-on.

## Input Context Examples

### Neutral Hover Enemy

```text
PointerMove
  -> BattlePointerPicker
  -> HoverEntity(enemy)
  -> BattlePreviewController.ShowIntentPreview(enemy)
```

No selection state changes.

### Select Unit

```text
PointerPrimaryPressed on player unit
  -> SelectEntity(unit)
  -> UnitSelectedContext pushed
  -> HUD shows unit status and action menu
  -> Selected overlay appears
```

### Choose Move

```text
ChooseCommand("move")
  -> MoveTargetingContext pushed
  -> BattlePreviewController.ShowMovementRange(unit)
```

### Confirm Move

```text
PointerPrimaryPressed on valid cell
  -> ConfirmMove(cell)
  -> BattleActionRequest.Move(unit, cell)
  -> BattleActionExecutor.Execute(...)
  -> BattleTurnController/CommandController handles result
  -> UnitMotionController animates path
  -> Preview cleared
```

### Cancel Targeting

```text
RightClick or Esc
  -> CancelCurrentLayer
  -> MoveTargetingContext popped
  -> Movement preview cleared
  -> UnitSelectedContext remains
```

### Ability Command

```text
ChooseCommand("ability:push")
  -> AbilityTargetingContext pushed
  -> BattlePreviewController.ShowAbilityTargetHighlight(unit, ability)
```

The action menu is presentation. The command stack is system state.

## Logging Policy

Input logging should be low-noise.

Allowed by default:

- Selection changed.
- Input context pushed/popped.
- Command chosen.
- Action confirmed.
- Command rejected with reason.

Not allowed by default:

- Every pointer move.
- Every hover recomputation.
- Every frame with unchanged target.
- Camera movement every frame.

High-frequency diagnostics require an explicit debug flag.

## Migration Plan

### Step 1: Introduce Command Entry - Landed

- Added `BattleCommandKind` and `BattleCommand`.
- Added `InputRoot/BattleInputRouter`.
- Moved raw left mouse `_UnhandledInput` parsing out of `WorldSiteRoot`.
- `BattleInputRouter` converts valid grid clicks into `BattleCommand.GridCellClicked`.
- HUD command events convert to `BattleCommand.HudCommandSelected` and `BattleCommand.HudCommandCancelled`.
- Initial temporary dispatch in `WorldSiteRoot` has since been replaced by `FlowRoot/BattleCommandController`.
- `WorldSiteRoot` still owns action execution, outcome evaluation, and enemy phase transition boundaries.

### Step 2: Extract BattleCommandController - Landed

- Added `FlowRoot/BattleCommandController`.
- Moved selection state, interaction stack, HUD command interpretation, grid/entity click handling, move targeting, ability targeting, wait/end command handling, and player command resolution out of `WorldSiteRoot`.
- Kept `WorldSiteRoot.ExecuteActionRequest`, battle outcome evaluation, and enemy phase transition as root-owned delegates for now.
- HUD and `BattleInputRouter` both submit `BattleCommand` to `BattleCommandController`.
- Raw input and command business handling are now separate.

### Step 3: Extract BattlePreviewController - Landed

- `OverlayRoot/BattlePreviewController` owns movement range, path preview, ability target preview, intent hover preview, selected/target/invalid highlight application, and clear behavior.
- `BattleCommandController` decides when command context enters or exits targeting and asks the preview controller for visual feedback.
- `WorldSiteRoot._Process` only forwards preview update state exposed by `BattleCommandController`.

### Step 4: Extract Turn And Unit Presentation

- Move turn phase sequencing to `BattleTurnController`.
- Move movement tween presentation to `UnitMotionController`.
- Move intent marker lifecycle close to UnitRoot through `BattleIntentController` or marker host.

## Acceptance Criteria

- `WorldSiteRoot` no longer reads raw mouse clicks directly.
- UI buttons, hotkeys, and mouse clicks produce the same semantic command type.
- Cancel behavior is stack-based and predictable.
- Hover preview does not recompute every frame when the pick result and battle state are unchanged.
- Input code never spends AP, applies damage, kills units, or changes grid position directly.
- Command logs are useful and not high-frequency spam.
