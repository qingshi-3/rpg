# Strategic World Geographic Authoring And Chunk Runtime Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: STRATEGIC-WORLD-MAP-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/README.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/resource-authoring-taxonomy.md`
  - `system-design/strategic-world-map-authoring-architecture.md` (new)
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/archived/2026-07-10-strategic-world-map-workbench.md`

## Requirement

The strategic world must support a map comparable in scope to a large Sanguo Qunying-style campaign without requiring one unmaintainable full-resolution world texture or visible runtime TileMap terrain.

Geographic facts must be authored and validated in an engine-independent local Web workbench. Godot consumes the same world coordinates, stable ids, chunk manifest, and exported artifacts, while retaining navigation authoring, global navigation compilation, runtime chunk presentation, and final in-engine validation.

## Accepted Direction

### Responsibility Split

The local Web workbench is a strategic-world geographic data editor and validator. It does not own gameplay runtime logic, Godot navigation, or a general-purpose chunk-art production pipeline.

Godot owns:

- per-chunk static navigation authoring against final chunk art;
- compilation of all navigation chunks into one global strategic navigation surface;
- faction- and intent-aware dynamic route access;
- runtime visual chunk loading;
- fog, territory hover, selection, strategic objects, detailed-map entry, battle transitions, and final in-engine validation.

Both sides use one canonical world coordinate space and one chunk manifest. A position such as `(12000, 6400)` has the same meaning in the workbench, exported artifacts, Godot authoring, and runtime.

### Layer Management

The workbench exposes this ordered layer set:

- real-world reference map;
- terrain classification;
- water system;
- mountains and highlands;
- roads;
- strategic locations;
- city territories and smaller regions;
- final chunk art;
- regional masks;
- validation information.

Every layer can be shown, hidden, locked, reordered, and assigned a display opacity. Display ordering does not change data authority. Reference maps and final chunk art are comparison layers; regional masks and validation information are derived layers.

### Terrain Editing

The terrain-classification layer supports grassland, forest, marsh, wasteland, desert, snow, and later configured terrain ids through brush, erase, fill, lasso, and polygon-fill operations.

Editing is continuous across chunk boundaries. The workbench can inspect the terrain id at a world position, detect unclassified cells and suspicious isolated terrain, and save the result as chunk-aligned terrain masks without requiring the designer to repeat the same stroke per chunk.

### Rivers, Roads, Mountains, And Highlands

Rivers, roads, and mountain ranges are authored as global continuous vector features and displayed through their chunk-clipped results.

The workbench supports control-point editing, river width classes, road classes, mountain density, endpoint snapping, automatic tributary confluence relationships, and validation of unexplained breaks or mismatched chunk-edge results. River endpoints may snap to configured sources, lakes, coasts, or other rivers; snapping a tributary to another river records the receiver relationship automatically.

### Strategic Locations

The workbench supports cities, gates or passes, bridges, ferries, ports, ruins, and resource sites.

Every strategic location has a stable id, name, type, world position, and optional detailed-map id. Moving a location immediately updates its reference-map deviation and terrain-conflict diagnostics. Runtime city and outpost symbols may be previewed for alignment, but they are not baked into static terrain art.

### City Territories And Smaller Regions

The workbench supports irregular city-territory polygons and smaller internal regions. Each region carries at least `CityId`, `RegionId`, role, and direction.

It displays the unioned city outline, exposes the actual `RegionId` under the pointer, simulates default-hidden territory presentation plus city-territory hover and smaller-region highlight, and validates overlap, holes, discontinuity, and cross-city conflicts.

The derived outputs are:

- `territory_mask`;
- region lookup data;
- precompiled territory and region outlines.

### Local Tool Boundary

The workbench lives under `tools/world-map-workbench/`. The preferred implementation boundary is a TypeScript Web front end with OpenLayers for map interaction, Turf.js for vector geometry operations, and a local Node service for controlled project-file access. Raster mask generation may use Sharp. Licensed project assets remain local and are not uploaded by the workbench.

Reference-map cleanup or projection preparation may be performed in QGIS before the data enters the workbench. The workbench only needs to load configured reference layers; advanced GIS cleanup is not part of its first scope.

Basic project loading, saving, undo/redo, selection properties, and navigation from a validation result to its world position are editor infrastructure rather than additional production systems.

### Visual Chunks And Runtime

Strategic terrain is delivered as aligned raster chunks and remains a low-detail geographic signal rather than a scaled-down city map. CraftPix terrain assets under `assets/textures/tiles/` remain the visual reference for palette, pixel contour, lighting direction, and terrain identity.

Static chunk art contains terrain and fixed natural landmarks. It does not bake in cities, outposts, armies, faction control fills, territory borders, hover state, fog, or HUD.

Runtime may stream chunks intersecting the camera view plus a preload margin. Visual chunk residency never owns or removes navigation truth. The first implementation may keep all chunks resident if measured memory is acceptable, but it must retain a loading boundary that permits later streaming.

### Navigation

Navigation remains authored in Godot as per-chunk hidden NavigationLayer or equivalent data aligned to final chunk art. Web terrain classes and masks may provide visual context but do not automatically become walkability or movement-cost authority.

All static navigation chunks compile once into one global topology when the strategic world starts. Gate, pass, bridge, and later accepted route-state changes remain dynamic overlays. Dynamic access changes increment the navigation revision and invalidate stale army paths without repainting static navigation data.

## Current Architecture

The current prototype uses authored TileMapLayer content as the visible world surface and builds one strategic navigation grid from one navigation TileMapLayer. The navigation grid is static and faction-neutral. Army paths already retain a navigation surface version and rebuild when that version changes.

This prototype is useful as a migration base, but it does not define a scalable visual-chunk contract, an engine-independent geographic authoring workflow, chunk-aligned masks, global vector features, territory compilation, or faction-aware dynamic passage ownership.

## Expected Architecture

The expected authority copies define:

- a local Web geographic-data workbench with the five accepted editing areas;
- one canonical world coordinate, chunk, and stable-id contract shared with Godot;
- canonical geographic data and derived masks separated from final chunk art and runtime state;
- Godot-owned navigation authoring and global navigation compilation;
- runtime visual chunk loading separated from navigation availability;
- the Presentation boundary between static art, strategic objects, territory overlays, fog, and HUD.

## Scope Boundaries

This proposal does not add:

- a CraftPix asset-library browser or terrain-style rule editor;
- automatic final chunk-art generation;
- 3-by-3 art comparison, visual version comparison, or batch art generation;
- change-impact versions, stale-chunk generation queues, or advanced automatic repair;
- a separate chunk inventory, search, or production-management interface;
- Web-authored Godot navigation or automatic conversion from terrain ids to walkability;
- city-territory gameplay rules, fog detection rules, campaign geography, or a final city roster;
- detailed-city map geography, battle deployment, retreat, or result-writeback changes;
- final world size, chunk pixel size, navigation cell size, or streaming radius.

## Acceptance Criteria

- The Web workbench and Godot use one world coordinate space, chunk manifest, and stable-id contract.
- The accepted ten layers can be shown, hidden, locked, reordered, and assigned opacity.
- Terrain editing crosses chunk boundaries and produces chunk-aligned terrain masks with unclassified and isolated-terrain diagnostics.
- Rivers, roads, and mountain ranges remain continuous global features and expose their chunk-clipped results and edge diagnostics.
- Strategic locations retain stable ids and validate reference deviation and illegal terrain placement without being baked into static art.
- City territories and smaller regions validate topology and generate `territory_mask`, lookup data, and compiled outlines.
- The workbench does not grow into a final chunk-art generator, production queue, GIS replacement, or navigation editor.
- Godot can author and compile navigation independently of visual chunk residency.
- Runtime overlays remain independent from static visual art.
- A focused implementation proposal is approved before Web code, Godot code, scenes, resources, or asset-pipeline changes begin.
