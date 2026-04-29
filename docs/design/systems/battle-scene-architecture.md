# Battle Scene Architecture

This document defines how battle runtime scenes host concrete battle maps.

## Runtime Shell

`BattleRoot` is the generic battle runtime scene.

Concrete battles plug into `BattleRoot` through a battle map scene or a future battle map definition.

Current manual-map flow:

- `BattleRoot.tscn` owns common battle roots: map, units, overlays, camera, and HUD.
- `OverlayRoot/GridHighlightOverlay` draws procedural hover and range highlights without tile art.
- `Camera2D` owns WASD movement, mouse-wheel zoom, and clamping to the lowest foundation layer bounds.
- `TutorialBattleMap.tscn` is loaded into `BattleRoot/MapRoot`.
- `TutorialBattleMap.tscn` uses `TileMapLayer` nodes for water, low ground, high ground, stairs, objects, and overlays.

## Battle Map Layer Order

- `WaterFoundationLayer`: water, poison, swamp, or other lowest terrain surface.
- `WaterDetailLayer`: water ripples, foam, shore details, and other visual-only water details.
- `WaterObjectLayer`: water objects or obstacles such as rocks, posts, and floating props.
- `LowFoundationLayer`: low-ground walkable foundation.
- `LowDetailLayer`: low-ground visual details such as stones, cracks, and debris.
- `LowObjectLayer`: low-ground objects that may block movement.
- `HighFoundationLayer`: high-ground walkable foundation.
- `HighDetailLayer`: high-ground visual details.
- `HighObjectLayer`: high-ground objects that may block movement.
- `StairLayer`: stairs or ramps connecting low and high ground. This renders above foundations to cover height seams.
- `OverlayLayer`: runtime previews such as movement range, attack range, selected cells, and Intent.

## GridState Derivation

Rendering may use many layers, but combat logic should collapse them into one `GridState`.

Initial `GridState` derivation:

- Cells in `WaterFoundationLayer` can become water or hazard cells.
- Cells in `LowFoundationLayer` become walkable with `height = 0`.
- Cells in `HighFoundationLayer` become walkable with `height = 1`.
- Cells in `StairLayer` define allowed transitions between height levels.
- Cells in object layers may block movement or line of sight.
- Detail layers do not affect movement by default.

## Future Data-Driven Entry

- `BattleMapDefinition` can point to a map scene and carry map metadata such as grid size.
- Battle logic should build or read `GridState` from explicit battle data, not from visual nodes as the source of truth.

`TileMapLayer` belongs to presentation. It is used for hand-authored maps and visuals, while combat logic depends on `GridSystem` and runtime state.
