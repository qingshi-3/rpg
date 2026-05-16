# Battle Scene Architecture

## Authority

Battle scene architecture is currently in transition. The accepted player-facing direction is hero-led light RTS, while the first migration left a backend auto-resolve/report path that retires the old AP/manual runtime.

Current authority routes:

- Gameplay: `../../../gameplay-design/content-systems-long-term-design.md`
- System design route: `../../../system-design/README.md`
- Gap tracking: `../../../gameplay-alignment/gap-register.md`
- Historical migration record: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`

## Scene Role

`WorldSiteRoot` should stay a composition shell:

- load the authored `WorldSite` map;
- expose the active grid and coordinate layer;
- show management, deployment, exploration, battle presentation, and report presentation;
- delegate battle launch and result writeback through application services.

The scene must not become the authority for casualties, deployment ownership, or world persistence.

## Map Role

Authored site maps may still use Godot `TileMapLayer` nodes for visual terrain. Runtime rules consume derived grid data and site placement state:

- `WorldSiteState.UnitPlacements` owns deployment.
- Grid caches are candidates and validation helpers only.
- Terrain tags, water, height, and walkability can influence deployment and future light-RTS battle behavior.

## Retired Scene Pieces

Manual battle flow controllers, action HUD pieces, preview controllers, and battle AP components are not active scene dependencies. Do not add them back to `WorldSiteRoot` or battle unit scenes.

New scene work must not expand the old AP/manual command UI and must not turn pure post-deployment auto battle playback into the product identity. If a feature cannot be implemented safely yet, keep the scene surface narrow and register the missing ownership in `gameplay-alignment/` or an accepted design proposal.
