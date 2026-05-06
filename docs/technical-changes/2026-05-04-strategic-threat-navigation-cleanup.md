# 2026-05-04 Strategic Threat Navigation Cleanup

## Background

The strategic map now has a TileMap-derived navigation surface and moving `WorldArmyState` entities. The earlier hidden threat waypoint layer was redundant and could conflict with the RTS-style map contract.

## Changes

- Removed the `WorldMapRoot/MapAnchors/ThreatMovementAnchors` scene contract.
- Renamed the field-site art layer in `StrategicWorldRoot.tscn` to `SiteVisualLayer`.
- Kept `MapAnchors/Sites/<site_id>` as the authoritative site logic anchor.
- Legacy threats without a linked `WorldArmyId` now build their temporary marker path through the Godot-backed `StrategicNavigationContext`; if navigation fails, no direct-line marker is drawn.
- New enemy Raid behavior remains `EnemyThreatPlan -> enemy WorldArmyState -> StrategicNavigation -> Attacking on arrival`.

## Authoring Contract

```text
WorldMapRoot
  StrategicMapLayer      # walkable land
  SiteVisualLayer        # WorldSite strategic art only
  StrategicBridgeLayer   # walkable bridge cells
  StrategicNavigation
  MapAnchors
    Sites
      <site_id>
    ArmySpawnPoints
    EncounterZones
```

`SiteVisualLayer` does not define gameplay. The matching `MapAnchors/Sites/<site_id>` marker must sit inside the field-site art so the runtime can map the anchor to its visual footprint.

## Verification

- Project builds.
- No code or current design document references the removed threat waypoint node.
- Existing enemy Raid armies continue to use `WorldArmyState` rendering and movement.
