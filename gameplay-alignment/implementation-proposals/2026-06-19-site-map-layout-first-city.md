# Site Map Layout First City

Status: Paused - Scaffold Verified, Detailed Authoring Deferred

## Origin

- Requirement: SMLA-001
- Design Proposal: `design-proposals/archived/2026-06-17-site-map-layout-authoring/`
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/site-map-layout-architecture.md`
  - `system-design/semantic-map-marker-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`

## Scope

Create the first independent city-map validation slice under `scenes/city/`:

- `scenes/city/base/plains_city_base.tscn` as a reusable terrain-only base.
- `scenes/city/layouts/plains_city_v0_layout.tscn` as an inherited layout variant.
- A shared `BridgeMapMarker` authoring marker, because bridge gameplay must be marker-driven instead of inferred from visual bridge tiles.
- Regression tests that protect base/layout ownership, inheritance, marker authoring, and DemoSite/BonefieldSite isolation.

The first layout demonstrates a plains city with low ground, river/water, high ground, one river bridge, one explicit high-ground connection, building slots, player/enemy deployment zones, one objective/resource-style marker, and layout-owned decoration/obstacle layers.

The checked-in layout is a structural validation scaffold, not the final authored city map. Detailed TileMapLayer painting, decoration placement, obstacle distribution, resource-point tuning, and bridge art fitting are deferred until the operational/city loop has been proven on `DemoSite`; otherwise detailed map work is likely to be reauthored against moving gameplay requirements.

## Non-Goals

- Do not modify `DemoSite` or `BonefieldSite`.
- Do not bind Strategic Management locations to the new layout in this slice.
- Do not implement persistent location state, destroyed bridge state, battle result writeback, or city-management UI.
- Do not build a broad city-map library, procedural generation, or automatic bridge-height guessing.
- Do not introduce same-coordinate multi-surface movement or under-bridge routing.
- Do not spend production authoring time on detailed city-map tile configuration until the `DemoSite` operational loop is stable enough to validate whether map content supports the real systems.

## Touched Systems

- System documentation acceptance route and proposal archive.
- Semantic map marker definitions and marker authoring scenes.
- City scene resource authoring under `scenes/city/`.
- World-site deployment regression tests as static scene/resource contract checks.

## GodotPrompter Skills

- `scene-organization`
- `resource-pattern`
- `csharp-godot`
- `godot-testing`

## Tests

- Add RED regression tests before implementation for:
  - accepted authority and archived proposal route are current;
  - `BridgeMapMarker` type, data fields, authoring script, and marker scene exist;
  - `plains_city_base.tscn` exists, is `BattleMapView` backed, and contains only base terrain layers;
  - `plains_city_v0_layout.tscn` exists, inherits the base, and owns bridge/decoration/obstacle/semantic marker content;
  - the layout includes stable ids for bridge, building slots, deployment zones, and objective/resource marker;
  - `DemoSite` and `BonefieldSite` are not edited as part of this slice.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` if the focused regression suite passes.
- Run `git diff --check`.

## Diagnostics

This slice is mostly authoring-resource validation. Runtime diagnostics are not required until layout extraction or Strategic Management binding is implemented. Test failure messages should name the missing scene, marker, or ownership boundary directly.

## Manual QA

Manual editor QA for detailed visual authoring is intentionally deferred.

Open `scenes/city/layouts/plains_city_v0_layout.tscn` in Godot and confirm:

- it inherits the plains base scene;
- base terrain layers are visible;
- layout-owned bridge, decoration, obstacle, and marker nodes are visible in the scene tree;
- semantic marker previews show the bridge, deployment zones, building slots, and objective/resource region;
- `DemoSite` and `BonefieldSite` still open unchanged.

Do not treat the current tile art, obstacle placement, or decoration distribution as accepted content quality.

## Acceptance

- Authority docs are current and SMLA-001 is archived.
- The first city base and inherited layout scenes exist under `scenes/city/`.
- The base scene has no actual bridge or content markers.
- The layout scene owns bridge/content markers and explicit height connection metadata.
- Bridge authoring uses a business marker subclass instead of per-instance marker type editing.
- Multiple locations can later bind this layout without scene nodes becoming persistent state.

## Reopen Gate

Resume detailed city-map authoring only after the operational/city loop has been validated on `DemoSite`. The next implementation slice should then decide which authored content is needed by real systems before investing in final TileMapLayer painting, bridge fitting, decoration, obstacle, resource, deployment, and building-slot layouts.

## Verification Evidence

- RED observed before implementation:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Failed on missing `Bridge` semantic marker type, missing `scenes/city/base/plains_city_base.tscn`, and missing `scenes/city/layouts/plains_city_v0_layout.tscn`.
- Automated verification after implementation:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - `git diff --check`

Manual Godot editor QA and detailed map authoring remain deferred until the `DemoSite` operational loop is ready to validate the map content.
