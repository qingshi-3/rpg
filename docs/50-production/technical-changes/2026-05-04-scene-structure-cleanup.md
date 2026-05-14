# 2026-05-04 Scene Structure Cleanup

## Background

The project now treats `WorldSite` / 场域 as persistent operable world locations. The previous mix of battle-map, overmap, free-location, and temporary map-demo naming made it easy to put new content in the wrong place.

## Goal

Make the active scene and script structure explicit:

```text
scenes/
└─ world/
   ├─ StrategicWorldRoot.tscn
   ├─ sites/
   │  ├─ WorldSiteRoot.tscn
   │  └─ impl/
   │     └─ BonefieldSite.tscn
   └─ site_interactions/
      └─ .gitkeep
```

Corresponding presentation scripts:

```text
src/Presentation/World/
├─ StrategicWorldRoot.cs
├─ Sites/
│  └─ WorldSiteRoot.cs
└─ SiteInteractions/
   └─ .gitkeep
```

## Naming Rules

- `StrategicWorldRoot` is the strategic map, world progression, site selection, and enemy movement surface.
- `sites/WorldSiteRoot.tscn` is the world-connectable site runtime entry. It loads a concrete site and switches between wartime and peacetime modes.
- `sites/impl/` contains authored concrete site detail maps, such as `BonefieldSite.tscn` / 埋骨地. These maps are production scenes behind the site entry. Do not put them under a `maps` subfolder.
- `site_interactions` is reserved for detailed site interaction points. The first cleanup only creates the boundary; concrete logic can be added later.
- Tactical runtime classes may keep `Battle*` names when they represent combat logic, grid logic, HUD, input, intent, or unit behavior.

## Removed Historical Prototypes

These were removed from the current scene and script surface:

- `scenes/world/CampaignMapDemo.tscn`
- `scenes/world/WorldOvermapRoot.tscn`
- `scenes/world/WorldRoot.tscn`
- `scenes/world/locations/`
- `scenes/world/interactables/`
- old world-location scripts and definitions
- old campaign/story prototype definition folders

## Acceptance Checks

- Strategic world enters `scenes/world/sites/WorldSiteRoot.tscn`.
- `WorldSiteRoot` loads `scenes/world/sites/impl/BonefieldSite.tscn`.
- Source imports use `Rpg.Presentation.World.Sites`.
- Current code does not reference the removed prototype scenes or scripts.
- `dotnet build rpg.sln` passes after the cleanup.
