# 2026-05-03 World Progression And Map Surface

## Background

Strategic World V1 already had `WorldTick`, production, and Raid progression, but most feedback was text-panel based. The strategic layer needs to read as a map surface where enemy plans move across visible space.

## Goal

Add the first pass of:

- `WorldProgression` naming.
- `WorldClock` auto-advancing `WorldTick` while unpaused.
- TileMap-ready strategic map surface anchors.
- Moving enemy Raid markers on the strategic map.

## Non-goals

- Full real-time simulation.
- Full faction AI.
- Pathfinding armies across arbitrary tiles.
- Replacing battle, AP, or TurnSystem behavior.

## Affected Areas

```text
scenes/world/StrategicWorldRoot.tscn
src/Presentation/World/StrategicWorldRoot.cs
src/Application/World/WorldTickService.cs
src/Domain/World/EnemyThreatPlan.cs
docs/design/world/
docs/roadmap/
docs/testcases/
```

## Design Rules

- `WorldClock` may trigger `WorldTick`, but `WorldTick` remains the single settlement point for production and threat countdown.
- Threats must stop at `Attacking` and require player handling; no silent loss while the player is reading the map.
- `WorldMapRoot` is the scene-authoring surface. Code reads anchors and draws state overlays; it does not own the TileMap art.
- The fallback painted map remains only as a no-asset placeholder.

## Scene Contract

```text
StrategicWorldRoot
  WorldMapRoot
    TileMap / TileMapLayer...
    MapAnchors
      Sites
        <site_id>
```

Historical note: this first pass used hidden enemy movement waypoint anchors. The current strategic map contract has since moved enemy Raid presentation to `WorldArmyState + StrategicNavigation`; new maps should not add hidden threat waypoint nodes.

## Acceptance Checks

- Strategic map still works without a configured TileMap.
- Adding a TileMap or TileMapLayer under `WorldMapRoot` replaces the fallback painted background.
- Site buttons and labels follow `MapAnchors/Sites/<site_id>`.
- Graveyard Raid marker moves toward Bonefield across the authored map surface.
- World clock pause prevents automatic tick advancement.
- Attacking threats pause the world clock and expose defend/auto-resolve actions.
