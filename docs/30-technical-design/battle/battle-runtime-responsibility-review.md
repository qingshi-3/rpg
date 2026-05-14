# Battle Runtime Responsibility Review

This document records the target responsibility split after the first battle closed loop.

## Current State

`WorldSiteRoot` currently works as the battle runtime shell and temporary action execution boundary.

Raw left-click parsing has moved to `InputRoot/BattleInputRouter`. Command interpretation, selection state, and the interaction stack have moved to `FlowRoot/BattleCommandController`.

`WorldSiteRoot/UnitRoot` now has a dedicated `BattleUnitRoot` script. It owns battle entity snapshots/lookups, alive-faction enumeration, turn resource restoration, runtime movement blockers, unit motion tweening, and unit-attached intent marker lifecycle.

It still owns these transitional details:

- Action executor and action/AI context construction.
- Map/unit/HUD/controller wiring.
- Low-noise lifecycle logging.
- Temporary unit spawn placement.

This was acceptable to complete the first loop. It should not remain the long-term structure.

## Agreed Direction

### WorldSiteRoot

Keep:

- Scene composition.
- Shared context creation.
- Top-level references.
- Map/unit/HUD/controller wiring.
- Low-noise lifecycle logging.

Move out:

- Selection stack.
- Command interpretation.
- Turn phase details.
- Unit motion presentation.
- Death/outcome detail handling.

Landed: runtime action preview state and highlight application moved to `OverlayRoot/BattlePreviewController`. Command context and player command preview triggers moved to `FlowRoot/BattleCommandController`. Turn phase sequencing, player phase startup, auto-selection, victory/defeat checks, and defeated-entity coordination moved to `FlowRoot/BattleTurnController`. Enemy intent lifecycle, intent resolver execution, intent marker coordination, and enemy phase action entry moved to `FlowRoot/BattleIntentController`.

Transitional boundary: `WorldSiteRoot` still constructs action/AI contexts, owns `BattleActionExecutor`, and wires map/unit/HUD/controllers. Flow controllers call those remaining Root boundaries through injected delegates.

### MapRoot / BattleMapView

Map initialization has moved to `BattleMapView`.

Current behavior:

- `BattleMapView._Ready()` resolves its authored tile layers.
- `BattleMapView.EnsureRuntimeData()` builds and exposes `BattleGridMap` plus the coordinate layer.
- `EnsureRuntimeData()` is idempotent, so `WorldSiteRoot` may call it as a load-order fallback without rebuilding map runtime data.
- `WorldSiteRoot` receives the ready map and stores the exposed `BattleGridMap` and coordinate layer references.

This keeps map-specific initialization with the concrete map scene.

### UnitRoot

UnitRoot should own unit-associated presentation and runtime hosts:

- Battle entities.
- Unit registry or lookup cache.
- Unit motion presentation.
- Intent marker host or unit-attached intent marker lifecycle.
- Defeated unit node cleanup such as hiding, collision disabling, and
  movement-blocker removal. Damage reactions such as non-lethal hit playback
  belong to the unit's `DamageReactionComponent`, not to action-result playback.

Intent rules are system logic, but intent markers are unit presentation.

Current transition status:

- Landed: `BattleUnitRoot` owns unit collection queries, blocked movement surfaces, turn resource restoration, movement tweens, and intent marker lifecycle.
- Landed: `BattleTurnController.HandleEntityDefeated` coordinates defeated-entity handling by clearing intent-controller bookkeeping through a delegate, delegating unit-node presentation to `BattleUnitRoot.MarkEntityDefeated`, and marking battle state changed through a delegate.

### OverlayRoot

Runtime action previews belong under `OverlayRoot`, not map-authored tile layers.

OverlayRoot owns:

- Hover frame.
- Movement range.
- Path and arrows.
- Target/attack/skill highlights.
- Invalid target feedback.

Current transition status:

- Landed: `OverlayRoot/BattlePreviewController` owns movement range/path cache, hover intent preview cache, selected/target/invalid highlight application, and ability range/target highlighting.
- Transitional: `BattleCommandController` coordinates player command preview timing. `BattleIntentController` calls the preview controller for enemy phase execution. `WorldSiteRoot` still triggers battle-state invalidation through wiring delegates.

Map `OverlayLayer` should be treated as authored visual map content unless a specific map needs a visual overlay tile.

### InputRoot

Battle input should be a dedicated system.

First step landed:

- `InputRoot/BattleInputRouter` owns raw left mouse `_UnhandledInput`.
- The router converts valid grid clicks into `BattleCommand`.
- HUD commands are also routed through `BattleCommand`.
- Input code does not spend AP, apply damage, move units, or mutate the selection stack.

See `battle-input-command-architecture.md`.

### FlowRoot

FlowRoot owns battle flow controllers:

- `BattleCommandController`.
- `BattleTurnController`.
- `BattleIntentController`.

These controllers can still be presentation/application-level classes at first. The important part is to remove unrelated details from `WorldSiteRoot`.

Current transition status:

- Landed: `BattleCommandController` owns selected entity state, interaction stack, command dispatch, map/entity click interpretation, HUD command handling, move/ability targeting entry, wait/end resolution, and player move/ability resolution.
- Landed: `BattleTurnController` owns enemy phase state, round count, enemy phase sequencing, player phase startup, auto-selection, victory/defeat checks, and defeated-entity coordination.
- Landed: `BattleIntentController` owns enemy intent dictionaries, enemy intent planning, intent resolver execution for enemy phase, intent marker setup/cleanup coordination, and the enemy action execution entry used by `BattleTurnController`.
- Transitional: `WorldSiteRoot` still owns `ExecuteActionRequest`, action context construction, map/unit/HUD wiring, and low-noise lifecycle logs. Flow controllers reach those boundaries through injected delegates rather than reading root fields directly.

### Temporary Unit Placement

Current unit placement is temporary.

The long-term source of truth should be explicit map or encounter configuration, for example:

```text
BattleEncounterDefinition
  -> BattleMapDefinition
  -> UnitSpawnDefinition[]
```

or site-map-local configuration:

```text
BonefieldSite
  -> BattleMapSpawnConfig
  -> UnitSpawnDefinition[]
```

Scene node positions and hardcoded `GridOccupantComponent` values should not remain the final encounter authoring workflow.

## Logging Rule

`WorldSiteRoot` may keep lifecycle logs:

- Map loaded.
- Battle started.
- Phase started.
- Outcome resolved.
- Critical wiring failure.

High-frequency logs must stay behind explicit debug flags:

- Per-frame hover.
- Pointer move.
- Repeated preview recomputation.
- Camera movement every frame.
