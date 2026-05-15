# Strategic Grid Navigation

## Decision

Strategic world movement no longer uses Godot `NavigationServer2D` as the runtime path authority. `StrategicNavigationTileLayer` remains the authored walkable-surface source, but `StrategicNavigationContext` now builds an in-memory `StrategicNavigationGrid` from painted cells and runs synchronous A* over that grid.

## Boundary

- Camera movement, fog-of-war visibility, site labels, and UI state must not affect path queries.
- Missing, empty, or disconnected navigation cells are contract failures and enter `NavigationBlocked`.
- The provider id for successful strategic movement paths is `strategic_grid`.
- Diagonal movement is allowed only when both adjacent orthogonal cells are walkable, preventing corner cutting through blocked map authoring.

## QA

- Right-click movement to a reachable painted area should log `WorldArmyPathBuilt provider=strategic_grid`.
- Returning from a site scene must not create transient navigation sync failures; there is no Godot navigation map warmup path.
- Clicking or targeting disconnected painted islands should fail explicitly and pause world advancement through `WorldArmyNavigationBlocked`.
