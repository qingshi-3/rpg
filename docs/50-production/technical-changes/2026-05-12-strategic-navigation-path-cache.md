# 2026-05-12 Strategic Navigation Path Cache Stability

## Background

Enemy Raid armies could become blocked immediately after returning from a site battle to the strategic map. Logs showed that the scene had a configured `StrategicNavigationTileLayer`, but the first `NavigationServer2D.MapGetPath` call after scene activation sometimes returned `godot_navigation_path_empty`. Earlier frames in the same logs showed the same route succeeding after a short delay.

## Goal

- Keep a successfully built `WorldArmyState` path valid across strategic-scene reloads when the authored navigation data did not change.
- Treat the first transient `godot_navigation_path_empty` results as pending navigation, not as a permanent map-authoring failure.
- Keep true navigation contract failures explicit through `NavigationBlocked`.
- Stop world-clock advancement while any army is in `NavigationBlocked`.

## Implementation Notes

- `StrategicNavigationContext.Version` is now derived from the painted `StrategicNavigationTileLayer` cells and tile ids instead of from the context instance lifetime.
- `WorldArmyMovementService` consumes `IStrategicNavigationContext`, which lets regression tests exercise path-failure behavior without running a Godot scene.
- `WorldArmyState` tracks runtime-only transient path-build failures. A short run of `godot_navigation_path_empty` leaves the army `Moving` and retries; repeated failure still becomes `NavigationBlocked`.
- `godot_navigation_path_empty` is not judged by call count alone. After returning from a site scene, Godot can report a synchronized navigation map before `MapGetPath` can answer the route. Empty paths stay pending for a short real-time warmup window before they are allowed to become `NavigationBlocked`.
- `StrategicWorldRoot` treats `NavigationBlocked` armies as a hard world-clock blocker, including the resume-after-site-return path and the clock toggle.
- `GameLog` supports `RPG_GAMELOG_DIR` for non-Godot regression tests; normal Godot runtime logs still use `user://logs`.

## Verification

- `dotnet run --project tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -v:minimal`
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Manual Acceptance Checks

- Generate an enemy Raid, enter and exit a site battle, and confirm the enemy army does not become `NavigationBlocked` from a single `godot_navigation_path_empty` after strategic-scene activation.
- Confirm the logs show either a retry followed by `WorldArmyPathBuilt`, or a later `WorldArmyNavigationBlocked` only after both repeated path failure and the warmup window have elapsed.
- With a deliberately broken navigation layer, confirm the army still enters `NavigationBlocked` and the world clock cannot continue advancing.
