# Strategic World Fog Of War Technical Contract

This document defines the implementation-facing contract for strategic-map fog of war. Player-facing design lives in `../../20-game-design/strategic-map/strategic-world-fog-of-war.md`.

## Ownership

Fog of war belongs to the strategic world presentation and intel layer.

It may read strategic map positions, player-owned WorldSites, player-controlled WorldArmies, and WorldSite state snapshots. It must not own or gate map generation, navigation, battle flow, AP, TurnSystem, or WorldSite runtime internals.

## State Model

The long-term state should be stored independently from authored map definitions:

```text
StrategicWorldIntelState
  ExploredCells / ExploredRegions
  VisibleCells / VisibleRegions
  KnownSites: Dictionary<SiteId, WorldSiteIntelSnapshot>
  KnownArmies / KnownThreats, when needed later
  LastUpdatedWorldTick

WorldSiteIntelSnapshot
  SiteId
  LastSeenWorldTick
  KnownControlState
  KnownOwnerFactionId
  KnownLocalResources
  KnownGarrisonSummary
  KnownFacilitySummary
  KnownThreatSummary
```

Exact region granularity can be tile-cell based or authored-region based. The implementation should pick the cheaper representation that fits the current strategic map. The public behavior must remain the same: unknown, revealed stale intel, and currently visible.

Current first implementation uses low-resolution fog texels keyed as `x:y` strings for intel storage and queries. These texels are independent from TileMap cells and can be replaced by a different mask representation later without changing saved intel semantics. Presentation must not expose the storage cells as the current visible edge; current visibility is drawn from circular vision masks.

## Runtime Derivation

Current visibility is derived data.

Each refresh should:

```text
clear Visible
-> gather vision sources
-> mark current visible cells/regions
-> merge visible into Explored
-> refresh intel snapshots for visible WorldSites and visible moving entities
```

Vision sources for the first version:

- Player-owned WorldSites.
- Player-controlled WorldArmies / expeditions.

Future vision sources may include buildings, hero traits, temporary actions, or events. Those should add vision source definitions rather than changing fog ownership.

Vision circles are stamped into the intel texel mask for state queries and snapshots. The presentation overlay draws current visible areas as smooth circular masks from the same vision sources, while explored stale intel may still be queried by texel keys.

## UI Contract

Strategic UI must ask the intel state before showing map information.

```text
Unknown:
  no concrete site/event/army/resource details

Revealed:
  show discovered terrain and known site shell
  show stale snapshot data with last-seen tick

Visible:
  show current StrategicWorldState-backed data
```

Hover panels, click detail panels, enemy markers, opportunity markers, and resource summaries should all use the same information-state rules. Do not add separate hidden fallbacks that bypass fog.

Fog must never affect strategic navigation. Right-click movement, expedition targeting, site target lookup, endpoint snapping, and path building use authored map geometry plus `StrategicNavigationTileLayer`; they must not call fog visibility helpers or intel queries.

The current presentation uses a CanvasItem shader to draw unknown fog outside circular visible masks and blends explored stale intel from a soft mask texture. Stored intel texels are allowed to remain coarse for save/query performance, but the rendered `Unknown` / `Revealed` edge must not expose hard cell rectangles; presentation should stamp and sample soft mask values. The overlay must refresh from current vision-source positions even when intel texel membership does not change, so moving armies do not make fog edges jump from cell to cell. Any later visual pass must keep the same `Unknown` / `Revealed` / `Visible` data contract and the same navigation separation.

## Decoupling From Map Randomization

Fog does not decide what content exists.

Map or slot randomization may later decide:

```text
slot -> concrete WorldSite / event / encounter / resource
```

Fog only decides whether the player has discovered that assignment and whether the displayed information is current.

## Persistence

Fog/intel state is campaign runtime state and must be saved with `StrategicWorldState` or an owned child state. It should not be rebuilt from authored definitions on load except for validating missing or obsolete references.

## Non-Goals

First implementation must not include:

- Complex scout unit systems.
- Dedicated reconnaissance action economy.
- Terrain line-of-sight blocking.
- Stealth and detection contests.
- False intelligence.
- Full fog-triggered ambush gameplay.
