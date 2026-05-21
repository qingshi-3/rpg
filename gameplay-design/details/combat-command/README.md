# Combat Command Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the combat model and battle report sections.

## Boundary

This detail area defines player-facing battle command rules:

- selecting a hero company;
- separating hero command, corps command, and combined command;
- hero hold/attack/retreat/skill behavior;
- corps advance/guard/hold/attack/retreat behavior;
- command cadence for medium-frequency light RTS;
- command feedback and battle-report implications.

## To Refine

- Post-v0.1 command list beyond battle start.
- Command UI expectations after the first playable slice.
- What commands are allowed while the hero or corps is routed, casting, stunned, retreating, or separated.
- How command state is explained in the battle report.

## V0.1 Playable Slice

The first playable slice proves entry into hero-led battle without requiring the full light RTS command UI.

Required player flow:

1. The player clicks `出征` on the world map.
2. The player selects a hero in a Chinese expedition panel.
3. The selected hero brings a default corps.
4. The player right-clicks an enemy strategic location.
5. The hero company travels on the world map toward the enemy strategic location.
6. After arrival, the player chooses to enter the assault battle.
7. The game enters pre-battle deployment.
8. The player confirms or places the hero company in a valid deployment zone.
9. The player clicks `开战`.
10. Real-time battle starts.

V0.1 does not require post-start hero/corps command controls. Those controls remain the next combat-command iteration after this playable path is usable.

## Square-Grid Realtime Battle Contract

The live battle space uses square-grid anchored realtime combat. The player still deploys units onto valid square cells, and the battle map keeps its existing square grid. After the battle starts, units move and fight automatically inside the current command or default posture.

Battle movement is realtime presentation over grid authority:

- Each unit has an anchored square cell and may reserve a next square cell while moving.
- Movement is displayed as smooth travel between cell centers.
- A unit may path toward targets in any valid square-grid direction. The first expected implementation uses 8-neighbor movement unless map data forbids diagonal transitions.
- Runtime builds the static navigation graph once for the battle handoff. Static graph legality owns terrain, walkable surfaces, height-specific surfaces, and authored height connections.
- Runtime may plan several cells ahead, but it commits only the next neighbor cell. The next tick recalculates from current actor, target, occupancy, reservations, and command posture.
- Without explicit AI override, ordinary assault chooses the enemy and attack slot that can enter valid basic-attack execution soonest. Retained targets and nearby support positioning are stability preferences, not permission to ignore a faster reachable attack opportunity.
- When the target is already engaged, nearby support should reinforce the local fight if a valid attack slot is reachable. Support positioning is a fallback when direct attack slots are unavailable or tactically constrained.
- Living-unit occupancy is layered: the immediate next footprint is a hard blocker, while future occupied cells are soft route cost. This keeps a moving actor from treating another unit as a permanent wall, while still making comparable open routes more attractive.
- Same-tick reservations are hard blockers for the next committed footprint and reject opposite-edge swaps.
- A unit cannot perform a basic attack while in transit between cells.
- A unit may attack once it is anchored and the target actor is within the ability's grid range.

This model is not a side-scrolling lane battle and does not restrict units to attacking only forward enemies. It is also not freeform physics RTS movement. The square cell remains the readable tactical position and command surface.

## Ability Targeting Direction

Ability content should support multiple targeting styles without changing the battle identity:

- Unit target: lock onto one battle actor.
- Cell target: lock onto a square cell.
- Direction target: cast toward a resolved direction.
- Self-centered: resolve from the caster.

Direction handling is part of the ability definition. Common direction modes are free angle, 8-way square-grid snap, 4-way cardinal snap, and forward arc. Area handling is also definition-driven, with shapes such as single actor, single cell, line, cone, circle radius, and grid radius.

The first implementation only needs basic actor-target attacks and the data-contract extension points for future abilities. Full projectile and area-skill behavior can be added later through these target and direction contracts.

## Unit Footprints

Larger units may use grid footprints while keeping the square-grid realtime model. A unit footprint is a rectangular set of occupied cells such as `1x1`, `1x2`, `2x1`, `2x2`, or `3x3`.

The anchor cell is always the footprint's top-left cell. The anchor is a compact state identifier, not the terrain legality rule by itself. Movement, deployment, attack range, area overlap, occupancy, and reservation resolve by expanding the anchor into the full covered footprint.

Static navigation legality is footprint-aware. A candidate anchor is legal only when every covered cell required by that footprint exists in the compiled battle topology. Same-level movement between anchors is legal only when the actor footprint can move from source anchor to target anchor without cutting blocked corners or passing through missing covered terrain. Runtime may still path over an anchor graph or flow field, but that graph must already represent valid footprint placement for the actor size.

Unit body collision is stricter than terrain navigation. Other units cannot occupy or reserve any cell inside the committed candidate footprint. During a live movement tick, each actor proposes one next anchor cell, reserves the full candidate footprint, and cannot directly swap edges with another same-tick mover. Tick-start occupied cells remain blocked for immediate committed movement throughout that tick, even when the occupying actor also moves during the same tick. A released footprint may be entered only after Runtime advances to a later decision boundary with updated occupancy. Future projected cells may include occupied cells as soft cost, not hard legality, because those units may move before the actor reaches that part of the route.

During pre-battle deployment, the existing hover selection frame should resize around every cell covered by the dragged unit's footprint. The dragged sprite should stay centered on that footprint and only move to the next anchor after the pointer crosses the half-cell threshold around the current footprint center. Drop validation uses that same covered-cell set.

Attacks and area effects should read the footprint instead of pretending the unit only occupies its anchor:

- Basic attack range uses shortest square-grid distance between attacker footprint and target footprint.
- Actor-target attacks still lock onto an actor.
- A valid basic-attack position is any legal attacker anchor whose full footprint is within range of the target footprint and does not overlap it.
- Larger targets naturally expose more valid attack positions. Multiple smaller units may surround and attack a larger unit when terrain, placement legality, occupancy, and reservations allow it.
- Area effects hit when at least one covered cell overlaps the target footprint.

This makes large units readable as multi-cell bodies without forcing the runtime into freeform physics movement. Navigation remains anchored, but every anchor represents a full footprint state.

Visual size supports this readability, but it is not the occupancy rule. Larger units should scale their sprite uniformly from a tuned footprint size signal instead of stretching differently on X and Y.

## Spatial Tactical Mechanics Direction

Combat is a low-frequency spatial realtime tactical battle. The player should make commander-level choices about local fronts, terrain, reserves, and skill timing. The player should not control individual soldiers or win through high-frequency input.

Maps are the first combat asset. The same unit roster should play differently on bridges, mountain passes, forests, city gates, alleys, high ground, and multi-entrance maps. If terrain does not change tactical value, the combat design is failing its core direction.

The first validation scene should be one strategy scenario before expanding content breadth. A bridge assault or bridge defense is the preferred slice because it can test chokepoint value, delay, reserve timing, ranged pressure, enemy telegraphing, retreat, and spatial skills in one readable setup.

## Corps Tactical Roles

Corps should stay low-complexity and readable through battlefield responsibility:

- spear: chokepoint and anti-charge pressure;
- shield: holding, protection, and frontline stability;
- archer: ranged pressure and area denial;
- cavalry: flank pressure, pursuit, and interruption;
- mage: battlefield area changes and timing pressure.

Individual corps types may gain special rules later, but a corps should not need several active skills to communicate its role.

## Skill And Time Rules

Hero and corps skills should change space, time, or battle state instead of acting as plain damage buttons.

Useful skill categories:

- terrain control such as ice wall, earth wall, fire field, and temporary blockers;
- area suppression such as arrow rain, artillery, poison, and denial zones;
- formation disruption such as charge, knockback, shock, and break-line effects;
- tempo control such as slow, freeze, root, and delayed cast pressure;
- frontline stabilization such as shields, war drums, guard stance, and recovery windows.

Time is a tactical resource. Delaying should matter when it enables reinforcements, cooldown recovery, setup completion, flanking arrival, or a breakthrough elsewhere.

## AI Telegraphing And Reports

Enemy AI should be stable and readable before it is clever. Strong enemy behavior must telegraph intent, such as longbow preparation, cavalry charge setup, or mage channeling.

The battle report should explain meaningful outcomes through terrain, timing, reserves, command, skill use, and local collapse. Reports should avoid source-less randomness when the real cause was a chokepoint failure, late retreat, unprotected ranged line, interrupted cast, or bad reserve timing.

## Semantic Marker Dependency

Spatial combat requires authored map semantics. Battle maps and strategic-location interiors should be able to define visible rectangular grid markers from an editor-placed anchor, extending right and down by `m*n` cells.

Marker types may include deployment zone, chokepoint, lane, reserve point, flank route, ranged point, defend point, entrance, event spawn, and building slot. The marker data model and Godot authoring workflow belong to a separate semantic marker proposal and implementation.

## Non-Goals

- Individual soldier micro-control.
- Large-scale RTS box selection.
- AP/turn-based command flow as the future combat identity.
