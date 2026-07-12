# Semantic Map Marker Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports the accepted strategic-location and spatial battle direction. Maps are authored gameplay spaces, so tactical and management semantics should be placed visually on the map instead of maintained as raw coordinate lists.

## Responsibility

The semantic marker system defines reusable map regions for authored gameplay meaning:

- strategic construction regions in city or strategic-location interiors;
- deployment zones and entrances for battle handoff;
- optional objective zones and route hints for tactical map semantics;
- event spawns and direct strategic-location interaction markers;
- tactical combat regions such as chokepoints, lanes, reserve points, flank routes, ranged points, and defend points.

The system owns marker authoring, extraction, validation, and pure-data handoff. It does not decide final combat AI, building rules, settlement, or battle outcomes.

## Authoring Model

Map scenes may define a conventional `SemanticMarkers` root node. Marker scene instances such as `ConstructionRegionMapMarker.tscn` or `DeploymentZoneMapMarker.tscn` may be placed directly under that root or under plain `Node2D` grouping folders such as `ConstructionRegions`, `DeploymentZones`, or `ObjectiveZones`. These grouping nodes exist only for editor readability and must not carry gameplay facts.

`SemanticMapMarker` is an abstract Godot editor tool base. It owns only shared authoring mechanics:

```text
MarkerId
Width
Height
CellHeight
Tags
SnapToGrid
DrawEditorPreview
```

Business semantics belong to subclasses:

| Scene / Script | Fixed Marker Type | Extra Fields |
|---|---|---|
| `ConstructionRegionMapMarker.tscn` / `ConstructionRegionMapMarker` | `ConstructionRegion` | `Priority` |
| `DeploymentZoneMapMarker.tscn` / `DeploymentZoneMapMarker` | `DeploymentZone` | `DeploymentSide`, optional `FactionId`, `Priority` |
| `ObjectiveZoneMapMarker.tscn` / `ObjectiveZoneMapMarker` | `ObjectiveZone` | `ObjectiveRole`, `DeploymentSide`, optional `FactionId`, `Priority` |
| `BridgeMapMarker.tscn` / `BridgeMapMarker` | `Bridge` | `BridgeKind`, `ConnectionIds`, `Priority` |

The marker anchor is the top-left tile cell. Width and height extend right and down by `m*n` cells. The node draws the covered tile cells, outline, anchor, and label in the editor so authors can see the region while building the map. Authors should not need to edit raw cell coordinates for ordinary region placement.

For `DeploymentZone` markers, `DeploymentSide` is the normal authoring contract. `Player` means the zone is available to the battle request's player-side force bucket, `Enemy` means enemy-side force bucket, and `Any` means shared. `FactionId` is optional and reserved for a concrete faction-specific override or legacy compatibility; ordinary battle deployment zones should not require marker names or concrete faction IDs.

Deployment marker editor previews use side-specific colors: player zones render as light green, enemy zones as light red, and shared zones as neutral blue. This visual distinction is authoring feedback only; runtime consumers still read `SemanticMapMarkerData`.

The marker node snaps its position to the map coordinate tile grid when grid snapping is enabled. If a coordinate layer cannot be found, the marker keeps its authored position but validation reports that it cannot be converted to grid data.

## Runtime Data

Runtime extraction converts authoring nodes into pure marker data:

```text
MapId
MarkerId
MarkerType
DeploymentSide
ObjectiveRole
AnchorCell
CellHeight
Width
Height
FactionId
Priority
Tags
SourcePath
BridgeKind
ConnectionIds
```

The pure data record is the only shape consumed by Application, Runtime, reports, and diagnostics. Godot marker nodes and editor preview drawing are not gameplay authority. Runtime extraction walks marker descendants recursively so editor-only grouping folders do not affect marker semantics.

## Marker Types

Initial marker types are:

| Type | First Consumer |
|---|---|
| `ConstructionRegion` | Strategic Management city-building placement preview and placement-region resolution. |
| `DeploymentZone` | Battle deployment preparation. |
| `ObjectiveZone` | Optional tactical planning, scenario intent, route hints, reports, and future objective-selection modes. |
| `Entrance` | Battle handoff and known entrance selection. |
| `Bridge` | Site Map Layout extraction, bridge validation, destructible bridge state hooks, and optional tactical route hints. |
| `ChokePoint` | Tactical AI and battle reports. |
| `Lane` | Tactical AI movement and pressure templates. |
| `ReservePoint` | Reserve and reinforcement mechanics. |
| `FlankRoute` | Tactical AI and player-readable map planning. |
| `RangedPoint` | Ranged pressure positioning. |
| `DefendPoint` | Hold-point and defense templates. |
| `EventSpawn` | Site and battle event placement. |

Consumers must filter by type and tags. A marker being present does not imply every system may use it.

Bridge markers describe bridge gameplay facts. Bridge art may overlap water, banks, roads, or high-ground edges, but bridge height, bridge footprint, and cross-height entry points must come from bridge markers and explicit map connections rather than visual tile overlap.

## Ownership By Layer

Definitions / Content owns stable marker IDs, marker type names, and marker-derived definition references.

Presentation / Authoring owns the Godot `SemanticMapMarker` node, editor preview drawing, grid snapping, and scene placement.

Application owns marker extraction and validation. It converts scene-authored markers into pure data for site management, deployment, objective selection, entrance selection, event placement, and future battle setup.

Runtime / StateMachine consumes marker-derived facts only through snapshots or Application services. Runtime does not query scene marker nodes.

Report and Settlement may reference marker IDs only after events or results expose those IDs. They must not rediscover marker data independently.

## Strategic Construction Region Consumption

Strategic city construction consumes `ConstructionRegion` markers as the map-authored presentation shape for Strategic Management construction regions.

The marker id must match a `StrategicConstructionRegionDefinition.RegionId`. The marker footprint supplies the visible map region used for hover highlights, mouse-follow building footprint preview, and click-to-place region resolution. Strategic Management definitions and rules remain the authority for buildable region membership, bounds, overlap, resource costs, explicit building eligibility, and durable building state. Construction-region markers must not carry allowed building categories; category remains building metadata for UI/capability use, not region placement legality.

The current first playable city may use `demo_site.tscn` as the test map and should expose three construction-region markers for the foundation loop: economy, military, and civic/support. `BuildingSlot` markers and old site facility-slot presentation are retired; Strategic Management building placement must route only through construction-region markers, Strategic Management rules, and the `BuildCityBuilding` command.

The target placement flow is:

```text
SemanticMapMarker(type=ConstructionRegion, MarkerId=StrategicConstructionRegionDefinition.RegionId)
-> extracted marker data
-> Presentation placement region highlight and footprint preview
-> StrategicManagementRules.GetBuildingPlacementFailureReason
-> BuildCityBuilding command on confirmed click
```

## Deployment Zone Consumption

Battle preparation consumes `DeploymentZone` markers as an authored constraint on top of the full walkable-surface deployment cache.

The full cache remains available for legacy placement, terrain reconciliation, and fallback behavior. Deployment-specific consumers should ask for deployment-zone candidates by deployment side, concrete faction override, and direction:

```text
SemanticMapMarker(type=DeploymentZone, DeploymentSide=Player/Enemy/Any, optional FactionId=...)
-> extracted marker data
-> side-aware deployment-zone candidate cache
-> battle-preparation highlight, drag validation, and automatic force placement
```

If no authored deployment-zone markers exist, battle preparation may fall back to the full walkable-surface cache. If side markers exist, player-side and enemy-side force placement and drag validation should prefer their side's cells instead of treating the whole map as deployable. Runtime code must not hardcode author marker node names or marker IDs for deployment routing.

## Objective Zone Consumption

Objective-zone markers are optional tactical semantics. Battle preparation no longer requires them as player-selectable target areas for each battle group in the accepted destination-beacon flow.

Objective zones should be authored as readable tactical regions:

```text
SemanticMapMarker(type=ObjectiveZone, ObjectiveRole=GateApproach/HighGround/FlankRoute/Core/Reserve/DefendPoint, DeploymentSide=Player/Enemy/Any, optional FactionId=...)
-> extracted marker data
-> optional objective-zone candidate list
-> optional tactical overview, scenario intent, route hint, or report attribution
-> future accepted objective-selection mode, if one is reintroduced
```

Objective zones are not deployment zones. A map may have a friendly deployment area on the left and several objective zones distributed across the horizontal battlefield. Objective-zone markers may overlap with lane, chokepoint, ranged point, reserve point, flank route, or defend point markers when that overlap is intentional map semantics.

If a future battle kind explicitly requires player-selected objectives, missing valid objective-zone markers must fail explicitly instead of fabricating hidden target cells. Battle kinds that use runtime destination beacons do not require objective-zone markers for launch.

Destination beacons are runtime command objects created from player input. They are not `ObjectiveZone` markers and are not authored under the `SemanticMarkers` root.

## Validation Rules

- Marker IDs must be unique within one map.
- Width and height are clamped to a safe authored range.
- The anchor must resolve to the coordinate tile grid.
- Covered cells are computed from the top-left anchor by extending right and down.
- Consumers that require grid-backed cells must reject markers whose footprint leaves the known grid.
- Consumers that require standable cells must verify that marker footprints resolve to the marker's `CellHeight` top surface.
- `Bridge` markers must declare a bridge kind and covered gameplay cells; height-changing bridges must reference or be paired with explicit height connections.
- Markers may overlap unless a consumer explicitly forbids overlap for its type.
- A missing `SemanticMarkers` root is valid for maps that have no semantic marker requirements.

## Failure Rules

Invalid marker extraction should produce diagnostics and skip invalid markers instead of fabricating cell coordinates.

Consumer failures should be explicit:

- deployment preparation reports missing deployment or entrance markers when a map requires them;
- battle preparation reports missing objective-zone markers only when a future battle kind explicitly requires player-selected objectives;
- site map layout validation reports ambiguous bridge height, missing bridge cells, or missing height connections for height-changing bridges;
- tactical AI treats missing optional tactical markers as unavailable tactical hints, not as hard failure.

## Acceptance

This architecture is acceptable when:

- strategic construction regions can be authored as visible tile-grid-aligned regions in the Godot editor;
- editor preview and runtime extraction agree on anchor, width, height, and covered cells;
- Application receives pure marker data without depending on editor drawing or scene node state during gameplay;
- Strategic Management building placement consumes `ConstructionRegion` marker data without depending on old facility slots;
- bridge behavior can be authored through explicit markers without inferring gameplay height from visual bridge art;
- battle-preparation deployment highlighting and validation can consume authored side-based `DeploymentZone` markers without restricting unrelated full-map placement consumers;
- optional objective-selection or scenario systems can consume authored `ObjectiveZone` markers without making them mandatory for destination-beacon battles;
- deployment, entrance, event, and tactical marker consumers can be added without inventing separate coordinate systems.
