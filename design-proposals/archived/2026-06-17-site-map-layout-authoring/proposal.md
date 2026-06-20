# Site Map Layout Authoring Proposal

Status: Accepted

## Relationship Metadata

- Requirement Id: SMLA-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: `system-design/README.md`, `system-design/strategic-management-system-architecture.md`, `system-design/semantic-map-marker-architecture.md`
- Amended By: None
- Affected Authority Documents:
  - `system-design/README.md`
  - `system-design/site-map-layout-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/semantic-map-marker-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-19-site-map-layout-first-city.md`

## Current Architecture

Strategic Management already owns strategic-location definitions and long-term location state. Semantic map markers already define authored regions for building slots, deployment zones, objective zones, and future tactical markers. Battle navigation already supports height-aware surfaces and explicit height links.

The current authority does not yet define a new reusable site-map layout authoring module. Existing site maps such as DemoSite and BonefieldSite are complete authored map scenes, and current definitions can still point to a battle scene path or map definition id. There is no accepted contract for:

- reusable base terrain scenes;
- Godot inherited layout variants;
- layout-scoped bridge, resource, decoration, obstacle, and semantic marker authoring;
- strategic-location binding to a layout id;
- reusable layout templates with per-location persistent state isolation.

This leaves future city and strategic-location maps at risk of either copying one complete DemoSite-like scene everywhere or mixing persistent location facts into scene nodes.

## Expected Architecture

Introduce a new Site Map Layout authoring architecture.

The module separates:

```text
BaseTerrainScene -> LayoutVariantScene -> SiteMapLayoutDefinition -> StrategicLocation binding -> per-location persistent state
```

Base terrain scenes are reusable Godot scenes that contain stable terrain structure only: water, low ground, high ground, roads, terrain boundaries, and base TileMapLayer organization. They do not own bridges, building slots, deployment zones, resource points, content decorations, event points, or location-specific obstacles.

Layout variant scenes inherit from a base terrain scene and add the actual content layout: bridges, decorations, obstacles, resource markers, semantic markers, deployment regions, objective regions, event points, and explicit height connections. A layout variant is the player-entered map template. Multiple strategic locations may bind to the same layout variant, but persistent facts must be stored by strategic location id plus stable layout/marker ids.

The first gameplay-height model is constrained:

```text
h=-1 water / pit / deep gap
h=0 low ground, normal roads, same-height river bridges
h=1 high ground, walls, platforms, height bridges
h=2 reserved for rare future use
```

Each grid cell resolves to at most one final standable surface. Visual TileMapLayers may overlap freely, but gameplay extraction produces a single top surface for movement, deployment, objective markers, and site interaction. Same-height walkable neighbors may connect normally. Different-height movement is forbidden unless an explicit connection defines the entry.

Bridge gameplay is marker-driven, not guessed from art:

- bridge TileMapLayer art may cover water, banks, and high-ground edges;
- bridge markers define bridge id, bridge kind, covered gameplay cells, and surface height;
- same-height river bridges are ordinary walkable surfaces for crossing water;
- height bridges use the upper bridge-surface height and require explicit low-to-high connections;
- bridge placement belongs to layout variants, not base terrain.

DemoSite and BonefieldSite are not changed by this design proposal. They remain existing maps and reference points. Any implementation must introduce the new module independently and migrate or reuse old maps only through later focused implementation proposals.

The first authored module target should live under `scenes/city/`. That directory is the home for the new city-map authoring base scenes, inherited layout variants, and city-specific map markers. The first implementation city should be a small plains-city validation slice, not a full content migration.

Initial target:

```text
scenes/city/base/plains_city_base.tscn
scenes/city/layouts/plains_city_v0_layout.tscn
```

The base scene validates reusable terrain structure only. The layout scene inherits from it and validates the first content configuration: bridge marker, explicit high-ground entry connection, building slots, deployment zones, one resource point, and layout-owned decorations or obstacles.

## Non-Goals

- No code, scene, or resource implementation in this design proposal.
- No DemoSite or BonefieldSite modification.
- No broad city-map library in the first implementation slice.
- No procedural map generation.
- No automatic bridge-height guessing from neighboring terrain or tile art.
- No same-coordinate multi-surface movement, bridge-underpass routing, or full multi-floor map simulation.
- No real-time dynamic navigation-topology rebuild for destructible bridges in the first layout architecture.
- No generic scripting language for layout rules.

## Acceptance Criteria

- `system-design/site-map-layout-architecture.md` defines base terrain scenes, layout variant scenes, layout extraction, validation, strategic binding, bridge markers, and state isolation.
- `system-design/strategic-management-system-architecture.md` states that Strategic Management definitions bind strategic locations to site map layouts while persistent map facts remain location-owned state.
- `system-design/semantic-map-marker-architecture.md` includes bridge markers and height-specific marker validation.
- `system-design/README.md` routes future agents to the new site map layout architecture.
- The accepted architecture names `scenes/city/` as the first module location and defines the initial plains-city validation slice.
- Future implementation work starts only after the expected copies are merged into authority documents, the proposal is archived, and a focused implementation proposal defines scope, tests, diagnostics, manual QA, and acceptance evidence.
