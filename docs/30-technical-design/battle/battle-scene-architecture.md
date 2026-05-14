# Battle Scene Architecture

This document defines how battle runtime components use concrete site maps during battle.

## Runtime Shell

`WorldSiteRoot` is the generic site runtime scene. It can run the same site map in wartime battle mode or peacetime site operation mode.

Concrete battles plug into `WorldSiteRoot` through a site map scene or a future site map definition.

Current manual-map flow:

- `WorldSiteRoot.tscn` owns common site/battle roots: map, units, overlays, camera, and HUD.
- `FlowRoot/BattleCommandController` owns player command flow, selected entity state, and the interaction stack.
- `FlowRoot/BattleTurnController` owns turn phase sequencing, round state, enemy phase state, player phase startup, auto-selection, victory/defeat checks, and defeated-entity coordination.
- `FlowRoot/BattleIntentController` owns enemy intent lifecycle, intent bookkeeping, enemy intent marker coordination, and the enemy phase action execution entry.
- `OverlayRoot/GridHighlightOverlay` draws procedural hover and range highlights without tile art.
- `OverlayRoot/BattlePreviewController` owns runtime action preview state and applies selected, movement, path, intent, target, attack, and invalid highlights.
- `Camera2D` owns WASD movement, mouse-wheel zoom, and clamping to the lowest foundation layer bounds.
- `scenes/world/sites/impl/BonefieldSite.tscn` is loaded into `WorldSiteRoot/MapRoot`.
- `BonefieldSite.tscn` uses `TileMapLayer` nodes for water, low ground, high ground, stairs, objects, and overlays.
- `BattleMapView._Ready()` resolves map-authored layers and builds map runtime data.
- `WorldSiteRoot.LoadSiteMap()` calls `BattleMapView.EnsureRuntimeData()` as an idempotent fallback, then stores `BattleMapView.GridMap` and `BattleMapView.CoordinateLayer`.

Current scene paths:

- Site runtime shell: `scenes/world/sites/WorldSiteRoot.tscn`.
- Authored site maps: `scenes/world/sites/`.
- Site interaction-point logic placeholders: `scenes/world/site_interactions/` and `src/Presentation/World/SiteInteractions/`.

Current runtime ownership:

- `WorldSiteRoot` wires the scene and stores shared references.
- `BattleMapView` owns map-scene initialization, layer references, `BattleGridMap` construction, and coordinate-layer discovery.
- `UnitRoot` has `BattleUnitRoot` attached and owns unit-associated runtime state: unit node snapshots/lookups, alive-faction enumeration, turn resource restoration, movement blockers, motion presentation, intent markers, and defeated-unit node cleanup. Unit-local damage reactions are handled by `DamageReactionComponent` on the battle entity.
- `OverlayRoot` owns runtime previews and highlights through `BattlePreviewController` plus `GridHighlightOverlay`.
- `InputRoot` owns raw battle input through `BattleInputRouter`.
- `FlowRoot` owns command business handling through `BattleCommandController`, turn flow through `BattleTurnController`, and enemy intent flow through `BattleIntentController`; HUD and input commands both route through `BattleCommand`.

`WorldSiteRoot` still owns action context construction and the action executor. Flow controllers call those remaining Root boundaries through delegates.

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
- `OverlayLayer`: authored map overlay tiles when a map needs them. Runtime previews should live under `WorldSiteRoot/OverlayRoot`, not in map-authored tile layers.

## GridState Derivation

Rendering may use many layers, but combat logic should collapse them into one `GridState`.

TileSet custom data describes static per-tile gameplay properties:

- `Walkable`: whether pathfinding may pass through the tile.
- `MoveCost`: movement cost for entering the tile. Minimum runtime value is `1`.
- `CanStandOn`: special marker for object tiles that may support standing on top of them in future rules.
- `IsObstacle`: semantic obstacle marker for rules, debug views, and future interactions.
- `TerrainTag`: terrain label for future rule queries.

Boolean custom data follows positive-only authoring: unconfigured values are consumed as `false`, and only explicitly enabled values are treated as `true`.

Field dependency rules:

- `MoveCost` is meaningful only when `Walkable = true`; non-walkable cells expose final `MoveCost = 0`.
- First-pass movement range does not use `CanStandOn`; movement range is based on `Walkable`, `MoveCost`, height, and runtime blockers.
- `CanStandOn` does not make a non-walkable cell walkable by itself.
- `IsObstacle` is semantic only; blocking still comes from `Walkable = false`.
- `TerrainTag` on the effective top foundation describes the cell terrain for movement rules. Water should use `TerrainTag = water`.

Initial `GridState` derivation:

- Cells in `WaterFoundationLayer` can become water or hazard cells.
- Cells in `LowFoundationLayer` become walkable with `height = 0`.
- Cells in `HighFoundationLayer` become walkable with `height = 1`.
- Later foundation layers override earlier foundation layers at the same coordinate, so a full water base can sit under low or high ground without producing a height conflict.
- Movement terrain checks use the effective top foundation, not the union of all lower layer tags.
- Cells in `StairLayer` define allowed transitions between height levels.
- Cells in object layers enter `GridState` and consume TileSet custom data.
- Detail layers are visual-only and do not affect `GridState`.
- Non-detail layers can affect movement only when their `BattleMapLayer.AffectsWalkability` is enabled.

## Unit Terrain Access

Ordinary units cannot enter water terrain.

Runtime movement checks combine:

- Cell walkability and move cost.
- Height transition rules.
- Runtime blockers from battle entities.
- Unit terrain capability such as `MovementComponent.CanEnterWater`.

This means water may remain technically walkable in TileSet data for future swimmers, boats, or amphibious units, while ordinary units are still prevented from pathing into it.

## Future Data-Driven Entry

- `BattleMapDefinition` can point to a map scene and carry map metadata such as grid size.
- Battle logic should build or read `GridState` from explicit battle data, not from visual nodes as the source of truth.
- Unit initial distribution is currently temporary scene data. Long term, spawn positions should come from map or encounter configuration such as `BattleEncounterDefinition` plus `UnitSpawnDefinition[]`.

`TileMapLayer` belongs to presentation. It is used for hand-authored maps and visuals, while combat logic depends on `GridSystem` and runtime state.
