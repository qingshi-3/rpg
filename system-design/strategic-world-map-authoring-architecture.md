# Strategic World Map Authoring Architecture

Status: Accepted Architecture

## Gameplay Authority

This architecture supports the accepted Sanguo Qunying-style realtime strategic world and paused city/battle contexts. It defines how geographic data, raster chunks, region artifacts, Godot navigation, and runtime presentation share one large-world contract. It does not decide campaign geography, city balance, fog detection rules, territory-control rules, or battle outcomes.

Player-facing city and smaller-region visuals must satisfy `../gameplay-design/details/strategic-world-region-presentation.md`. This architecture preserves the accepted canonical-data and derived-artifact boundaries, while the formal rendering technique remains open.

## Responsibility

This document owns:

- canonical strategic-world coordinates, chunk identity, and stable geographic ids;
- the local Web geographic-data workbench boundary;
- terrain, global linear features, strategic locations, and territory authoring contracts;
- derived terrain and territory mask contracts;
- final visual-chunk boundaries;
- Godot-owned per-chunk navigation authoring and static navigation compilation;
- faction- and intent-aware dynamic passage access;
- visual chunk runtime loading;
- the boundary between static art, geographic definitions, navigation truth, strategic state, and runtime overlays.

## Does Not Own

This document does not own:

- campaign geography or the final city roster;
- city, outpost, gate, bridge, or army persistent state;
- city-territory control and fog gameplay rules;
- detailed-city map topology or battle deployment markers;
- strategic AI intent;
- scene transition ownership;
- player-facing HUD layout;
- the exact shader, scene, mask compiler, resources, or rendering method used for formal strategic-region presentation;
- final chunk-art generation or licensed-asset browsing;
- automatic derivation of Godot navigation from Web terrain data.

## Core Model

The strategic world separates six responsibilities in one canonical coordinate space:

```text
configured reference layers
canonical geographic authoring data
derived terrain and territory artifacts
final visual chunks
Godot-authored navigation and dynamic access state
runtime strategic objects and presentation overlays
```

Reference pixels, final art pixels, geographic definitions, navigation, and strategic state are not interchangeable authorities.

## Canonical Identity And Coordinates

Every chunk has a stable identity and grid coordinate:

```text
ChunkId
ChunkCoordinate
WorldOrigin
WorldBounds
VisualTexturePath
NavigationScenePath
TerrainMaskPath
TerritoryMaskPath
```

Chunk placement is derived from one manifest and fixed chunk dimensions. The Web workbench, Godot authoring, and runtime must not maintain separate hand-authored positions for the same chunk.

Strategic locations and regions use stable ids. World coordinates do not pass through a Web-to-Godot scale or offset adapter: a stored position has the same meaning on both sides.

## Local Web Workbench Boundary

The workbench lives under `tools/world-map-workbench/` and runs locally. Its preferred boundary is:

```text
TypeScript Web front end
OpenLayers map and interaction surface
Turf.js vector geometry operations
local Node project-file service
Sharp raster-mask generation where required
```

The local service restricts reads and writes to configured project paths. Licensed assets are not uploaded. QGIS may prepare difficult source GIS data before it is configured as a reference layer, but the workbench is the daily game-specific geography editor.

The workbench owns only the five accepted feature areas below plus basic load, save, undo/redo, selection-property, and error-navigation infrastructure.

## Layer Contract

The workbench presents an ordered stack containing:

```text
real-world reference map
terrain classification
water system
mountains and highlands
roads
strategic locations
city territories and smaller regions
final chunk art
regional masks
validation information
```

Every layer supports visibility, lock, opacity, and display ordering. These controls are presentation state only.

- Reference maps and final chunk art are comparison inputs.
- Terrain, water, mountains/highlands, roads, strategic locations, and territories/regions expose canonical editable data.
- Regional masks and validation information are derived outputs.

Locking a derived or reference layer prevents accidental interaction; it does not create a second authority.

## Terrain Classification Contract

Terrain classification uses configured ids such as grassland, forest, marsh, wasteland, desert, and snow on a fixed world-aligned raster grid.

The editor supports brush, erase, fill, lasso, and polygon fill. It presents one continuous world surface even when a stroke touches several chunks. One operation writes every affected chunk-aligned mask consistently rather than requiring duplicated per-chunk editing.

The terrain layer supports position inspection and validates at least:

- unclassified cells;
- unknown terrain ids;
- suspicious isolated terrain regions;
- mask dimensions or origins that do not match the chunk contract.

Terrain ids communicate authored geography. They do not automatically define Godot walkability or movement cost.

## Global Linear Feature Contract

Rivers, roads, and mountain ranges are canonical global vector features. Chunk-clipped segments are derived views or export artifacts and must not become separately edited copies.

The workbench supports:

- control-point insertion, movement, and removal;
- river width classes;
- road classes;
- mountain density;
- river endpoint snapping to configured sources, lakes, coasts, or other rivers;
- automatic tributary-to-receiver relationships when a valid confluence is snapped or created;
- display of chunk-clipped results;
- validation of unexplained breaks and mismatched boundary intersections.

A feature crossing a chunk boundary retains one stable feature identity. Its clipped pieces must meet at the same canonical boundary coordinate.

## Strategic Location Contract

The accepted strategic-location types are cities, gates or passes, bridges, ferries, ports, ruins, and resource sites.

Each definition carries at least:

```text
LocationId
Name
LocationType
WorldPosition
DetailMapId (optional)
```

The workbench may render runtime-style city and outpost symbols as non-authoritative previews. Symbols are not baked into static terrain chunks.

Location validation covers stable-id uniqueness, configured reference-map deviation, and illegal placement against authored water, mountain/highland, or other configured exclusion facts. Strategic Management remains the owner of mutable ownership and gameplay state.

## Territory And Region Contract

City territories and smaller regions are canonical polygons. Each smaller region carries at least:

```text
CityId
RegionId
RegionRole
Direction
Geometry
```

The workbench presents the unioned outer city outline, exposes the actual `RegionId` under the pointer, and simulates the accepted default-hidden, city-hover, and smaller-region-highlight behavior for authoring review.

Topology validation covers:

- overlaps;
- holes;
- discontinuous geometry;
- region-without-city ownership;
- cross-city conflicts;
- invalid or duplicate stable ids.

Derived outputs include `territory_mask`, a world-position region lookup, and precompiled territory and region outlines. Runtime territory presentation and queries consume these artifacts but do not infer city ownership from final chunk colors.

## Visual Authoring Contract

Strategic-world art is delivered as aligned raster chunks. The workbench displays final chunk art for comparison but does not generate, regenerate, version, or batch-manage it.

The static art contains terrain and fixed natural landmarks. It does not bake in:

- cities or outposts whose presentation depends on state;
- armies or opportunities;
- faction control fills;
- city-territory boundaries;
- fog, hover, selection, alerts, or HUD.

Terrain remains intentionally low-detail. Forests, peaks, mountain ranges, rivers, roads, plains, marshes, snow, and wasteland are readable strategic symbols rather than tactical terrain simulations.

## Godot Navigation Authoring Contract

Each chunk owns one hidden authored NavigationLayer, TileMapLayer, or equivalent navigation resource aligned to final chunk art on a fixed shared grid. Godot authoring may display the active chunk and neighboring final art for context, but it is not the geographic editing workbench.

Navigation data may define:

```text
Walkable
MoveCost
NavigationTag
```

Blank or explicitly non-walkable cells are outside static navigation. Narrow tags may identify controlled passages or other navigation semantics, but persistent ownership and open/closed state remain strategic state rather than navigation-layer data.

Navigation chunks share one cell size, orientation, and origin convention. A local navigation cell maps to a global cell through the chunk coordinate and fixed navigation extent. Neighboring boundary cells resolve without hidden edge adapters.

## Navigation Initialization

Strategic-world startup performs one static compile:

```text
load every navigation chunk
-> validate shared grid contracts
-> map local cells into global cells
-> merge static walkability and base costs
-> register authored passage semantics
-> build immutable static topology
-> apply current dynamic access state
-> publish navigation revision
```

Visual chunk residency and Web terrain masks do not participate in navigation authority. Unloading a visual texture must not remove navigability.

## Dynamic Access

Dynamic route changes are overlays over static topology. They do not repaint navigation data or rebuild static geometry.

Examples include gate or pass ownership, bridge destruction or repair, and later accepted temporary route closures. Access evaluation receives at least the moving faction and command intent. A hostile controlled passage may reject through movement while allowing an army to route to an exterior assault approach.

When dynamic access changes:

```text
update access state
-> increment navigation revision
-> invalidate cached paths built against older revisions
-> recompute affected army paths at the next valid movement boundary
```

An alternate route is used when available. If no route exists, the army enters a gameplay-facing waiting or route-cut state and reports the reason. Invalid chunk data, broken coordinate contracts, or missing required navigation remain hard diagnostics.

## Runtime Visual Loading

Runtime displays visual chunks intersecting the camera view plus a preload margin. The initial implementation may keep all chunk textures resident if measured memory is acceptable, but runtime ownership stays behind a chunk-loading boundary so residency can change without changing navigation or strategic state.

The runtime render order keeps static art separate from dynamic overlays:

```text
visual chunks
strategic objects
city-territory and selection overlays
fog and alert presentation
screen-space HUD
```

City-territory, fog, hover, and control systems query strategic definitions, compiled region artifacts, and state. They do not infer authority from chunk pixel colors.

## Excluded Workbench Systems

The accepted workbench does not include:

- a CraftPix asset browser or style-rule editor;
- final chunk-art generation or regeneration;
- 3-by-3 art review, old/new visual comparison, or editor-thumbnail production;
- change-impact versions, stale-chunk queues, batch generation, or advanced automatic repair;
- a separate chunk inventory, search, or production-management interface;
- navigation painting or automatic terrain-to-navigation conversion.

## Failure Rules

- Missing canonical manifests, duplicate chunk coordinates, transform drift, and mask-grid mismatches fail visibly with stable ids and paths.
- Invalid global features or region topology fail validation and identify the relevant world position and object id.
- A save that would leave only part of one cross-chunk operation written must fail rather than commit a partial geographic state.
- Derived chunk clips, masks, lookups, and outlines must be reproducible from canonical data and must not be edited as independent authorities.
- Runtime must not substitute generated fallback terrain, territory, or navigation data when authoritative artifacts are missing.
- Visual loading failure may hide a visual chunk and report the resource failure, but it must not silently mutate navigation truth.
- Gameplay-valid route loss is not reported as an engine navigation-contract failure.

## Acceptance

This architecture is acceptable when:

- Web and Godot share one canonical world coordinate, chunk manifest, and stable-id contract;
- layer controls cover the accepted ten layers without changing their authority roles;
- terrain edits cross chunk boundaries and produce consistent terrain masks;
- rivers, roads, and mountains remain global features with reproducible chunk clips;
- strategic locations validate ids and placement without entering static terrain art;
- territory and region topology produces reproducible masks, lookups, and outlines;
- the workbench remains limited to the five accepted feature areas;
- Godot remains the navigation authoring and compilation authority;
- visual chunk residency remains independent from navigation availability;
- dynamic passage access invalidates stale paths through one revision contract;
- runtime overlays remain independent from static visual art.
