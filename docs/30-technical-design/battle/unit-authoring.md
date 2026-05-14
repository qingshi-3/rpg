# Battle Unit Authoring

This document defines the stable authoring contract for battle units. It is for
adding or changing unit content without creating another runtime authority.

## Runtime Structure

`assets/battle/units/<category>_<semantic-name>/unit.tres` is the preferred
unit definition entry point. Folder prefixes should be `首领_`, `中立_`, or
`f1_` through `f6_`; rare source terms may keep the original word after a close
Chinese semantic label, with `异界<原词>` as the fallback when no better semantic
translation is known. Legacy flat definitions under
`assets/battle/units/*.tres` remain loadable for compatibility.

`BattleUnitDefinition` resources and the nested unit-definition path index are
resident metadata. They are shared by the battle scene, strategic-world UI, and
site-operation UI so a world detail click does not rescan unit packages after
scene changes. Runtime scene nodes, instantiated `BattleEntity` objects, and
site-specific placement/render caches are not resident and should still be
rebuilt from current state when their scene is entered.

## Display Names

`BattleUnitDefinition.DisplayName` is the Chinese semantic label for the source
visual identity. Prefer the Duelyst source card name when it can be mapped from
the package sprite or `frames.tres`; otherwise use a close visual/resource
semantic translation. Do not use the technical `Id`, folder name, or encounter
role as the player-facing unit label.

Runtime battle entities append a two-digit visible instance suffix through
`BattleUnitDisplayNameFormatter`, such as `盾牌铸造者01` and `盾牌铸造者02`.
The base resource label stays unsuffixed in `unit.tres` so repeated instances can
share one authored definition.

Bulk DisplayName refresh is handled by
`tools/apply-battle-unit-display-names.mjs`. It may read the local Duelyst source
checkout to map RSX animation aliases back to original card names, then writes
`assets/battle/units/_display_name_translation_report.json` for audit. Directory
renaming is deferred; stable IDs and resource paths remain authoritative until a
separate migration updates all references.

Each `BattleUnitDefinition` should use:

- `Visual`: the package-local `visual.tres`.
- Stat fields on the definition for HP, AP, movement, targeting, blocking, and
  legacy attack compatibility.
- `Abilities`: configured `AbilityDefinition` resources. A normal attack should
  use `Id = "basic_attack"` while legacy command compatibility remains active.

Each generated unit package should contain:

- `<unit_id>.png`
- `<unit_id>.png.import`
- `frames.tres`
- `visual.tres`
- `unit.tres`

Bulk package generation from staged PNG files is handled by
`tools/generate-battle-unit-packages.mjs`. Duelyst atlas frames are regenerated
from plist metadata with `tools/generate-battle-unit-frames-from-plist.mjs`; set
`PLIST_ROOT` when the external plist directory is not in the default local path.

`BattleUnitBase.tscn` is the only complete battle unit component scene. It owns
the shared `BattleEntity`, `VisualRoot`, fixed `VisualRoot/AnimatedSprite2D`,
`AnimationPlayer`, combat components, `AbilityComponent`, and
`UnitAnimationComponent`.

## Visual Resources

`BattleUnitVisualDefinition` is the resource-driven visual entry point. It stores:

- `SpriteFrames`: assigned to the fixed `AnimatedSprite2D`.
- `AnimationSet`: cue names and playback policy.
- `AutoLayoutFromSpriteFrames`: default true. Runtime reads visible
  non-transparent `SpriteFrames` bounds and calculates scale and vertical
  position. It prefers `idle`, then `breathing`, then `move`.
- `TargetMaxSpriteSizePixels`: maximum post-scale visual size. Default `40`,
  and this value is exposed per `visual.tres`.
- `GroundAnchorOffsetPixels`: default `5`. Runtime moves the sprite upward by
  `scaledHeight / 2 - 5` pixels after scale is calculated.
- `VisibleAlphaThreshold`: default `0.05`; transparent padding inside atlas
  cells is ignored when calculating layout bounds.
- `Offset` and `Scale`: manual fallback values used only when auto layout is
  disabled or source bounds cannot be read.
- `Modulate`: per-unit sprite tint, usually `Colors.White` for final art.

Unit definitions should not reference per-unit visual scenes. Concrete unit
scenes that remain under `scenes/battle/entities/units/` are legacy visual
sketches only and are not authoritative for new battle units.

The package-local `frames.tres` should reference the package-local PNG, not the
old `assets/textures/units/` source path. For Duelyst atlas assets, `frames.tres`
should use plist regions as the source of truth. Standard runtime aliases are
`idle`, `move`, `attack`, `hit`, and `defeated`; additional plist cues should be
kept with their original plist cue names.

`Visual` and `SpriteFrames` are required. Missing visual resources are treated as
content errors and the unit should not be created.

The `SpriteFrames` resource should define these animation names:

- `idle`
- `move`
- `attack`
- `hit`
- `defeated`

Configure `attack`, `hit`, and `defeated` as non-looping animations. `idle` and
`move` may loop.

Units only support left/right facing for now. Movement faces the next horizontal
segment, attacks face the target, and hit reactions face the damage source when
the source is horizontally offset. Do not author separate up/down animations
until the runtime facing model is expanded.

Do not duplicate `BattleUnitBase.tscn` for each unit. Unit differences should be
expressed through unit definitions, ability resources, and visual resources.

## Animation

`UnitAnimationComponent` lives on `BattleUnitBase.tscn`. Unit-specific animation
configuration is assigned through `BattleUnitDefinition.Visual.AnimationSet`.

`AnimatedSprite2D` is the primary playback backend. `AnimationPlayer` remains
available for compatibility or broad presentation tracks. Procedural transform
fallback remains available for temporary debugging; missing sprite frames should
be fixed in the unit visual resource.

Animation cues, runtime boundaries, and manual checks are documented in
`unit-animation-system.md`.

Animation tracks are presentation only. They must not spend AP, apply damage,
move grid occupants, or end turns.

## Adding A Unit

1. Create a folder under `assets/battle/units/` named with the category prefix
   plus business semantic label, such as `f1_盾牌铸造者`.
2. Move the source PNG and its `.import` file into that folder.
3. Create `frames.tres` with the standard cue animations and bind it to the
   package-local PNG. For Duelyst atlas assets, prefer regenerating frames from
   plist metadata instead of hand-slicing the sheet.
4. Create `visual.tres`, assign `frames.tres`, assign or reuse a
   `BattleUnitAnimationSet`, and keep auto layout enabled unless this unit needs
   a special visual size or anchor.
5. Create `unit.tres`, set `Id` to the stable ASCII unit id, and set `Visual` to
   the package-local `visual.tres`.
6. Configure display name, stats, targetability, movement, blocking, and
   abilities.
7. Open the resources in Godot and check that all references resolve.

Keep unit-specific behavior data-driven. If a new gameplay behavior is needed,
prefer adding or configuring `AbilityDefinition`, `TargetRule`, `Condition`, or
`Effect` resources before changing battle runtime code.
