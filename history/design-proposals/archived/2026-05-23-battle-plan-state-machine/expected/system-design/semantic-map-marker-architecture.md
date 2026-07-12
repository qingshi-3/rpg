# Semantic Map Marker Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports the accepted strategic-location and spatial battle direction. Maps are authored gameplay spaces, so tactical and management semantics should be placed visually on the map instead of maintained as raw coordinate lists.

## Responsibility

The semantic marker system defines reusable map regions for authored gameplay meaning:

- building slots in city or strategic-location interiors;
- deployment zones and entrances for battle handoff;
- objective zones and route hints for battle-group planning;
- event spawns and direct strategic-location interaction markers;
- tactical combat regions such as chokepoints, lanes, reserve points, flank routes, ranged points, and defend points.

The system owns marker authoring, extraction, validation, and pure-data handoff. It does not decide final combat AI, building rules, settlement, or battle outcomes.

## Authoring Model

Map scenes may define a conventional `SemanticMarkers` root node. Children under that root must be business-specific marker scene instances such as `BuildingSlotMapMarker.tscn` or `DeploymentZoneMapMarker.tscn`.

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
| `BuildingSlotMapMarker.tscn` / `BuildingSlotMapMarker` | `BuildingSlot` | none in the first slice |
| `DeploymentZoneMapMarker.tscn` / `DeploymentZoneMapMarker` | `DeploymentZone` | `DeploymentSide`, optional `FactionId`, `Priority` |
| `ObjectiveZoneMapMarker.tscn` / `ObjectiveZoneMapMarker` | `ObjectiveZone` | `ObjectiveRole`, `DeploymentSide`, optional `FactionId`, `Priority` |

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
AnchorCell
CellHeight
Width
Height
FactionId
Priority
Tags
SourcePath
```

The pure data record is the only shape consumed by Application, Runtime, reports, and diagnostics. Godot marker nodes and editor preview drawing are not gameplay authority.

## Marker Types

Initial marker types are:

| Type | First Consumer |
|---|---|
| `BuildingSlot` | Site facility presentation and build actions. |
| `DeploymentZone` | Battle deployment preparation. |
| `ObjectiveZone` | Battle-group target-area selection and plan handoff. |
| `Entrance` | Battle handoff and known entrance selection. |
| `ChokePoint` | Tactical AI and battle reports. |
| `Lane` | Tactical AI movement and pressure templates. |
| `ReservePoint` | Reserve and reinforcement mechanics. |
| `FlankRoute` | Tactical AI and player-readable map planning. |
| `RangedPoint` | Ranged pressure positioning. |
| `DefendPoint` | Hold-point and defense templates. |
| `EventSpawn` | Site and battle event placement. |

Consumers must filter by type and tags. A marker being present does not imply every system may use it.

## Ownership By Layer

Definitions / Content owns stable marker IDs, marker type names, and marker-derived definition references.

Presentation / Authoring owns the Godot `SemanticMapMarker` node, editor preview drawing, grid snapping, and scene placement.

Application owns marker extraction and validation. It converts scene-authored markers into pure data for site management, deployment, objective selection, entrance selection, event placement, and future battle setup.

Runtime / StateMachine consumes marker-derived facts only through snapshots or Application services. Runtime does not query scene marker nodes.

Report and Settlement may reference marker IDs only after events or results expose those IDs. They must not rediscover marker data independently.

## Building Slot Migration

The first implementation slice migrates building-slot authoring.

Current facility slots may keep `FacilitySlotDefinition` for display name, allowed facilities, initial facility, and tags. Location and footprint should come from a `SemanticMapMarker` with `MarkerType = BuildingSlot` and a matching marker or slot ID.

`WorldFacilitySlotEntity` should stop being the authoritative authoring shape for a slot footprint. It may remain as a visual or migration adapter while maps are converted, but the target authoring path is:

```text
SemanticMapMarker(type=BuildingSlot)
-> extracted marker data
-> site facility layout
-> current build behavior
```

If both a legacy slot entity and a semantic marker describe the same slot, the semantic marker wins and a diagnostic should identify the mismatch.

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

Battle preparation consumes `ObjectiveZone` markers as the player-selectable target areas for each battle group.

Objective zones should be authored as readable tactical regions:

```text
SemanticMapMarker(type=ObjectiveZone, ObjectiveRole=GateApproach/HighGround/FlankRoute/Core/Reserve/DefendPoint, DeploymentSide=Player/Enemy/Any, optional FactionId=...)
-> extracted marker data
-> objective-zone candidate list
-> battle-preparation tactical overview and route preview
-> accepted BattleGroupPlan.ObjectiveZoneId
-> Runtime objective-zone movement and report attribution
```

Objective zones are not deployment zones. A map may have a friendly deployment area on the left and several objective zones distributed across the horizontal battlefield. Objective-zone markers may overlap with lane, chokepoint, ranged point, reserve point, flank route, or defend point markers when that overlap is intentional map semantics.

If a battle kind requires player-selected objectives and no valid objective-zone markers exist, battle preparation must fail explicitly instead of fabricating hidden target cells. If a simple field-intercept battle does not require objective choice, missing objective zones are valid and the runtime may use battle-kind defaults.

## Validation Rules

- Marker IDs must be unique within one map.
- Width and height are clamped to a safe authored range.
- The anchor must resolve to the coordinate tile grid.
- Covered cells are computed from the top-left anchor by extending right and down.
- Consumers that require grid-backed cells must reject markers whose footprint leaves the known grid.
- Markers may overlap unless a consumer explicitly forbids overlap for its type.
- A missing `SemanticMarkers` root is valid for maps that have no semantic marker requirements.

## Failure Rules

Invalid marker extraction should produce diagnostics and skip invalid markers instead of fabricating cell coordinates.

Consumer failures should be explicit:

- site facility layout reports missing or invalid `BuildingSlot` markers;
- deployment preparation reports missing deployment or entrance markers when a map requires them;
- battle preparation reports missing objective-zone markers when the battle kind requires player-selected objectives;
- tactical AI treats missing optional tactical markers as unavailable tactical hints, not as hard failure.

## Acceptance

This architecture is acceptable when:

- building slots can be authored as visible tile-grid-aligned regions in the Godot editor;
- editor preview and runtime extraction agree on anchor, width, height, and covered cells;
- Application receives pure marker data without depending on editor drawing or scene node state during gameplay;
- building-slot behavior remains stable after consuming semantic marker data;
- battle-preparation deployment highlighting and validation can consume authored side-based `DeploymentZone` markers without restricting unrelated full-map placement consumers;
- battle-preparation objective selection can consume authored `ObjectiveZone` markers and carry objective ids into battle-group plans;
- deployment, entrance, event, and tactical marker consumers can be added without inventing separate coordinate systems.
