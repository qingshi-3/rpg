# Semantic Map Markers Proposal

Status: Archived

## Purpose

Create a reusable semantic map marker system for authored map regions. The first implementation should replace point-like building-slot authoring with editor-visible, tile-grid-aligned rectangular regions and let Bonefield battle preparation consume authored deployment-zone markers. The same system should later feed entrances, chokepoints, reserve points, flank routes, ranged points, defend points, exploration points, and event spawns.

This proposal covers the system boundary and authoring contract. It does not directly implement combat AI, reserve behavior, new battle commands, or the bridge-battle vertical slice.

## Current Architecture

Map semantics are split across several paths:

- Facility slots use `WorldFacilitySlotEntity` scene nodes, but in the Godot editor they are effectively point markers. The footprint is resolved and drawn by runtime site presentation rather than being a clear editor-time `m*n` tile region.
- Deployment placement uses runtime candidate caches derived from walkable surfaces and attack direction ordering, so the visible battle-preparation zone is inferred from the map instead of authored as an intentional region.
- Entrance and exploration point definitions carry IDs and coordinate-like data separately from the scene-authored map.
- Future tactical markers such as chokepoints, reserve points, flank routes, ranged points, and defend points have no shared authoring model.

This makes authoring too abstract for spatial gameplay. The map is already built and visible in Godot, but many semantic regions still require either raw coordinates or runtime inference instead of direct visual placement on the tile grid.

## Expected Architecture

Add a reusable semantic marker authoring and extraction path:

- `SemanticMapMarker` is an abstract Godot editor-visible `Node2D` tool base.
- Business marker scenes inherit from it, for example `BuildingSlotMapMarker.tscn` and `DeploymentZoneMapMarker.tscn`.
- Markers live under a conventional `SemanticMarkers` scene root on map scenes.
- The marker anchor is the top-left tile cell.
- Width and height extend right and down by `m*n` cells.
- The editor preview draws the covered tile cells, outline, anchor, and label while authoring.
- The marker snaps to the map coordinate tile grid so authors do not edit raw coordinate fields.
- Runtime extracts marker nodes into pure data records before gameplay systems consume them.
- Deployment-zone preview color follows `DeploymentSide`: player is light green, enemy is light red, shared is neutral.

The pure data contract contains:

```text
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
```

The runtime data is the authority for downstream systems. The editor node and drawing logic are authoring tools only.

## Marker Types

Initial marker types:

- `BuildingSlot`
- `DeploymentZone`
- `Entrance`
- `ChokePoint`
- `Lane`
- `ReservePoint`
- `FlankRoute`
- `RangedPoint`
- `DefendPoint`
- `ExplorationPoint`
- `EventSpawn`

Consumers must filter by marker type and tags instead of assuming all markers are valid for all systems.

## First Implementation Slice

The first implementation should focus on building slots and Bonefield deployment zones:

1. Author `BuildingSlot` markers in the `BonefieldSite` scene under `SemanticMarkers`.
2. Instance business child scenes rather than placing the abstract `SemanticMapMarker` base directly.
3. Draw the full `m*n` tile region in the Godot editor.
4. Extract marker data at runtime.
5. Let site facility presentation consume `BuildingSlot` markers while preserving current build behavior.
6. Author `DeploymentZone` markers for the player and undead deployment areas in the same scene.
7. Let battle-preparation highlighting, drag validation, and automatic battle deployment prefer `DeploymentZone` markers by deployment side, with `FactionId` kept only as an optional concrete faction override.
8. Keep the full walkable-surface deployment cache available for legacy placement, exploration, and fallback behavior.
9. Keep old `WorldFacilitySlotEntity` behavior only as a migration fallback until the first map is converted.

After this slice is verified, entrances can migrate to the same marker path. Tactical combat markers should come after the authoring and extraction path is stable.

## Ownership

Definitions / Content owns static marker data shape and marker IDs.

Presentation / Authoring owns the Godot `SemanticMapMarker` tool node, scene preview, snap behavior, and visual labels.

Application owns extraction and validation that converts scene nodes into pure marker data for world-site and battle setup services.

Runtime consumes marker-derived facts only through snapshots or services. Runtime must not query Godot marker nodes directly.

## Failure Rules

- Missing `SemanticMarkers` root is allowed on maps that do not use semantic markers.
- Duplicate marker IDs on one map are invalid and must produce a low-noise diagnostic.
- Marker width and height are clamped to a safe authored range.
- A marker whose anchor cannot resolve to the coordinate tile grid is invalid.
- A marker footprint that leaves the known grid is invalid for consumers that require grid-backed cells.
- If both old facility slot entities and `BuildingSlot` markers exist for the same slot ID, the semantic marker path wins and the mismatch is logged.

## Non-Goals

- No combat AI implementation in this proposal.
- No reserve or reinforcement mechanics in this proposal.
- No full migration of every current coordinate-bearing definition in the first slice.
- No runtime dependence on editor drawing, `Node2D` state, or Godot physics callbacks.
- No requirement to open the Godot editor during automated verification.

## Acceptance Criteria

- Authors can place and resize a building slot as a visible tile-grid-aligned `m*n` region in the Godot editor.
- Building-slot editor preview is not merely a point marker.
- Runtime extraction returns the same anchor and size that the editor preview represents.
- Building-slot behavior remains functionally unchanged after consuming semantic markers.
- Battle preparation can highlight and validate authored `DeploymentZone` regions without treating the entire walkable map as the deployment area.
- The architecture allows entrances and tactical combat markers to migrate without creating separate point systems.
