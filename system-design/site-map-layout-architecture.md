# Site Map Layout Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md`, `gameplay-design/details/cities-and-locations/README.md`, `gameplay-design/details/strategic-region-detail-map-mapping.md`, `system-design/strategic-management-system-architecture.md`, `system-design/semantic-map-marker-architecture.md`, `system-design/strategic-region-detail-map-mapping-architecture.md`, and `system-design/battle-navigation-topology-architecture.md`.

The accepted direction is city-led strategic management with authored strategic-location spaces. City and site maps should be reusable enough for production, but varied enough that bridges, high ground, deployment areas, construction regions, resources, and decoration can change how a location reads and plays.

## Responsibility

The Site Map Layout system owns the authoring contract for reusable strategic-location and battle-capable site maps:

- reusable base terrain scenes made from stable Godot TileMapLayer structure;
- inherited layout variant scenes that add bridges, content layers, obstacles, resources, semantic markers, and height connections;
- layout definitions that bind a stable layout id to a layout variant scene path and base terrain id;
- extraction of final gameplay surfaces, semantic markers, bridge markers, and explicit height connections into pure data;
- validation of layer taxonomy, single-surface-per-cell rules, marker height legality, bridge legality, and layout-state key stability;
- stable detailed-map marker catalogs that province-member city mappings can reference without copying footprints;
- diagnostics for invalid or ambiguous map authoring.

The system is an authoring and extraction boundary. It does not own Strategic Management rules, battle Runtime movement decisions, settlement, or persistent campaign mutation.

## Does Not Own

Site Map Layout does not own:

- strategic-location ownership, facilities, resources, corps, heroes, expeditions, or campaign time;
- per-location persistent state such as built facilities, depleted resources, destroyed bridges, captured objectives, or event progress;
- battle Runtime occupancy, reservations, targeting, damage, movement state, or AI decisions;
- root scene transition policy;
- procedural map generation;
- same-coordinate multi-surface movement or full multi-floor simulation;
- automatic inference of bridge height, bridge entry points, or high-ground access from visual tile overlap;
- province/city large-world geometry, control, approach selection, or polygon-to-grid projection;
- selection of which member city maps to which detailed-map semantic marker.

## Authoring Model

## Repository Location

The first authored city-map module lives under `scenes/city/`.

Recommended first structure:

```text
scenes/city/
  base/
    plains_city_base.tscn
  layouts/
    plains_city_v0_layout.tscn
  markers/
    city-specific marker scenes, only if shared map markers are insufficient
```

`base/` contains reusable terrain bases. `layouts/` contains player-entered layout variant scenes that inherit from those bases. `markers/` is optional and should stay small; shared marker scenes still belong to the broader map marker system when their semantics are not city-specific.

The first implementation city is a validation slice, not a complete city library. It should prove that a new city map can be authored independently from DemoSite and BonefieldSite.

### Base Terrain Scene

A base terrain scene is a reusable Godot scene. It contains stable terrain possibility, not final site content.

Base terrain may include:

- water, pits, low ground, high ground, roads, banks, cliffs, walls, terrain boundaries, and base visual style;
- stable TileMapLayer groups for gameplay heights;
- optional candidate regions that help authors place bridges or other layout content.

Base terrain must not include:

- actual bridges;
- construction-region markers;
- deployment zones;
- objective zones;
- resource points;
- event spawns;
- location-specific decorations;
- location-specific obstacles;
- persistent state nodes.

The first base terrain scene should contain enough terrain variety to validate the contract:

- water or a river area;
- low ground;
- one high-ground platform or wall-like area;
- stable TileMapLayer groups for gameplay heights;
- no actual bridge, no construction-region markers, no deployment zones, and no resources.

### Layout Variant Scene

A layout variant scene inherits from a base terrain scene and represents the detailed-map template that a province can use for its member cities.

Layout variants may add:

- bridge visual layers and bridge markers;
- layout-specific decoration and obstacle layers;
- resource, event, entrance, objective, building-slot, and deployment markers;
- explicit height connections such as ramps, stairs, bridge approaches, or gate slopes;
- layout metadata such as `LayoutId`, `BaseTerrainId`, supported strategic-location kinds, and authoring tags.

Multiple provinces may bind to the same layout variant. The layout variant remains a reusable template. It must not store persistent progress or player-owned facts.

Each province definition binds its authoritative `LayoutId`. Member-city mappings do not choose a layout; they select semantic marker roles only inside the layout already bound to the province. Different member cities may select different entrances or deployment contexts, but they cannot switch the province to another layout.

The first layout variant should inherit from the first base terrain scene and include:

- one same-height river bridge marker that turns part of the river crossing into ordinary walkable `h=0` ground;
- one explicit high-ground entry connection such as a stair, ramp, or bridge approach;
- two or three building-slot markers;
- player and enemy deployment-zone markers;
- one resource-point marker or equivalent site interaction marker;
- a small set of layout-owned decorations or obstacles.

This first layout validates the authoring contract. It does not need to implement full city-management UI, destruction rules, battle result writeback, or a broad pool of reusable maps.

## Layer Taxonomy

The first layout model uses constrained gameplay heights:

```text
h=-1 water, pit, deep gap, or non-standing lower visual surface
h=0 low ground, normal roads, ordinary city ground, same-height river bridges
h=1 high ground, walls, platforms, upper bridge surfaces
h=2 reserved for rare future special high surfaces
```

Each gameplay height may use:

```text
Foundation: creates a gameplay surface and provides terrainTag / walkability.
Detail: visual-only art that never changes walkability.
Object: same-height obstacle or line-of-sight object.
Overlay: visual-only foreground, selection, shadow, or presentation art.
```

Visual layers may overlap freely. Gameplay extraction must resolve each grid cell to at most one final standable top surface. If a higher walkable foundation covers a lower foundation at the same grid coordinate, only the higher surface is standable for gameplay consumers.

Same-height neighboring walkable surfaces may connect through normal topology rules. Different-height neighboring surfaces never connect by adjacency alone. Height changes require explicit connections.

## Bridge Rules

Bridge placement belongs to layout variants, not base terrain, because bridge position changes route structure, chokepoints, deployment value, and map identity.

Bridge art may be drawn on one or more visual TileMapLayers and may overlap water, banks, roads, and high-ground edges. Bridge art does not decide gameplay height or connectivity.

Bridge gameplay is defined by bridge markers:

```text
BridgeId
BridgeKind
CellHeight
CoveredCells
Tags
ConnectionIds, when needed
```

Initial bridge kinds are:

| Kind | Rule |
|---|---|
| `RiverBridge` | Same-height bridge used to cross water. Covered cells are ordinary walkable surfaces at the marker `CellHeight`, usually `h=0`. |
| `HeightBridge` | Bridge, ramp-like bridge, or raised approach whose bridge surface is on the marker `CellHeight`, usually `h=1`; cross-height entry requires explicit connections. |

A river bridge does not create a height transition. If both banks are `h=0`, the bridge cells are `h=0` and connect like ordinary ground.

A height bridge uses the bridge surface's gameplay height. A unit that enters the bridge surface is already on that height. A low-to-high bridge entry is an explicit connection such as:

```text
low ground h=0 -> bridge surface h=1
```

The unit is never considered to stand "inside" a connection. It always stands on a concrete surface.

Bridge height and bridge entry points must not be inferred from neighboring terrain, art overlap, or the highest nearby surface. Missing or ambiguous bridge marker data is an authoring error.

## Persistent State

Persistent state belongs to Strategic Management and must be serializable without Godot scene nodes.

Reusable layout templates may expose stable ids:

- layout id;
- marker ids;
- bridge ids;
- resource point ids;
- entrance ids;
- objective ids;
- construction-region marker ids.

Durable location facts must be keyed by strategic location id plus stable layout/marker ids. For example:

```text
location_id + building_slot_marker_id
location_id + bridge_id
location_id + resource_point_id
```

Two provinces may use the same layout id and marker ids without sharing built facilities, depleted resources, bridge destruction, control, garrison, event progress, or battle outcomes. Persistent city facts remain keyed by member `LocationId` plus stable layout/marker ids.

## Runtime State

Layout runtime state may include:

- an instantiated layout scene tree for the active site or battle;
- extracted pure surface, marker, bridge, and connection data;
- validation diagnostics;
- presentation-only TileMapLayer nodes and visual overlays.

This runtime state is disposable. It must not become campaign state and must not be used as the long-term source for built facilities, resources, bridges, control, or battle results.

## Inputs

Inputs are:

- authored base terrain scenes;
- authored layout variant scenes;
- Site Map Layout definitions;
- canonical province definitions that bind province ids to layout ids and member location ids;
- semantic markers, bridge markers, and explicit height connections;
- Godot TileMapLayer custom data used for terrain tags and walkability;
- optional layout validation rules for strategic-location kind, bridge candidates, and marker requirements.

## Outputs

Outputs are:

- pure layout metadata keyed by `LayoutId`;
- final top-surface data for movement, deployment, placement, and marker validation;
- bridge marker data and bridge diagnostics;
- semantic marker data;
- explicit height-connection data;
- low-noise validation diagnostics;
- authoring failures that block unsafe layout use when required data is missing.

## Contracts

- Base terrain scenes define terrain possibility; layout variant scenes define actual site content.
- Layout variants must be Godot inherited scenes or an equivalent authored scene composition that preserves the base terrain contract.
- Bridges, decorations, resources, obstacles, construction-region markers, deployment zones, objective zones, and event markers belong to layout variants unless a later confirmed discussion updates this authority rule.
- Every grid cell has at most one final standable gameplay surface.
- Same-height movement may use ordinary topology adjacency; cross-height movement requires explicit connections.
- Bridge gameplay facts come from bridge markers and explicit connections, not visual tile overlap.
- Bridge position is layout-specific and must not be hardcoded into base terrain.
- Provinces bind to layout ids. Member cities must not bind to layouts or directly to base terrain as the entered map.
- The province's `LayoutId` binding is the sole entered-map selector. A mapping or Bridge value may carry or validate that same identity but must not select a different layout.
- Extracted `SemanticMapMarkerData.MapId` is the serialized carrier of the containing layout's `LayoutId`; it must equal the selected province layout and is not an independent map identity.
- Persistent facts are keyed by strategic location id plus stable layout/marker ids.
- Province-member city mappings may reference a layout's stable marker ids, but the layout never embeds large-world polygons or derives marker cells from world coordinates.
- A city binding cannot override extracted marker type, footprint, height, topology, or placement legality.
- Runtime consumers receive pure data; they must not query authoring nodes as gameplay authority.

## Failure Rules

- Missing layout definition or layout scene path fails explicitly.
- A province that requires an entered site map must not silently fall back to an unrelated layout.
- A city mapping, extracted marker record, or Bridge context whose carried map identity differs from the province's authoritative `LayoutId` fails validation rather than changing layouts.
- Duplicate layout ids, bridge ids, or marker ids fail validation within the relevant layout scope.
- A bridge marker without covered cells fails validation.
- A height bridge without required cross-height connections fails validation.
- Different-height adjacency without an explicit connection is not a fallback path.
- A marker footprint that does not resolve to the marker's `CellHeight` top surface fails validation for consumers that require standable cells.
- Ambiguous bridge height is an error; code must not guess from nearby terrain.
- Persistent state must not be loaded from or written into scene nodes.

## Acceptance

This architecture is acceptable when:

- future authors can create multiple layout variants from one base terrain scene without modifying the base;
- bridge placement, construction regions, deployment zones, resources, decorations, and obstacles can vary by layout;
- bridge gameplay uses explicit marker and connection data;
- cross-river bridges can act as ordinary same-height ground;
- height bridges preserve high-ground entrance strategy by requiring explicit entry connections;
- every map extraction yields a single final standable surface per cell;
- multiple provinces can reuse one layout without sharing persistent province or city state;
- invalid layout authoring produces actionable diagnostics instead of hidden fallback behavior.
- the first `scenes/city/` validation slice can demonstrate base-scene inheritance and layout-owned bridge, marker, decoration, obstacle, and connection authoring without touching DemoSite or BonefieldSite.
