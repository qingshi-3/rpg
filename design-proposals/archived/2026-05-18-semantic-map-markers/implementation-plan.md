# Semantic Map Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable Godot-authored semantic map marker path, migrate Bonefield building slots to editor-visible tile-grid-aligned markers, and let Bonefield battle preparation consume authored deployment-zone markers.

**Architecture:** `SemanticMapMarker` is an abstract `[Tool]` authoring base. Map scenes instance business child scenes such as `BuildingSlotMapMarker.tscn` and `DeploymentZoneMapMarker.tscn` under `SemanticMarkers`. The base implements the source interface and shared grid preview mechanics; subclasses own marker type, business-specific exports, and preview colors. Application extraction converts marker sources into pure data; `WorldSiteRoot` consumes extracted `BuildingSlot` markers first and keeps legacy `WorldFacilitySlotEntity` nodes as a migration fallback. Battle preparation consumes extracted side-based `DeploymentZone` markers through the deployment cache while the full walkable-surface cache remains available to legacy placement and exploration consumers.

**Tech Stack:** Godot 4.5 C#, `.tscn` authored scenes, existing console regression project `tests/WorldSiteDeploymentCacheRegression`, low-concurrency `.NET` build.

---

## File Structure

- Create `src/Definitions/Maps/SemanticMapMarkerType.cs`: marker type enum shared by authoring and consumers.
- Create `src/Application/Maps/ISemanticMapMarkerSource.cs`: source interface implemented by editor nodes so Application extraction does not depend on Presentation classes.
- Create `src/Application/Maps/SemanticMapMarkerData.cs`: pure runtime data record for extracted marker facts.
- Create `src/Application/Maps/SemanticMapMarkerExtractionResult.cs`: extraction result with valid markers and diagnostics.
- Create `src/Application/Maps/SemanticMapMarkerExtractor.cs`: traverses scene nodes and extracts marker data from `ISemanticMapMarkerSource`.
- Create `src/Presentation/Maps/SemanticMapMarker.cs`: abstract `[Tool] Node2D` base that snaps to `TileMapLayer` grid, draws `m*n` region preview, and implements marker extraction.
- Create `src/Presentation/Maps/BuildingSlotMapMarker.cs` and `src/Presentation/Maps/DeploymentZoneMapMarker.cs`: business subclasses that fix marker type and expose only relevant authoring fields.
- Create `scenes/maps/markers/SemanticMapMarker.tscn`, `BuildingSlotMapMarker.tscn`, and `DeploymentZoneMapMarker.tscn`: abstract base scene plus child marker scenes.
- Modify `src/Presentation/Common/GameUiSceneFactory.cs`: add reusable `WorldFacilitySlotEntityScenePath`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`: add semantic marker cache and constants.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteMapPresentation.cs`: prefer `BuildingSlot` semantic markers, instantiate slot visuals from the existing scene template, and fall back to legacy `FacilitySlots`.
- Modify `src/Application/World/WorldSiteRuntimeDeploymentCache.cs`: keep full-surface candidates and add authored deployment-zone candidates by deployment side, with optional concrete faction override buckets.
- Modify `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`: derive side-aware deployment-zone candidates from extracted `DeploymentZone` markers.
- Modify `src/Application/World/WorldSiteBattleDeploymentPreparer.cs`: prefer side-aware deployment-zone candidates for automatic force placement.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs`, `WorldSiteRoot.DeploymentFootprint.cs`, and `WorldSiteRoot.SiteInteraction.cs`: highlight and validate battle-preparation deployment against marker zones.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.Types.cs`: keep facility layout source path if useful for diagnostics.
- Modify `scenes/world/sites/impl/BonefieldSite.tscn`: add `SemanticMarkers` root, two `BuildingSlotMapMarker` instances, and two `DeploymentZoneMapMarker` instances.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`: register semantic marker regression cases.
- Create or modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SemanticMarkers.cs`: focused tests for contract, scene authoring, and source boundaries.

## Task 1: Regression Tests

**Files:**
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- Create `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SemanticMarkers.cs`

- [ ] **Step 1: Add test registrations**

Add these registrations near the presentation architecture cases:

```csharp
WorldSiteDeploymentCacheRegressionCases.Run("semantic map marker contract exposes building slot regions", WorldSiteDeploymentCacheRegressionCases.SemanticMapMarkerContractExposesBuildingSlotRegions);
WorldSiteDeploymentCacheRegressionCases.Run("bonefield building slots are authored as semantic markers", WorldSiteDeploymentCacheRegressionCases.BonefieldBuildingSlotsAreAuthoredAsSemanticMarkers);
WorldSiteDeploymentCacheRegressionCases.Run("world site root prefers semantic building slot markers", WorldSiteDeploymentCacheRegressionCases.WorldSiteRootPrefersSemanticBuildingSlotMarkers);
WorldSiteDeploymentCacheRegressionCases.Run("semantic marker extraction stays out of battle runtime", WorldSiteDeploymentCacheRegressionCases.SemanticMarkerExtractionStaysOutOfBattleRuntime);
```

- [ ] **Step 2: Add failing tests**

Create the test file with these cases:

```csharp
using Rpg.Application.Maps;
using Rpg.Definitions.Maps;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void SemanticMapMarkerContractExposesBuildingSlotRegions()
{
    AssertEqual(SemanticMapMarkerType.BuildingSlot, Enum.Parse<SemanticMapMarkerType>("BuildingSlot"), "building slot marker type");
    SemanticMapMarkerData marker = new()
    {
        MapId = "bonefield",
        MarkerId = "mine_slot_01",
        MarkerType = SemanticMapMarkerType.BuildingSlot,
        AnchorCell = new Godot.Vector2I(18, 12),
        CellHeight = 0,
        Width = 3,
        Height = 2,
        SourcePath = "SemanticMarkers/mine_slot_01"
    };

    AssertEqual(new Godot.Vector2I(18, 12), marker.AnchorCell, "marker anchor");
    AssertEqual(3, marker.Width, "marker width");
    AssertEqual(2, marker.Height, "marker height");
    AssertTrue(marker.CoveredCells.SequenceEqual(new[]
    {
        new Godot.Vector2I(18, 12),
        new Godot.Vector2I(19, 12),
        new Godot.Vector2I(20, 12),
        new Godot.Vector2I(18, 13),
        new Godot.Vector2I(19, 13),
        new Godot.Vector2I(20, 13)
    }), "covered cells should extend right then down from top-left anchor");
}

internal static void BonefieldBuildingSlotsAreAuthoredAsSemanticMarkers()
{
    string scene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "sites", "impl", "BonefieldSite.tscn"));
    AssertTrue(scene.Contains("[node name=\"SemanticMarkers\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal), "bonefield should have SemanticMarkers root");
    AssertTrue(scene.Contains("type=\"Node2D\" parent=\"SemanticMarkers\"", StringComparison.Ordinal), "bonefield should place marker nodes under SemanticMarkers");
    AssertTrue(scene.Contains("SemanticMapMarker.cs", StringComparison.Ordinal), "bonefield should reference SemanticMapMarker script");
    AssertTrue(scene.Contains("MarkerType = 0", StringComparison.Ordinal) || scene.Contains("MarkerType = 0", StringComparison.Ordinal), "building slot marker enum value should be authored");
    AssertTrue(scene.Contains("MarkerId = \"mine_slot_01\"", StringComparison.Ordinal), "mine slot marker should be authored");
    AssertTrue(scene.Contains("MarkerId = \"tower_slot_01\"", StringComparison.Ordinal), "tower slot marker should be authored");
    AssertTrue(scene.Contains("Width = 3", StringComparison.Ordinal) && scene.Contains("Height = 2", StringComparison.Ordinal), "mine slot footprint should be visible as 3x2");
    AssertTrue(scene.Contains("Width = 2", StringComparison.Ordinal) && scene.Contains("Height = 2", StringComparison.Ordinal), "tower slot footprint should be visible as 2x2");
}

internal static void WorldSiteRootPrefersSemanticBuildingSlotMarkers()
{
    string source = ReadWorldSiteRootSource();
    AssertTrue(source.Contains("SemanticMapMarkerExtractor", StringComparison.Ordinal), "world site root should extract semantic markers");
    AssertTrue(source.Contains("SemanticMapMarkerType.BuildingSlot", StringComparison.Ordinal), "world site root should filter building slot markers");
    AssertTrue(source.Contains("BuildFacilitySlotEntitiesFromSemanticMarkers", StringComparison.Ordinal), "semantic building slot path should be explicit");
    AssertTrue(source.Contains("RefreshLegacyFacilitySlotEntities", StringComparison.Ordinal), "legacy slot path should remain a named fallback");
}

internal static void SemanticMarkerExtractionStaysOutOfBattleRuntime()
{
    string runtimeSource = string.Join(
        "\n",
        Directory.GetFiles(Path.Combine(ProjectRoot(), "src", "Runtime"), "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path)
            .Select(File.ReadAllText));
    AssertTrue(!runtimeSource.Contains("SemanticMapMarker", StringComparison.Ordinal), "battle runtime should not query semantic marker nodes or extraction directly");
}
}
```

- [ ] **Step 3: Verify RED**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected before implementation: compile failures mention missing `SemanticMapMarkerType`, `SemanticMapMarkerData`, or missing test registrations.

## Task 2: Marker Contract And Extraction

**Files:**
- Create `src/Definitions/Maps/SemanticMapMarkerType.cs`
- Create `src/Application/Maps/ISemanticMapMarkerSource.cs`
- Create `src/Application/Maps/SemanticMapMarkerData.cs`
- Create `src/Application/Maps/SemanticMapMarkerExtractionResult.cs`
- Create `src/Application/Maps/SemanticMapMarkerExtractor.cs`

- [ ] **Step 1: Add marker type enum**

```csharp
namespace Rpg.Definitions.Maps;

public enum SemanticMapMarkerType
{
    BuildingSlot = 0,
    DeploymentZone = 1,
    Entrance = 2,
    ChokePoint = 3,
    Lane = 4,
    ReservePoint = 5,
    FlankRoute = 6,
    RangedPoint = 7,
    DefendPoint = 8,
    ExplorationPoint = 9,
    EventSpawn = 10
}
```

- [ ] **Step 2: Add deployment side enum**

```csharp
namespace Rpg.Definitions.Maps;

public enum SemanticDeploymentSide
{
    Any = 0,
    Player = 1,
    Enemy = 2
}
```

- [ ] **Step 3: Add source interface**

```csharp
namespace Rpg.Application.Maps;

public interface ISemanticMapMarkerSource
{
    bool TryResolveSemanticMarkerData(string mapId, out SemanticMapMarkerData data, out string failureReason);
}
```

- [ ] **Step 4: Add pure data record**

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Application.Maps;

public sealed class SemanticMapMarkerData
{
    public string MapId { get; set; } = "";
    public string MarkerId { get; set; } = "";
    public SemanticMapMarkerType MarkerType { get; set; } = SemanticMapMarkerType.BuildingSlot;
    public SemanticDeploymentSide DeploymentSide { get; set; } = SemanticDeploymentSide.Any;
    public Vector2I AnchorCell { get; set; }
    public int CellHeight { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public string FactionId { get; set; } = "";
    public int Priority { get; set; }
    public List<string> Tags { get; } = new();
    public string SourcePath { get; set; } = "";

    public IReadOnlyList<Vector2I> CoveredCells => BuildCoveredCells();

    private IReadOnlyList<Vector2I> BuildCoveredCells()
    {
        int width = System.Math.Clamp(Width, 1, 64);
        int height = System.Math.Clamp(Height, 1, 64);
        return Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width).Select(x => new Vector2I(AnchorCell.X + x, AnchorCell.Y + y)))
            .ToArray();
    }
}
```

- [ ] **Step 5: Add extraction result and extractor**

Implement `SemanticMapMarkerExtractionResult` with `Markers` and `Diagnostics` lists. Implement `SemanticMapMarkerExtractor.Extract(Node root, string mapId)` by traversing descendants, collecting `ISemanticMapMarkerSource`, skipping invalid entries with diagnostics, and reporting duplicate marker IDs.

- [ ] **Step 6: Verify GREEN for contract compile**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: contract tests compile; scene and source-boundary tests still fail until later tasks.

## Task 3: Godot Authoring Node

**Files:**
- Create `src/Presentation/Maps/SemanticMapMarker.cs`
- Create `src/Presentation/Maps/BuildingSlotMapMarker.cs`
- Create `src/Presentation/Maps/DeploymentZoneMapMarker.cs`
- Create `scenes/maps/markers/SemanticMapMarker.tscn`
- Create `scenes/maps/markers/BuildingSlotMapMarker.tscn`
- Create `scenes/maps/markers/DeploymentZoneMapMarker.tscn`

- [ ] **Step 1: Implement abstract `[Tool]` marker base and business marker children**

Create an abstract `Node2D` base with exported `MarkerId`, `Width`, `Height`, `CellHeight`, `Tags`, `SnapToGrid`, and `DrawEditorPreview`.

Create business subclasses:

- `BuildingSlotMapMarker`: fixed `BuildingSlot` type.
- `DeploymentZoneMapMarker`: fixed `DeploymentZone` type, exported `DeploymentSide`, optional `FactionId`, and `Priority`. Player preview is light green; enemy preview is light red.

The base should:

- find the nearest map coordinate `TileMapLayer` by preferring `BattleMapView.CoordinateLayer`;
- resolve the anchor from `GlobalPosition` through `LocalToMap`;
- snap to the coordinate layer's `MapToLocal(anchor)` when `SnapToGrid` is true;
- draw the covered footprint with coordinate-layer geometry when `DrawEditorPreview` is true;
- implement `ISemanticMapMarkerSource.TryResolveSemanticMarkerData`.

- [ ] **Step 2: Verify compile state**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: marker class compiles; scene and consumer tests still fail.

## Task 4: Building Slot Consumer

**Files:**
- Modify `src/Presentation/Common/GameUiSceneFactory.cs`
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteMapPresentation.cs`
- Modify `src/Presentation/World/Sites/WorldSiteRoot.Types.cs`

- [ ] **Step 1: Add facility slot scene path**

Add `WorldFacilitySlotEntityScenePath = "res://scenes/world/site_interactions/WorldFacilitySlotEntity.tscn"` to `GameUiSceneFactory`.

- [ ] **Step 2: Cache semantic markers after map load**

Add a `SemanticMapMarkerExtractionResult` field to `WorldSiteRoot`, populate it after `_activeSiteMap` and grid runtime data are ready, and log diagnostics.

- [ ] **Step 3: Prefer semantic building slots**

Split `RefreshFacilitySlotEntities` into:

```text
RefreshFacilitySlotEntities
-> BuildFacilitySlotEntitiesFromSemanticMarkers
-> RefreshLegacyFacilitySlotEntities
```

If any valid `BuildingSlot` marker matches a `FacilitySlotDefinition.SlotId`, instantiate `WorldFacilitySlotEntity` from the scene template under the site placement entity root, bind it to the slot, set its global position to the marker anchor cell, draw footprint polygons from marker covered cells, and fill `_siteFacilitySlotLayouts`.

Only use the legacy `FacilitySlots` root when no usable semantic building slot markers exist.

- [ ] **Step 4: Verify behavior regressions**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: source-boundary and consumer tests pass; scene authoring test still fails until Bonefield scene migration.

## Task 5: Bonefield Scene Migration

**Files:**
- Modify `scenes/world/sites/impl/BonefieldSite.tscn`

- [ ] **Step 1: Add business marker scene ext_resources**

Add `PackedScene` ext_resources pointing to `res://scenes/maps/markers/BuildingSlotMapMarker.tscn` and `res://scenes/maps/markers/DeploymentZoneMapMarker.tscn`.

- [ ] **Step 2: Add `SemanticMarkers` root and converted slots**

Add:

```text
[node name="SemanticMarkers" type="Node2D" parent="."]
y_sort_enabled = true

[node name="mine_slot_01" parent="SemanticMarkers" instance=ExtResource("<building_marker_id>")]
MarkerId = "mine_slot_01"
Width = 3
Height = 2

[node name="tower_slot_01" parent="SemanticMarkers" instance=ExtResource("<building_marker_id>")]
MarkerId = "tower_slot_01"
Width = 2
Height = 2

[node name="player_deployment_zone_west" parent="SemanticMarkers" instance=ExtResource("<deployment_marker_id>")]
MarkerId = "player_deployment_zone_west"
DeploymentSide = 1
Width = 4
Height = 8

[node name="undead_deployment_zone_east" parent="SemanticMarkers" instance=ExtResource("<deployment_marker_id>")]
MarkerId = "undead_deployment_zone_east"
DeploymentSide = 2
Width = 8
Height = 4
```

Use the existing legacy slot positions as the marker positions, then remove the old `FacilitySlots/mine_slot_01` and `FacilitySlots/tower_slot_01` instances or leave an empty legacy root only if needed for compatibility.

- [ ] **Step 3: Verify focused regression**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: all WorldSiteDeploymentCacheRegression cases pass.

## Task 6: Documentation Merge And Final Verification

**Files:**
- Modify `system-design/README.md`
- Add `system-design/semantic-map-marker-architecture.md`
- Modify `design-proposals/active/2026-05-18-semantic-map-markers/proposal.md`

- [ ] **Step 1: Merge accepted expected architecture into authority docs**

Copy the expected architecture document to `system-design/semantic-map-marker-architecture.md` and register it in `system-design/README.md`.

- [ ] **Step 2: Mark proposal implementing**

Change proposal status to `Implementing`.

- [ ] **Step 3: Run final verification**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.sln -maxcpucount:2 -v:minimal
git diff --check
dotnet build-server shutdown
```

Expected: all commands exit 0. Existing source generator warnings may remain, but there must be no new errors.
