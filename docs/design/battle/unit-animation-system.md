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

`UnitAnimationComponent` is the per-unit presentation owner.

Current cues:

- `idle`
- `move`
- `attack`
- `hit`
- `defeated`

`BattleUnitRoot` triggers cues from existing presentation events:

- Movement path starts -> `move`.
- Movement tween finishes -> `idle`.
- Ability or attack action succeeds -> actor `attack`.
- Damage is applied and target survives -> target `hit`.
- Unit is defeated -> target `defeated`, then hidden unless the animation set
  says defeated units should remain visible.

## Authoring Contract

Each battle unit scene may contain:

```text
BattleEntity
  AnimationPlayer
  UnitAnimationComponent
```

`UnitAnimationComponent` exports:

- `AnimationPlayerPath`: defaults to `AnimationPlayer`.
- `AnimationSet`: a `BattleUnitAnimationSet` resource.

`BattleUnitAnimationSet` stores the animation names for each cue. Artists or
designers author actual `AnimationPlayer` clips and assign the matching resource
on the component.

If no animation set is assigned, the component is inert. This is allowed while
art configuration is pending. If an animation set is assigned but the matching
clip is missing, the component logs a low-noise warning once per missing cue.

## Boundaries

- Do not make animation clips authoritative for gameplay timing.
- Do not spend AP, apply damage, move grid occupants, or end turns from animation
  tracks.
- Do not add unit-specific animation branches to action resolution.
- Do not hardcode concrete sprite sheets, clips, or visual nodes in battle logic.
- Animation resources may be added per unit without changing combat code.

## Manual Checks

- Configure a unit scene with an `AnimationPlayer` and `BattleUnitAnimationSet`.
- Confirm idle starts when the unit is ready.
- Move the unit and confirm move starts, then returns to idle after movement.
- Attack a target and confirm the actor attack cue plays.
- Damage a surviving target and confirm hit plays.
- Defeat a target and confirm defeated plays before hide, or remains visible if
  configured that way.
