# Square-Grid Realtime Combat Proposal

Status: Accepted

## Purpose

Refine live battle feel without changing the battle map grid type. The target is square-grid realtime automatic combat inspired by auto-battler combat flow, not a full Teamfight Tactics rules clone.

This proposal only covers the combat space model, movement, basic attack authority, and ability targeting abstraction. It does not cover economy, synergies, shop, bench, or other auto-battler metagame systems.

## Current Architecture

The accepted architecture already says combat is realtime and should not become old tactical chess or AP growth. The current implementation still behaves like automatic grid chess:

- Runtime actors store `GridX` and `GridY`.
- Runtime advances one grid step toward a target.
- Engagement uses Manhattan distance.
- Presentation sometimes performs additional grid path movement before playing attack feedback.
- Ability validation and previews are built around target grid cells and affected grid cells.

This creates a visible mismatch: the game is meant to feel realtime, but the battle facts and playback are still turn/grid-step shaped.

## Expected Architecture

Live battle uses square-grid anchored realtime combat:

- The square grid remains the combat authority. The project does not switch to hexes.
- Deployment places units on valid square grid cells.
- After battle starts, each unit has an anchored cell, optional reserved next cell, movement state, target actor, cooldowns, and ability execution state.
- Units move smoothly between square cell centers, but rules still use anchored/reserved grid cells.
- Units may path and acquire targets in all directions supported by the square grid. First implementation uses 8-neighbor movement unless map data forbids diagonal transitions.
- Units may attack only when anchored, not while transitioning between cells.
- Basic attacks target actors and resolve by grid range from attacker anchored cell to target anchored cell.
- Godot `Area2D` and `CollisionShape2D` are used for selection, hover, debug, and future visual query helpers. They are not the source of damage truth in this proposal.

## Performance And Memory Boundaries

The first implementation must keep battle simulation predictable:

- Runtime must not depend on Godot physics callbacks for combat truth.
- Runtime must not run full-map pathfinding for every actor every tick in this slice.
- Runtime chooses at most one neighboring square cell per actor update, then reevaluates next tick.
- Per-tick data structures must be bounded by active actor count, not map cell count.
- Event generation must stay bounded by the existing runtime tick limit and actor count.
- Presentation interpolation and interaction collision shapes must not create additional rule-resolution state.

## Footprint Implementation Slice

Unit size becomes a gameplay rule through lightweight grid footprints:

- Every unit keeps one anchor cell.
- The anchor cell is always the top-left cell of the footprint.
- Supported footprints include `1x1`, `1x2`, `2x1`, `2x2`, and `3x3`.
- Movement still chooses an anchor-cell neighbor. It does not require full-map multi-size pathfinding in the first footprint slice.
- A candidate anchor is valid only if every footprint-covered cell is walkable, unoccupied, and unreserved.
- Occupancy and reservation apply to all footprint-covered cells, not just the anchor.
- Pre-battle deployment drag previews resize the existing hover selection frame to the full footprint from the anchor cell instead of adding separate fill-highlight layers. The dragged sprite root snaps to the footprint center, and the anchor changes only when the pointer crosses the half-cell threshold around that center. Drop validation must use the same footprint cells as the preview so a large unit cannot be placed partly outside deployable space or over another unit.
- Basic attack range is measured from attacker footprint to target footprint by shortest square-grid distance.
- Area effects hit a unit if the effect area covers any cell in that unit's footprint.
- Unit sprites are authored from varied source pixel sizes. Footprint width and height define runtime occupancy, while Presentation derives a uniform visual scale from the largest footprint side and a tuning coefficient. A `2x1` unit must not stretch horizontally, and a `2x2` unit must not blindly double already-large art.

This preserves predictable movement cost while making large units visually and mechanically occupy the cells the player sees.

Initial authored content cases:

- `f1_azuritelion` (`assets/battle/units/f1_天蓝石狮/`) is the player default corps, with `2x1` footprint.
- `f1_grandmasterzir` (`assets/battle/units/f1_宗师Zir/`) is the player hero unit, with `2x1` footprint.
- `f6_draugarlord` (`assets/battle/units/f6_Draugar领主/`) is the enemy leader unit, with `2x2` footprint.

## Ability Abstraction

Abilities should be modeled so later content can use multiple targeting and direction styles without replacing the runtime contract.

Target modes:

- `UnitTarget`: lock onto a battle actor.
- `CellTarget`: lock onto a square grid cell.
- `DirectionTarget`: cast in a direction.
- `SelfCentered`: resolve from the caster.

Direction modes:

- `FreeAngle`: arbitrary direction from caster to target.
- `EightWay`: snapped to 8 square-grid directions.
- `FourWay`: snapped to cardinal directions.
- `ForwardArc`: cone or arc facing the resolved target direction.

Area shapes:

- `SingleActor`
- `SingleCell`
- `Line`
- `Cone`
- `CircleRadius`
- `GridRadius`

First implementation only needs basic attacks plus enough data shape to avoid hardcoding future abilities into the wrong model.

## Non-Goals

- No hex grid migration.
- No complete Teamfight Tactics rules clone.
- No freeform physics-based RTS movement in this slice.
- No Godot physics callbacks as authoritative damage resolution.
- No large-scale RTS box selection or high-frequency unit micro.
- No full projectile, area skill, or complex targeting implementation in the first slice.

## Acceptance Criteria

- Future implementation plans can clearly separate deployment grid placement, runtime anchored cell authority, and presentation movement.
- Runtime owns movement, targeting, attack, cooldown, and event facts.
- Presentation no longer creates separate attack-position truth by moving units into visual range independently of runtime facts.
- Basic melee/ranged attacks can be expressed as actor-target range checks on square grid anchored cells.
- Unit definitions can configure footprint width and height, and runtime snapshots carry those values without making Runtime load Godot resources.
- A large unit's rendered sprite uses uniform footprint visual scaling with a coefficient, while runtime footprint width and height remain the occupancy authority.
- Ability definitions have explicit target mode, direction mode, and area shape extension points.
- Unit scene collision shapes are allowed for interaction and debug, but docs state they do not decide damage.
- Runtime movement work is bounded by actor count and fixed neighbor candidates for this slice.
