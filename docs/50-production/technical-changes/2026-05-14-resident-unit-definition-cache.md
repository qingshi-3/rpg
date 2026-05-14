# Resident Unit Definition Cache

## Background

Strategic-world and site-operation UI now display unit labels through
`BattleUnitDefinition.DisplayName` so they match battle HUD names. The first
implementation used per-scene `BattleUnitFactory` caches, which meant a new
`StrategicWorldRoot` or `WorldSiteRoot` could synchronously rebuild the nested
`assets/battle/units/**/unit.tres` path index during a simple world-detail click.

## Contract

- `BattleUnitDefinition` resources and the nested unit-definition path index are
  resident metadata shared across world and site scenes.
- Strategic-world detail UI, site-operation detail UI, and battle entities all
  resolve unit labels through the same battle unit definition authority.
- Runtime nodes, instantiated battle entities, site deployment runtime caches,
  TileMap-derived footprints, and UI child rows are not resident; they are
  rebuilt from the current world/site state when their owning scene is active.

## Acceptance

- Clicking a world site detail after returning from a site should not rescan all
  unit packages just to display garrison labels.
- Entering a site may still load the site scene and rebuild site-local runtime
  caches.
- Battle unit display-name regression coverage must check both world UI label
  routing and the shared resident cache boundary.
