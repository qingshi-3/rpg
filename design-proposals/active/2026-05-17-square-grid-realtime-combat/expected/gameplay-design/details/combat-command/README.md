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

Future larger units may use grid footprints while keeping the square-grid realtime model. A unit footprint is a rectangular set of occupied cells such as `1x1`, `1x2`, `2x1`, `2x2`, or `3x3`.

The anchor cell is always the footprint's top-left cell. Movement still evaluates neighboring anchor cells, but a move is valid only when every cell covered by the new footprint is legal. Other units cannot occupy or reserve any cell inside that footprint.

During pre-battle deployment, the existing hover selection frame should resize around every cell covered by the dragged unit's footprint. The dragged sprite should stay centered on that footprint and only move to the next anchor after the pointer crosses the half-cell threshold around the current footprint center. Drop validation uses that same covered-cell set.

Attacks and area effects should read the footprint instead of pretending the unit only occupies its anchor:

- Basic attack range uses shortest square-grid distance between attacker footprint and target footprint.
- Actor-target attacks still lock onto an actor.
- Area effects hit when at least one covered cell overlaps the target footprint.

This makes large units readable as multi-cell bodies without forcing the runtime into expensive full-map multi-size pathfinding.

Visual size supports this readability, but it is not the occupancy rule. Larger units should scale their sprite uniformly from a tuned footprint size signal instead of stretching differently on X and Y.

## Non-Goals

- Individual soldier micro-control.
- Large-scale RTS box selection.
- AP/turn-based command flow as the future combat identity.
