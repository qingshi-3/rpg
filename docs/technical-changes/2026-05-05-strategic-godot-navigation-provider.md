# 2026-05-05 Strategic Godot Navigation Provider

## Background

Strategic-world army movement previously asked the TileMap sampled A* service for a fresh path every frame for every moving army. That is acceptable for a tiny prototype, but it creates avoidable CPU and GC pressure as the map and army count grow. The strategic map direction is RTS-style continuous movement, so path requests should be event-driven and owned by Godot 2D navigation data.

## Goal

- Add a navigation context that requests Godot `NavigationServer2D` paths in `WorldMapRoot` space.
- Make `StrategicNavigationTileLayer` the only strategic navigation authoring surface.
- Cache each moving army's current path at runtime and only rebuild it when the target or navigation version changes.
- Remove the custom sampled TileMap A* and direct-path fallbacks from strategic army movement.

## Non-Goals

- Generate navigation polygons from TileMap cells in code.
- Add group avoidance, formation slots, or crowd steering.
- Replace battle/site tactical grid movement.
- Make collision shapes the authority for strategic pathfinding.

## Affected Files

```text
src/Application/World/StrategicNavigationContext.cs
src/Application/World/StrategicNavigationPath.cs
src/Application/World/WorldArmyMovementService.cs
src/Domain/World/WorldArmyState.cs
src/Presentation/World/StrategicWorldRoot.cs
scenes/world/StrategicWorldRoot.tscn
docs/design/world/strategic-world-rts-navigation-and-armies.md
```

## Implementation Notes

- `StrategicNavigationContext` uses Godot `NavigationServer2D.MapGetPath` and converts path points back into `WorldMapRoot` local coordinates.
- `StrategicNavigationContext` keeps a reference to `WorldMapRoot` and uses the current `ToGlobal` / `ToLocal` transform for each request; the strategic camera can move and scale `WorldMapRoot`, so a cached creation-time transform is invalid.
- If Godot navigation returns no path, a start/end point is outside the navigation area, or the navigation layer is missing, the command fails explicitly.
- A returned Godot path is accepted only when every endpoint, path point, and sampled path segment remains on painted `StrategicNavigationTileLayer` cells.
- `MapAnchors/Sites/<site_id>` is the visual/logical site center. Army movement uses a site navigation point: `MapAnchors/ArmySpawnPoints/<site_id>` when authored, otherwise the nearest painted `StrategicNavigationTileLayer` cell within the configured search radius.
- Player right-click movement, site attack/reinforce commands, and expedition creation build the authoritative path before mutating army state or removing garrison units.
- `WorldArmyState` stores runtime-only cached path points, current path index, destination, and navigation version. These fields are not saved.
- `WorldArmyMovementService` now builds a path only when the cached path is missing or invalid, then moves along cached waypoints each frame.
- Arrival, path failure, garrison transfer, field interception, battle-result resume, and explicit command changes clear cached paths.
- `StrategicWorldRoot.tscn` does not contain a `NavigationRegion2D` for strategic movement; the authored entry point is `WorldMapRoot/StrategicNavigationTileLayer`.
- `StrategicNavigationService` and `StrategicNavigationSurface` were removed; the strategic map must not keep a parallel sampled A* pathing authority.

## Map Authoring Contract

- `StrategicNavigationTileLayer` is the preferred dedicated navigation authoring layer; it can use a single debug or transparent tile with a full-cell navigation polygon.
- `StrategicMapLayer` remains the land/base visual layer.
- `StrategicBridgeLayer` remains the bridge visual layer.
- `SiteVisualLayer` remains only for WorldSite icon art and does not participate in navigation.
- Configure TileSet navigation polygons on `StrategicNavigationTileLayer` tiles; old visual layers do not define walkability.
- Clicking a blank area outside `StrategicNavigationTileLayer` must reject the command and log `WorldArmyCommandNavigationRejected`; it must not create a pending direct move.
- Site attack, reinforce, expedition, and newly generated moving armies resolve site endpoints to site navigation points before requesting a path. If no navigation point exists near a site, the command fails with a logged reason.
- Collision shapes can be added for physical blockers or future baking input, but collision alone is not the strategic pathfinding authority.

## Risks

- Godot navigation data is scene/runtime data; if it is edited dynamically at runtime, cached paths need explicit invalidation.
- If the navigation layer is missing, empty, or disconnected, army commands will fail instead of silently choosing another route system.

## Verification

- `dotnet build rpg.sln` passes.

## Manual Acceptance Checks

- With `StrategicNavigationTileLayer` configured, new army commands log `WorldArmyPathBuilt` with `provider=godot_navigation`.
- With missing, empty, or disconnected Godot navigation, movement logs `WorldArmyPathFailed` and reports the failure reason.
- Clicking outside painted navigation cells logs `WorldArmyCommandNavigationRejected` and leaves the selected army's previous state intact.
- If a site center is not directly on a painted navigation cell, the log contains `SiteNavigationPointResolved` once for that site, and the army starts from or arrives at that resolved navigation point.
- Visual-only `StrategicMapLayer` and `StrategicBridgeLayer` do not produce strategic army paths.
- After configuring Godot 2D navigation data, new army commands log `WorldArmyPathBuilt` with `provider=godot_navigation`.
- Moving armies do not emit path-build logs every frame; the log appears when a command is issued or a cached path is invalidated.
