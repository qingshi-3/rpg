# 2026-05-04 RTS World Army Foundation

## Background

The strategic map is moving away from a site-menu presentation toward a Sanguo Qunying style campaign surface. Sites remain nodes on the map, while armies should become RTS-style moving entities in continuous world space.

## Goal

Add the first foundation for:

- Persistent `WorldArmyState`.
- RTS-style continuous army movement service.
- Strategic map scene contract nodes for future navigation, army spawn points, and encounter zones.
- Minimal army marker drawing on `StrategicWorldRoot`.
- Enemy Raid creation backed by a moving enemy `WorldArmyState`.
- Threat attacking state driven by army arrival for new threats.
- Player reinforcement expedition creation for player camp to Bonefield.
- Player assault expedition creation for player camp to Bonefield, with battle entry on arrival.
- Field interception between moving player armies and enemy armies.
- TileMapLayer-derived strategic navigation surface for authored land and bridge tiles.
- Battle trigger presentation flow: focus the strategic map on the contact point, show a one-button battle alert, then show pre-battle information before scene handoff.
- Shared map camera controller reused by both `WorldSiteRoot` and `StrategicWorldRoot`.

## Non-goals

- Navigation polygon generation.
- Custom TileSet terrain metadata or per-tile movement costs.
- Group avoidance or formations.
- Battle result army casualty writeback.
- Dedicated field-intercept battle map content.

## Affected Areas

```text
src/Domain/World/
src/Application/World/
src/Presentation/World/StrategicWorldRoot.cs
src/Presentation/Common/MapCameraController.cs
scenes/world/StrategicWorldRoot.tscn
docs/design/world/strategic-world-rts-navigation-and-armies.md
```

## Design Rules

- Strategic-world armies use continuous map coordinates, not battle grid cells.
- Tile cells may be sampled internally to build a navigation surface, but this must remain an RTS-style pathing implementation detail rather than a grid-tactics gameplay rule.
- Saved army state stores scalar coordinates so JSON save/load remains stable.
- The battle grid remains isolated inside battle/site runtime.
- Enemy threats should be represented by `WorldArmyState`; hand-authored threat movement waypoints are not part of the current map contract.
- Empty navigation or anchor nodes must not disable the fallback painted strategic map.

## Implementation Notes

- `StrategicWorldState` now has `ArmyStates`.
- `WorldArmyState` stores world position, destination, speed, radius, status, intent, units, cargo, and optional related threat id.
- `WorldArmyMovementService` advances `Moving` armies by `delta * MoveSpeed` and emits `WorldArmyArrived`.
- `WorldArmyMovementService` transfers `ReinforceSite` army units into the target site's garrison when the army arrives.
- `WorldEffectKind.CreateArmy` lets world actions create moving armies without hardcoding the action id into presentation code.
- `ActionAssaultBonefield` now creates a player `AssaultSite` army instead of immediately opening battle.
- `WorldArmyMovementResult.BattleReadyArmyIds` reports arrived assault armies that are ready to open a battle.
- `BattleStartRequest` can carry `SourceArmyId` / `TargetArmyId` for army-backed battle handoff.
- `StrategicWorldRoot` opens the Bonefield assault battle when the player assault army arrives.
- Arrived assault armies switch the target site into `Wartime` before battle handoff.
- `WorldBattleResultApplier` resolves the source assault army after battle result writeback.
- `WorldArmyMovementService` checks moving player and enemy armies for RTS-style proximity interception.
- `WorldArmyMovementResult.FieldIntercepts` carries the first contact pair to the presentation layer.
- `WorldBattleRequestBuilder.BuildFieldInterceptRequest` creates a `FieldIntercept` battle request for the contact pair.
- `WorldBattleResultApplier` resolves `FieldIntercept`: player victory defeats the enemy army and resolves its linked Raid; player defeat defeats the player army and lets the enemy continue.
- `StrategicNavigationSurface` stores walkable strategic map cells and their world-space centers.
- `StrategicWorldRoot` builds the strategic navigation surface from `StrategicMapLayer` and `StrategicBridgeLayer`.
- Any occupied cell in either walkable layer is treated as walkable; land/bridge overlap at a connection cell is valid and remains one walkable cell.
- `StrategicNavigationService` now finds a path across the sampled surface and returns a world-space polyline for continuous army movement.
- If no strategic navigation surface is configured, navigation falls back to direct straight-line movement so prototype scenes without TileMapLayer data still run.
- `StrategicWorldRoot` syncs `WorldSiteDefinition.MapPosition` from `WorldMapRoot/MapAnchors/Sites/<site_id>` during scene ready/reset so runtime armies use authored scene anchors rather than stale definition coordinates.
- `StrategicWorldRoot` keeps army/navigation state in map-local coordinates and uses `WorldMapRoot` only as the visual transform for focusing the strategic map.
- `MapCameraController` owns shared WASD movement, mouse wheel zoom, middle-mouse drag panning, focus, and map-bounds clamping.
- `BattleCameraController` now derives from `MapCameraController` and only adapts `WorldSiteRoot` / `BattleMapView` bounds into the shared camera.
- `StrategicWorldRoot.tscn` now has its own `WorldCamera` using `MapCameraController`; the strategic UI remains screen-space while the camera drives the `WorldMapRoot` transform.
- `StrategicWorldRoot` now delays `BattleSessionHandoff.BeginBattle` until after the battle alert and pre-battle information dialog are confirmed.
- Assault and defense battles include site information in the pre-battle dialog; field intercepts show both sides' army information without site details.
- `WorldSiteRoot` can add requested player forces for `FieldIntercept`, reusing the existing site battle runtime until a dedicated field map exists.
- `WorldSiteRoot` can load a dedicated `FieldInterceptMapScene` for `field_intercept_v1`; if it is not configured, it falls back to the default site map.
- `WorldTickService` now creates an enemy army when a Raid threat is generated, stores the army id on `EnemyThreatPlan`, and emits `WorldArmyCreated`.
- Linked threats no longer use countdown progression while their world army exists; army arrival sets the threat to `Attacking`.
- `StrategicWorldRoot` draws non-garrisoned, non-defeated armies and calls movement while the world clock is active and unpaused.
- `StrategicWorldRoot` suppresses legacy threat-route marker drawing for threats that already have a real army marker.
- Attacking world armies draw their own warning ring, so linked threats no longer depend on the legacy countdown marker.
- When a linked enemy army arrives, the strategic world pauses, focuses the attack point, and opens the existing battle alert and pre-battle flow for the defense raid.
- The threat list can select moving enemy armies and shows an approximate arrival time based on current army distance and clock speed.
- `ActionAssignMilitiaToBonefield` now removes militia from player camp, creates a player army, and only adds the militia to Bonefield after arrival.
- `StrategicWorldRoot.tscn` now includes:

```text
StrategicWorldRoot
  WorldCamera
  WorldMapRoot
    StrategicMapLayer
    StrategicBridgeLayer
    StrategicNavigation
    MapAnchors
      Sites
      ArmySpawnPoints
      EncounterZones
```

## Next Steps

1. Configure authored field-intercept battle map content into `WorldSiteRoot.FieldInterceptMapScene`.
2. Promote the sampled TileMapLayer surface to authored Godot `Navigation2D` or costed terrain only when bridge/terrain authoring needs exceed the current lightweight pathing.
3. Deepen `BattleStartRequest` / `BattleResult` so battle can write back unit-level army casualties, threat state, and site state.
4. Reduce remaining hardcoded action and battle-entry branches.
5. Add save/debug tools for spawning and teleporting armies through service APIs.

## Acceptance Checks

- Project builds.
- Existing strategic map still works with no armies.
- Empty `StrategicNavigation`, `ArmySpawnPoints`, and `EncounterZones` do not hide fallback map rendering.
- `StrategicMapLayer` and `StrategicBridgeLayer` cells are treated as a single walkable surface.
- A bridge cell overlapping a land cell at a connection point remains walkable.
- A Graveyard-to-Bonefield army routes across authored land/bridge cells instead of moving directly through empty water or gaps.
- Player camp, Bonefield, and Graveyard army source/destination positions match `MapAnchors/Sites` markers in the scene.
- When an assault, defense, or field-intercept battle triggers, the strategic map immediately centers the trigger point in the visible map area.
- Strategic map camera supports WASD pan, mouse wheel zoom, middle-mouse drag, and clamping to authored map bounds.
- Site battle camera still supports the same movement and zoom controls after the shared controller extraction.
- The first battle prompt only says that battle happened and has one `确定` button.
- Confirming the prompt opens pre-battle information with both sides' forces; assault/defense battles also show the target site's state, owner, damage, buildings, and garrison.
- Army state can be serialized through the existing strategic-world save format.
- Graveyard Raid appears as a moving enemy army instead of only a countdown marker.
- New linked Raid threats become `Attacking` when their army arrives, then pause world progression and automatically enter the defense-raid battle prompt.
- Moving Raid entries remain selectable and expose source, target, and approximate arrival timing.
- Assigning militia to Bonefield creates a visible player army instead of instantly teleporting the garrison.
- The player army joins the target garrison only after it arrives.
- Attacking Bonefield creates a visible player assault army instead of entering battle immediately.
- The assault battle opens only after the player assault army reaches Bonefield.
- Moving player and enemy armies trigger a field intercept when their radii overlap with the configured proximity threshold.
- Field-intercept victory clears the enemy army and related Raid; defeat clears the player army and resumes the enemy army.
