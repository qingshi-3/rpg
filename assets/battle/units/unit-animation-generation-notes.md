# Unit Animation Generation Notes

This directory records durable notes for extending unit pixel animations from existing battle spritesheets.
It is asset-pipeline memory, not gameplay design authority and not runtime architecture.

## Scope

- Use these notes when adding new unit action frames such as cast, skill, taunt, guard, or impact variants.
- Prefer producing new preview assets first. Do not overwrite the source spritesheet until the result is accepted.
- Keep gameplay rules, runtime timing, and `SpriteFrames` wiring out of this directory unless the task explicitly moves from asset production into implementation.
- This directory covers unit-bound animation frames and unit-bound casting VFX. Released projectiles, target impacts, AoE markers, persistent fields, and other world-space skill effects are separate FX assets.

## Source Constraints

- Inspect the existing `SpriteFrames` or atlas regions before drawing. For the current battle unit packages, common regions are `100x100` cells inside a larger transparent spritesheet.
- Preserve the unit's visual identity: silhouette, armor colors, weapon shape, highlight colors, and pixel density should come from existing frames.
- Treat the standing foot position as the anchor. Body motion can change, but the character should not appear to slide between frames.
- If the source is under the external asset library, copy it into the project before editing. Never modify the external library in place.

## Recommended Workflow

1. Find stable source poses from the existing atlas: neutral, windup, raised weapon, swing, recovery, and return-to-neutral.
2. Describe what the source action is doing before adding effects: thrust, slash, overhead chop, ranged shot, cast chant, guard raise, recoil, or recovery.
3. Build a short action strip as a new file, usually `N x 100` by `100` for `N` frames when matching current unit cells.
4. Use existing body poses first, then make small pixel-level edits only where needed. This keeps anatomy and style more stable than fully redrawing the unit.
5. Add only caster-bound VFX as a separate visual layer conceptually: hand glow, weapon glow, local charge motes, foot aura, spell core near the body, and residual caster particles.
6. Generate an enlarged nearest-neighbor preview and, when useful, a GIF preview. Preview files are review aids, not runtime assets.
7. After visual acceptance, wire the action into `SpriteFrames` or other Godot resources as a separate implementation step.

## Unit-Bound And Released FX Boundary

Skill presentation has two asset responsibilities:

- Unit-bound cast animation: belongs to the unit spritesheet or `SpriteFrames` animation. It shows what the caster's body, weapon, mount, hands, and local aura do while releasing the skill.
- Released skill effect: belongs to a separate FX asset. It shows what leaves the caster or appears in world space, such as projectile trails, target impact bursts, AoE telegraphs, ground fields, beams, chains, shields on another actor, or persistent hazards.

Do not bake released effects into the unit action frames. A unit strip may show a small muzzle flash, hand glow, weapon flare, or local release accent so the cast reads, but the projectile, explosion, AoE shape, target marker, and lingering field should be generated and integrated separately.

This boundary keeps one cast animation reusable across multiple skill effects and prevents source-unit frames from carrying combat truth such as target direction, range shape, or impact timing.

## Pose And Anchor Rules

- Keep each frame inside its fixed cell. Avoid effects that depend on pixels outside the intended atlas region unless the final asset format explicitly supports that.
- Body motion should read clearly but stay modest for small units: crouch, shoulder shift, weapon lift, torso twist, release, recover.
- Maintain the same bottom-foot row for the body where possible. Ground rings or spell effects may extend lower inside the cell, but should not change the perceived standing point.
- Prefer a 5-8 frame sequence for first-pass skill actions. Six frames is a good default:
  `neutral -> windup -> focus -> release -> recover -> neutral`.
- Reuse source poses with matching bottom bounds before attempting manual limb repainting.

## VFX Style Rules

- Match the existing unit palette. For Baast champion-style units, blue, cyan, violet, and dark violet fit the weapon highlights.
- Keep effects pixel-crisp: draw integer-position lines, arcs, sparks, and rings; scale previews with nearest-neighbor.
- Choose the caster-bound VFX shape from the source action semantics. Do not default every skill cast to expanding circles.
- Use unit-bound VFX to clarify cast timing:
  - early frames: sparse motes and small arcs;
  - focus frame: compact spell core or denser orbit;
  - release frame: strongest local hand, weapon, or foot-aura accent;
  - recovery frames: fading caster-local residue.
- Keep beams, thrown slash waves, large explosion bursts, target hit sparks, AoE circles, and persistent fields out of the unit strip. Build them as released skill FX.
- Avoid soft painterly gradients unless the source unit already uses them. Pixel units usually read better with stepped colors and small highlight clusters.

## Action-Semantic VFX Mapping

Use the source animation's physical action as the first filter for local VFX:

- Slash / cleave: use a blade-following crescent, edge glow, or short near-weapon arc. The arc should grow with the swing, peak at the release or contact frame, then shrink into a trailing residue during recovery.
- Overhead chop: use a vertical or diagonal wedge, brief impact glint near the weapon head, and downward sparks. Avoid round radial expansion unless the skill is explicitly shockwave-like.
- Thrust / lance / stab: use a narrow forward line, point flash, and compressed speed streaks aligned to the weapon. Do not use wide circles.
- Ranged shot: use hand, bow, staff, muzzle, or weapon-tip charge plus a tiny launch flash. The projectile trail belongs to released skill FX.
- Cast chant / stationary magic: use hand glow, compact spell core, orbiting motes, or small local runes. This is where circular or ring-like local effects are most appropriate.
- Guard / shield: use a close shield rim, braced glint, or half-dome attached to the unit. Full protective area or shield on another actor belongs to released skill FX.

For attack-derived skill casts, keep the frame count aligned with the source attack when possible. If the attack is a cleave, the local VFX should also read as a cleave: small pre-glow, expanding weapon-following slash, peak slash frame, then shrinking trail. A generic expanding circle is a poor fit unless the source action already reads as a centered pulse.

## Output Naming

- New action strips should use descriptive sibling names, for example:
  `f1_baastchampion_cast_blueviolet_pose.png`.
- Preview files should include `preview` and their scale or format, for example:
  `f1_baastchampion_cast_blueviolet_pose_preview_x4.png`
  or `f1_baastchampion_cast_blueviolet_pose_preview_x4.gif`.
- Do not replace the original spritesheet until the accepted integration plan calls for it.

## Acceptance Checklist

- The official action strip is transparent RGBA.
- Frame dimensions match the intended atlas cell size and count.
- Character identity remains stable across frames.
- Body foot anchor does not visibly slide.
- Unit-bound VFX stays inside the cell or inside the agreed output bounds.
- Released projectiles, target impacts, AoE telegraphs, and persistent fields are not baked into the unit strip.
- Local VFX shape matches the identified source action, such as cleave for cleave frames or thrust streak for thrust frames.
- The release frame is readable at in-game scale, not only in enlarged preview.
- The animation still reads when played at likely Godot speeds such as 8-12 FPS.
- Preview assets are clearly separated from runtime assets.

## Current Learning

The most promising path is not pure VFX over one idle frame and not full AI redraw.
The stronger workflow is hybrid:

- reuse existing atlas body poses with compatible anchors;
- make restrained pixel-level pose edits when needed;
- layer caster-local action VFX on top;
- move released skill effects into separate FX assets;
- review as both a frame strip and an animated preview before resource integration.
