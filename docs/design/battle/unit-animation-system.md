# Unit Animation System

This document defines the battle unit animation framework. It covers runtime
ownership and authoring contracts only; concrete animation clips are authored by
humans in Godot scenes/resources.

## Goal

Battle units can react visually to common battle presentation cues without
changing Battle flow, AP, TurnSystem, ability resolution, or grid rules.

Runtime code requests animation cues. Unit scenes and resources decide what those
cues look like.

## Runtime Owner

`UnitAnimationComponent` is the per-unit animation playback owner.
`DamageReactionComponent` is the per-unit damage reaction owner. It strongly
depends on `HealthComponent` and `UnitAnimationComponent`; missing dependencies
are scene authoring errors and should fail loudly.

Current cues:

- `idle`
- `move`
- `attack`
- `hit`
- `defeated`

Runtime cues are triggered by their owning presentation systems:

- Movement path starts -> `move`.
- Movement tween finishes -> `idle`.
- Ability or attack action succeeds -> actor `attack`.
- Damage is applied and target survives -> target `hit`, through
  `DamageReactionComponent` reacting to `HealthComponent.Damaged`.
- Unit is defeated -> target `defeated`, then hidden unless the animation set
  says defeated units should remain visible.

`BattleUnitRoot` must not replay target `hit` from `BattleActionResult`; that
would make action-result playback a second authority for damage reactions.

## Authoring Contract

Battle units use one shared entity scene plus resource-driven visual data.

The authoritative runtime entity scene is:

```text
res://scenes/battle/entities/units/BattleUnitBase.tscn
```

It contains the common battle root and components:

```text
BattleEntity
  VisualRoot
    AnimatedSprite2D
  AnimationPlayer
  FactionComponent
  HealthComponent
  ActionPointComponent
  MovementComponent
  AttackComponent
  GridOccupantComponent
  SelectableComponent
  TargetableComponent
  AbilityComponent
  UnitAnimationComponent
  DamageReactionComponent
```

Concrete unit differences are not authored as per-unit scenes. Each
`BattleUnitDefinition` points `Visual` at a `BattleUnitVisualDefinition`
resource, and the factory applies that resource to the fixed
`VisualRoot/AnimatedSprite2D` node in `BattleUnitBase.tscn`.

`UnitAnimationComponent` exports:

- `AnimationPlayerPath`: defaults to `AnimationPlayer`.
- `AnimatedSpritePath`: defaults to `VisualRoot/AnimatedSprite2D`.
- `AnimationSet`: a `BattleUnitAnimationSet` resource.
- `VisualRootPath`: defaults to `VisualRoot`.
- `EnableProceduralFallback`: defaults to false. It is only for temporary debug
  visuals and must not be treated as authored unit animation.

`BattleUnitAnimationSet` stores the animation names for each cue. Artists or
designers may author either `AnimationPlayer` clips or `AnimatedSprite2D`
`SpriteFrames` animations with matching names.

Visual and sprite configuration lives on `BattleUnitVisualDefinition`:

- `SpriteFrames`: assigned to the fixed `AnimatedSprite2D`.
- `AnimationSet`: cue names and playback policy.
- `AutoLayoutFromSpriteFrames`: when true, `BattleUnitFactory` reads all
  `SpriteFrames` textures, calculates visible non-transparent source bounds, and
  fits the sprite to `TargetMaxSpriteSizePixels`. The layout prefers `idle`, then
  `breathing`, then `move`, so attack reach does not shrink the standing unit.
- `TargetMaxSpriteSizePixels`: maximum display size after scaling. The default
  is `40`, and each `visual.tres` may override it.
- `GroundAnchorOffsetPixels`: after scaling, the sprite is moved upward by
  `scaledHeight / 2 - GroundAnchorOffsetPixels`. The default is `5`, keeping the
  unit footprint close to the battle cell center while showing the body above it.
- `VisibleAlphaThreshold`: alpha cutoff for visible-pixel bounds. The default is
  `0.05`, so transparent padding in atlas cells is ignored.
- `Offset` and `Scale`: manual fallback values used only when auto layout is
  disabled or source bounds cannot be read.
- `Modulate`: per-unit sprite tint.

`BattleUnitAnimationSet` stores the animation names for each cue and playback
rules:

- `PreferAnimatedSprite`: when true, `AnimatedSprite2D` is tried before
  `AnimationPlayer`. When false, `AnimationPlayer` remains first for backwards
  compatibility, then `AnimatedSprite2D` is tried before procedural fallback.
- `BalanceSpriteSpeedByFrameCount`: when true, `UnitAnimationComponent` reads
  `SpriteFrames.GetFrameCount()` and the authored animation speed, then applies
  `AnimatedSprite2D.SpeedScale` so long frame sequences fit the cue's target
  duration without rewriting the shared `frames.tres` resource.
- Target durations: `TargetIdleCycleSeconds`, `TargetMoveCycleSeconds`,
  `TargetAttackSeconds`, `TargetHitSeconds`, and `TargetDefeatedSeconds`.
  Defaults are tuned for battle readability: idle `1.2s`, move `0.5s`, attack
  `0.6s`, hit `0.2s`, defeated `0.8s`.
- `MinBalancedSpeedScale` and `MaxBalancedSpeedScale` clamp runtime speed. The
  default minimum is `1.0`, so short authored clips are not slowed down unless a
  unit-specific animation set opts into that.

If no animation set is assigned, the component still tries the default cue names
against the fixed `VisualRoot/AnimatedSprite2D`. If no sprite animation or
compatible `AnimationPlayer` clip is available, it logs a low-noise warning once
per missing cue and does not pretend that the unit has authored animation.
Procedural transform fallback only runs when `EnableProceduralFallback` is
explicitly enabled for temporary debugging.

One-shot `attack` and `hit` cues return to `idle` when the authored animation
finishes and `ReturnToIdleAfterOneShot` is true. `defeated` completes the same
hide callback used by the existing `AnimationPlayer` and procedural paths.
For `AnimatedSprite2D`, configure `attack`, `hit`, and `defeated` as non-looping
animations. `idle` and `move` may loop.

Facing is left/right only and is runtime entity state, not unit configuration.
Current sprite assets are authored facing right. Movement faces the next
horizontal movement segment, attacks face the target, and non-lethal hit cues
face the damage source if the source is horizontally offset. Facing is applied
through `AnimatedSprite2D.FlipH`, not by mutating the configured `Scale`.

## Boundaries

- Do not make animation clips authoritative for gameplay timing.
- Do not spend AP, apply damage, move grid occupants, or end turns from animation
  tracks.
- Do not add unit-specific animation branches to action resolution.
- Do not hardcode concrete sprite sheets, clips, or visual nodes in battle logic.
- Animation resources may be added per unit without changing combat code.

## Manual Checks

- Configure a `BattleUnitVisualDefinition` with `SpriteFrames` animations named
  `idle`, `move`, `attack`, `hit`, and `defeated`.
- Confirm `attack`, `hit`, and `defeated` are non-looping in `SpriteFrames`.
- Optionally assign a `BattleUnitAnimationSet` through
  `BattleUnitVisualDefinition.AnimationSet` when the sprite animation names
  differ from the default cue names.
- Confirm idle starts when the unit is ready.
- Move the unit and confirm move starts, then returns to idle after movement.
- Attack a target and confirm the actor attack cue plays.
- Damage a surviving target and confirm hit plays.
- Defeat a target and confirm defeated plays before hide, or remains visible if
  configured that way.
