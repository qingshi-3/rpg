# Grid Map Debug Readout

## Background And Goal

Hand-authored battle maps use multiple `TileMapLayer` nodes. Runtime logic needs a single grid data collection, and designers need a quick way to inspect the merged cell data in editor/runtime.

## Non-Goals

- Do not implement pathfinding, movement range, line-of-sight rules, or battle resolution.
- Do not make visual `TileMapLayer` nodes the permanent source of truth for all future maps.

## Architecture

- `Domain/Battle/Grid` owns grid runtime data: positions, cells, layer records, and the merged map collection.
- `Presentation/Battle/GridMapReader` adapts `BattleMapLayer` nodes into domain grid data.
- `Presentation/Battle/BattleGridHighlightOverlay` owns procedural grid highlights under `OverlayRoot`.
- `Presentation/Battle/BattleCameraController` owns battle camera movement, zoom, and map-bound clamping.
- `Presentation/Battle/Debug` owns optional debug components.
- `BattleDebugController` is the single debug toggle point. Future battle debug tools should be children of `DebugRoot` and derive from `BattleDebugComponent`.
- `BattleGuideGridDebug` draws a 16-world-pixel pale-blue guide grid for tile alignment checks and is toggled independently under the debug system.

## Current Behavior

- The reader records every used tile from every `BattleMapLayer`.
- The camera bounds are derived from the lowest-height `Foundation` layer, which is expected to be a rectangular ground layer.
- Visual-only layers are preserved in per-cell layer data but do not affect merged movement or line-of-sight flags.
- Foundation layers set cell existence, height, and initial walkability.
- Object layers can block walkability or line of sight according to their layer config.
- Stair layers mark height transitions.

## Acceptance Checks

- `BattleRoot` loads a map and emits the loaded map to debug components.
- Hovering a cell with debug enabled shows merged cell data and source layer tile records.
- Hovering a cell shows a transparent procedural cell frame through the highlight overlay.
- Movement, attack, skill, target, selected, and invalid ranges can be rendered by passing cell collections to the highlight overlay.
- WASD moves the battle camera without letting the viewport edges pass the ground layer bounds.
- Mouse wheel zooms the battle camera within configured limits; the minimum zoom is raised when needed so the viewport cannot become larger than the map bounds.
- Pressing the debug toggle key disables all battle debug components together.
- Pressing the guide-grid toggle key shows or hides the 16-pixel auxiliary grid while debug is enabled.
