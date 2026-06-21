using Rpg.Application.Maps;
using Rpg.Definitions.Maps;
using Rpg.Definitions.StrategicManagement;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void SemanticMapMarkerContractRetiresBuildingSlotRegions()
{
    string root = ProjectRoot();
    string typeSource = File.ReadAllText(Path.Combine(root, "src", "Definitions", "Maps", "SemanticMapMarkerType.cs"));
    string markerDataSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Maps", "SemanticMapMarkerData.cs"));

    AssertTrue(!Enum.GetNames(typeof(SemanticMapMarkerType)).Contains("BuildingSlot"), "semantic marker type enum should not expose retired BuildingSlot");
    AssertTrue(!typeSource.Contains("BuildingSlot", StringComparison.Ordinal), "semantic marker type source should not retain retired BuildingSlot");
    AssertTrue(!markerDataSource.Contains("BuildingSlot", StringComparison.Ordinal), "pure marker data should not preserve building-slot compatibility fields");

    SemanticMapMarkerData marker = new()
    {
        MapId = "bonefield",
        MarkerId = StrategicManagementIds.RegionPlainsEconomy,
        MarkerType = SemanticMapMarkerType.ConstructionRegion,
        AnchorCell = new Godot.Vector2I(18, 12),
        CellHeight = 0,
        Width = 3,
        Height = 2,
        SourcePath = "SemanticMarkers/ConstructionRegions/strategic_region_plains_economy"
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

internal static void SemanticMapMarkerContractExposesConstructionRegions()
{
    bool hasConstructionRegionType = Enum.TryParse("ConstructionRegion", out SemanticMapMarkerType constructionRegionType);
    AssertTrue(hasConstructionRegionType, "semantic marker type enum should expose ConstructionRegion");
    SemanticMapMarkerData marker = new()
    {
        MapId = "player_camp",
        MarkerId = StrategicManagementIds.RegionPlainsEconomy,
        MarkerType = constructionRegionType,
        AnchorCell = new Godot.Vector2I(0, 0),
        Width = 8,
        Height = 6,
        SourcePath = "SemanticMarkers/ConstructionRegions/strategic_region_plains_economy"
    };
    AssertEqual(StrategicManagementIds.RegionPlainsEconomy, marker.MarkerId, "construction marker id should match strategic region id");
    AssertTrue(
        typeof(SemanticMapMarkerData).GetProperty("AllowedCategoryIds") == null,
        "construction marker data should not carry building-category restrictions");
    AssertTrue(marker.CoveredCells.Contains(new Godot.Vector2I(7, 5)), "construction marker should expose its covered region cells");
}

internal static void SemanticMapMarkerContractExposesDeploymentSide()
{
    AssertEqual(SemanticDeploymentSide.Any, Enum.Parse<SemanticDeploymentSide>("Any"), "shared deployment side enum value");
    AssertEqual(SemanticDeploymentSide.Player, Enum.Parse<SemanticDeploymentSide>("Player"), "player deployment side enum value");
    AssertEqual(SemanticDeploymentSide.Enemy, Enum.Parse<SemanticDeploymentSide>("Enemy"), "enemy deployment side enum value");

    SemanticMapMarkerData marker = new()
    {
        MarkerId = "author_named_start_zone",
        MarkerType = SemanticMapMarkerType.DeploymentZone,
        DeploymentSide = SemanticDeploymentSide.Player,
        AnchorCell = new Godot.Vector2I(7, 8)
    };

    string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "DeploymentZoneMapMarker.cs"));
    AssertEqual(SemanticDeploymentSide.Player, marker.DeploymentSide, "runtime marker data should preserve authored deployment side");
    AssertTrue(source.Contains("[Export]", StringComparison.Ordinal) &&
               source.Contains("public SemanticDeploymentSide DeploymentSide { get; set; } = SemanticDeploymentSide.Any;", StringComparison.Ordinal),
        "deployment marker node should expose deployment side as an editor-authored property");
    AssertTrue(
        source.Contains("protected override SemanticDeploymentSide ResolvedDeploymentSide => DeploymentSide;", StringComparison.Ordinal),
        "deployment marker extraction should copy deployment side into pure marker data through the marker base contract");
}

internal static void SemanticMarkerAuthoringUsesBusinessSubclasses()
{
    string baseSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "SemanticMapMarker.cs"));
    string constructionSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "ConstructionRegionMapMarker.cs"));
    string deploymentSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "DeploymentZoneMapMarker.cs"));
    string baseScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "SemanticMapMarker.tscn"));
    string constructionScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "ConstructionRegionMapMarker.tscn"));
    string deploymentScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "DeploymentZoneMapMarker.tscn"));
    string buildingSourcePath = Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "BuildingSlotMapMarker.cs");
    string buildingScenePath = Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "BuildingSlotMapMarker.tscn");

    AssertTrue(
        baseSource.Contains("abstract partial class SemanticMapMarker", StringComparison.Ordinal),
        "generic semantic marker script should be an abstract authoring base");
    AssertTrue(
        !baseSource.Contains("public SemanticMapMarkerType MarkerType { get; set; }", StringComparison.Ordinal) &&
        !baseSource.Contains("public SemanticDeploymentSide DeploymentSide { get; set; }", StringComparison.Ordinal),
        "generic marker base should not expose business-specific marker type or deployment side fields");
    AssertTrue(
        !File.Exists(buildingSourcePath) &&
        !File.Exists(buildingScenePath) &&
        !baseScene.Contains("BuildingSlotMapMarker", StringComparison.Ordinal),
        "retired building-slot marker authoring script and scene should be deleted");
    AssertTrue(
        constructionSource.Contains("protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.ConstructionRegion;", StringComparison.Ordinal),
        "construction region marker subclass should own the construction region type");
    AssertTrue(
        deploymentSource.Contains("protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.DeploymentZone;", StringComparison.Ordinal),
        "deployment marker subclass should own the deployment zone type");
    AssertTrue(
        baseScene.Contains("Abstract semantic marker scene", StringComparison.Ordinal) &&
        constructionScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal) &&
        deploymentScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal),
        "business marker scenes should inherit from the abstract marker scene template");
}

internal static void DeploymentMarkerPreviewColorFollowsSide()
{
    string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "DeploymentZoneMapMarker.cs"));

    AssertTrue(
        source.Contains("PlayerFill", StringComparison.Ordinal) &&
        source.Contains("EnemyFill", StringComparison.Ordinal),
        "deployment marker should define distinct player and enemy preview colors");
    AssertTrue(
        source.Contains("SemanticDeploymentSide.Player => PlayerFill", StringComparison.Ordinal) &&
        source.Contains("SemanticDeploymentSide.Enemy => EnemyFill", StringComparison.Ordinal),
        "deployment marker fill should be light green for player and light red for enemy");
}

internal static void SemanticMapMarkerEditorPreviewDrawsOnlyOuterBorder()
{
    string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "SemanticMapMarker.cs"));
    string drawBody = ExtractMethodBody(source, "public override void _Draw()");

    AssertTrue(source.Contains("DrawRegionFill", StringComparison.Ordinal), "semantic marker preview should fill the footprint without owning gameplay data");
    AssertTrue(source.Contains("DrawRegionOutline", StringComparison.Ordinal), "semantic marker preview should draw a single outer outline for the footprint");
    AssertTrue(source.Contains("BuildRegionOutlineLocal", StringComparison.Ordinal), "semantic marker preview should build one footprint outline from the covered cells");
    AssertTrue(
        !drawBody.Contains("DrawPolyline(ClosePolygon(polygon)", StringComparison.Ordinal),
        "semantic marker preview should not draw per-cell borders because that creates internal grid lines");
}

internal static void WorldSiteRootUsesDemoSiteAsBattleMap()
{
    string worldSiteRootScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "sites", "WorldSiteRoot.tscn"));
    string demoScene = ReadDemoSiteScene();
    string rootNode = ExtractSceneNodeBlock(demoScene, "[node name=\"DemoSite\" type=\"Node2D\"]");

    AssertTrue(
        worldSiteRootScene.Contains("path=\"res://scenes/world/sites/impl/demo_site.tscn\"", StringComparison.Ordinal) &&
        worldSiteRootScene.Contains("SiteMapScene = ExtResource", StringComparison.Ordinal),
        "WorldSiteRoot should load the demo site as the battle map for the V0 combat validation slice.");
    AssertTrue(
        demoScene.Contains("BattleMapView.cs", StringComparison.Ordinal) &&
        rootNode.Contains("script = ExtResource", StringComparison.Ordinal),
        "demo site should be a BattleMapView-backed map so combat can build grid runtime data.");
}

internal static void ReferenceMapsDoNotAuthorBuildingSlots()
{
    string root = ProjectRoot();
    string scene = ReadDemoSiteScene();
    string bonefield = File.ReadAllText(Path.Combine(root, "scenes", "world", "sites", "impl", "BonefieldSite.tscn"));
    string plainsCity = File.ReadAllText(Path.Combine(root, "scenes", "city", "layouts", "plains_city_v0_layout.tscn"));

    AssertTrue(scene.Contains("[node name=\"SemanticMarkers\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal), "demo site should have SemanticMarkers root");
    AssertTrue(scene.Contains("ConstructionRegionMapMarker.tscn", StringComparison.Ordinal), "demo site should keep construction-region markers for Strategic Management placement");

    foreach ((string Name, string Text) candidate in new[]
    {
        ("demo_site", scene),
        ("BonefieldSite", bonefield),
        ("plains_city_v0_layout", plainsCity)
    })
    {
        foreach (string forbidden in new[]
        {
            "BuildingSlots",
            "BuildingSlotMapMarker.tscn",
            "parent=\"SemanticMarkers/BuildingSlots\"",
            "SemanticMapMarkerType.BuildingSlot",
            "MarkerId = \"mine_slot_01\"",
            "MarkerId = \"tower_slot_01\"",
            "[node name=\"FacilitySlots\""
        })
        {
            AssertTrue(!candidate.Text.Contains(forbidden, StringComparison.Ordinal), $"{candidate.Name} should not retain retired building/facility-slot marker fragment={forbidden}");
        }
    }
}

internal static void DemoSiteConstructionRegionsAreAuthoredAsSemanticMarkers()
{
    string root = ProjectRoot();
    string scene = ReadDemoSiteScene();
    string typeSource = File.ReadAllText(Path.Combine(root, "src", "Definitions", "Maps", "SemanticMapMarkerType.cs"));
    string dataSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Maps", "SemanticMapMarkerData.cs"));
    string markerSourcePath = Path.Combine(root, "src", "Presentation", "Maps", "ConstructionRegionMapMarker.cs");
    string markerScenePath = Path.Combine(root, "scenes", "maps", "markers", "ConstructionRegionMapMarker.tscn");

    AssertTrue(typeSource.Contains("ConstructionRegion", StringComparison.Ordinal), "semantic marker type enum should expose construction regions");
    AssertTrue(!dataSource.Contains("AllowedCategoryIds", StringComparison.Ordinal), "pure marker data should not carry construction-region category restrictions");
    AssertTrue(File.Exists(markerSourcePath), "construction region authoring should have a business marker subclass");
    AssertTrue(File.Exists(markerScenePath), "construction region authoring should have a reusable PackedScene");

    string markerSource = File.Exists(markerSourcePath) ? File.ReadAllText(markerSourcePath) : "";
    string markerScene = File.Exists(markerScenePath) ? File.ReadAllText(markerScenePath) : "";
    AssertTrue(
        markerSource.Contains("ConstructionRegionMapMarker : SemanticMapMarker", StringComparison.Ordinal) &&
        markerSource.Contains("ResolvedMarkerType => SemanticMapMarkerType.ConstructionRegion", StringComparison.Ordinal) &&
        !markerSource.Contains("AllowedCategoryIds", StringComparison.Ordinal),
        "construction region marker subclass should own only construction-region type and priority");
    AssertTrue(
        markerScene.Contains("ConstructionRegionMapMarker.cs", StringComparison.Ordinal) &&
        markerScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal),
        "construction region marker scene should inherit from the semantic marker base scene");

    AssertTrue(scene.Contains("ConstructionRegionMapMarker.tscn", StringComparison.Ordinal), "demo site should instance construction-region marker child scenes");
    foreach ((string NodeName, string RegionId, string Position, int Width, int Height) required in new[]
    {
        ("strategic_region_plains_economy", StrategicManagementIds.RegionPlainsEconomy, "position = Vector2(160, 96)", 8, 6),
        ("strategic_region_plains_military", StrategicManagementIds.RegionPlainsMilitary, "position = Vector2(336, 288)", 7, 5),
        ("strategic_region_plains_civic", StrategicManagementIds.RegionPlainsCivic, "position = Vector2(192, 448)", 6, 4)
    })
    {
        string node = ExtractSceneNodeBlock(scene, $"[node name=\"{required.NodeName}\" parent=\"SemanticMarkers/ConstructionRegions\"");
        AssertTrue(node.Contains($"MarkerId = \"{required.RegionId}\"", StringComparison.Ordinal), $"construction region marker should bind strategic region id={required.RegionId}");
        AssertTrue(node.Contains(required.Position, StringComparison.Ordinal), $"construction region marker should align with strategic region bounds for id={required.RegionId}");
        AssertTrue(node.Contains($"Width = {required.Width}", StringComparison.Ordinal), $"construction region marker should expose width={required.Width}");
        AssertTrue(node.Contains($"Height = {required.Height}", StringComparison.Ordinal), $"construction region marker should expose height={required.Height}");
        AssertTrue(!node.Contains("AllowedCategoryIds", StringComparison.Ordinal), $"construction region marker should not carry category restrictions id={required.RegionId}");
    }
}

internal static void DemoSiteDeploymentZonesAreAuthoredAsSemanticMarkers()
{
    string scene = ReadDemoSiteScene();
    string playerNode = ExtractSceneNodeBlock(scene, "[node name=\"player_deployment_zone_west\" parent=\"SemanticMarkers/DeploymentZones\"");
    string enemyNode = ExtractSceneNodeBlock(scene, "[node name=\"undead_deployment_zone_east\" parent=\"SemanticMarkers/DeploymentZones\"");

    AssertTrue(scene.Contains("MarkerId = \"player_deployment_zone_west\"", StringComparison.Ordinal), "player deployment zone marker should be authored");
    AssertTrue(scene.Contains("MarkerId = \"undead_deployment_zone_east\"", StringComparison.Ordinal), "enemy deployment zone marker should be authored");
    AssertTrue(scene.Contains("DeploymentZoneMapMarker.tscn", StringComparison.Ordinal), "deployment zones should instance the deployment marker child scene");
    AssertTrue(
        !playerNode.Contains("MarkerType =", StringComparison.Ordinal) &&
        !enemyNode.Contains("MarkerType =", StringComparison.Ordinal),
        "deployment marker type should come from the deployment marker subclass, not per-instance enum editing");
    AssertTrue(playerNode.Contains("DeploymentSide = 1", StringComparison.Ordinal), "player deployment zone should carry player-side routing");
    AssertTrue(enemyNode.Contains("DeploymentSide = 2", StringComparison.Ordinal), "enemy deployment zone should carry enemy-side routing");
    AssertTrue(scene.Contains("Width = 4", StringComparison.Ordinal) && scene.Contains("Height = 8", StringComparison.Ordinal), "player deployment zone footprint should be visible as 4x8");
    AssertTrue(scene.Contains("Width = 8", StringComparison.Ordinal) && scene.Contains("Height = 4", StringComparison.Ordinal), "enemy deployment zone footprint should be visible as 8x4");
}

internal static void BattlePreparationShowsBothFactionDeploymentZones()
{
    string source = ReadWorldSiteRootSource();
    string scene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "sites", "WorldSiteRoot.tscn"));
    string showBody = ExtractMethodBody(source, "private void ShowBattlePreparationDeploymentZone()");
    string activateBody = ExtractMethodBody(source, "private bool ActivateBattleRuntime()");

    AssertTrue(
        showBody.Contains("ResolveBattlePreparationPlayerDeploymentFactionId", StringComparison.Ordinal),
        "battle preparation should keep showing the player deployment zone");
    AssertTrue(
        showBody.Contains("ResolveBattlePreparationEnemyDeploymentFactionId", StringComparison.Ordinal),
        "battle preparation should also resolve the enemy deployment zone");
    AssertTrue(
        showBody.Contains("_deploymentZoneOverlay?.SetZones(playerCells, enemyCells)", StringComparison.Ordinal),
        "battle preparation deployment zones should be presented by the dedicated deployment-zone overlay");
    AssertTrue(
        !showBody.Contains("BattleGridHighlightKind.FriendlyMove", StringComparison.Ordinal) &&
        !showBody.Contains("BattleGridHighlightKind.EnemyDeployment", StringComparison.Ordinal),
        "deployment zones must not be routed through generic movement or enemy-deployment grid highlights");
    AssertTrue(
        scene.Contains("BattleDeploymentZoneOverlay.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"DeploymentZoneOverlay\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport/OverlayRoot\"]", StringComparison.Ordinal),
        "WorldSiteRoot scene should author a dedicated deployment-zone overlay node beside the generic grid highlight overlay");
    AssertTrue(
        activateBody.Contains("_deploymentZoneOverlay?.ClearZones()", StringComparison.Ordinal),
        "dedicated deployment-zone overlay should be cleared when battle runtime starts");
}

internal static void BattlePreparationUsesDeploymentSideMarkersInsteadOfMarkerNames()
{
    string root = ProjectRoot();
    string siteSources = ReadWorldSiteRootSource();
    string applicationWorldSources = string.Join(
        "\n",
        Directory.GetFiles(Path.Combine(root, "src", "Application", "World"), "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path)
            .Select(File.ReadAllText));

    AssertTrue(
        siteSources.Contains("GetDeploymentZoneCandidatesForSide", StringComparison.Ordinal),
        "battle preparation should query deployment zones by semantic side");
    AssertTrue(
        siteSources.Contains("ResolveBattlePreparationDeploymentSide", StringComparison.Ordinal),
        "battle preparation should translate request player/enemy buckets into deployment-side marker routing");
    AssertTrue(
        applicationWorldSources.Contains("GetDeploymentZoneCandidatesForSide", StringComparison.Ordinal),
        "automatic deployment preparation should use the same deployment-side marker routing as UI drops");
    AssertTrue(
        !applicationWorldSources.Contains("player_deployment_zone_west", StringComparison.Ordinal) &&
        !applicationWorldSources.Contains("undead_deployment_zone_east", StringComparison.Ordinal) &&
        !siteSources.Contains("player_deployment_zone_west", StringComparison.Ordinal) &&
        !siteSources.Contains("undead_deployment_zone_east", StringComparison.Ordinal),
        "runtime deployment code must not hardcode author marker ids or node names");
}

internal static void BattlePreparationObjectiveSelectionUsesCompactMarkerBackedThumbnail()
{
    string root = ProjectRoot();
    string siteSources = ReadWorldSiteRootSource();
    string planningSource = ReadWorldSitePresentationSource();
    string previewSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleObjectiveMapPreview.cs"));
    string thumbnailSourcePath = Path.Combine(root, "src", "Presentation", "World", "Sites", "BattlePreparationObjectiveThumbnail.cs");
    string thumbnailScenePath = Path.Combine(root, "scenes", "world", "ui", "BattlePreparationObjectiveThumbnail.tscn");
    string factorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));

    AssertTrue(
        siteSources.Contains("BindBattlePreparationObjectiveThumbnail", StringComparison.Ordinal) &&
        !siteSources.Contains("AddBattlePreparationObjectiveMapButton", StringComparison.Ordinal) &&
        !siteSources.Contains("BuildDefaultBattlePreparationObjectiveZones", StringComparison.Ordinal),
        "battle preparation should bind a compact tactical thumbnail instead of generating abstract objective buttons or a vertical modal button");
    AssertTrue(
        planningSource.Contains("SemanticMapMarkerType.ObjectiveZone", StringComparison.Ordinal) &&
        planningSource.Contains("SemanticMapMarkerType.DeploymentZone", StringComparison.Ordinal) &&
        planningSource.Contains("SemanticDeploymentSide.Enemy", StringComparison.Ordinal),
        "objective choices should be sourced from semantic objective markers, with enemy deployment markers as the current authored V0 target regions");
    AssertTrue(
        planningSource.Contains("BuildBattleObjectiveMapRegions", StringComparison.Ordinal) &&
        planningSource.Contains("SemanticDeploymentSide.Player", StringComparison.Ordinal) &&
        planningSource.Contains("Selectable = selectable", StringComparison.Ordinal) &&
        !planningSource.Contains(".GroupBy(marker => marker.MarkerId", StringComparison.Ordinal),
        "the tactical thumbnail should show each deployment marker instance, including duplicate-id player/enemy zones, while only target regions are selectable");
    AssertTrue(
        planningSource.Contains("activeGridMap.TopSurfacePositions", StringComparison.Ordinal) &&
        planningSource.Contains("BattleGridTerrainQueries.IsWater", StringComparison.Ordinal) &&
        previewSource.Contains("WaterColor", StringComparison.Ordinal) &&
        previewSource.Contains("LandColor", StringComparison.Ordinal),
        "the objective map preview should draw a simplified land/water thumbnail from TileMapLayer-derived grid data");
    AssertTrue(
        File.Exists(thumbnailSourcePath) &&
        File.Exists(thumbnailScenePath),
        "the compact objective selector should be an authored thumbnail scene with a script binder");
    if (!File.Exists(thumbnailSourcePath) || !File.Exists(thumbnailScenePath))
    {
        return;
    }

    string thumbnailSource = File.ReadAllText(thumbnailSourcePath);
    string thumbnailScene = File.ReadAllText(thumbnailScenePath);
    AssertTrue(
        thumbnailSource.Contains("BattlePreparationObjectiveThumbnail : Control", StringComparison.Ordinal) &&
        thumbnailSource.Contains("BattleObjectiveMapPreview", StringComparison.Ordinal) &&
        thumbnailSource.Contains("ObjectiveZoneSelected", StringComparison.Ordinal) &&
        thumbnailScene.Contains("node name=\"BattlePreparationObjectiveThumbnail\"", StringComparison.Ordinal) &&
        thumbnailScene.Contains("node name=\"MapPreview\"", StringComparison.Ordinal),
        "the compact thumbnail should wrap the marker-backed preview and emit objective selections");
    AssertTrue(
        factorySource.Contains("BattlePreparationObjectiveThumbnailScenePath", StringComparison.Ordinal) &&
        factorySource.Contains("CreateBattlePreparationObjectiveThumbnail", StringComparison.Ordinal) &&
        siteSources.Contains("MinimapHost", StringComparison.Ordinal),
        "the objective selector should be loaded through GameUiSceneFactory and hosted in the compact minimap/overlay area");
}

internal static void ObjectiveZoneMarkerAuthoringIsAvailable()
{
    string root = ProjectRoot();
    string typeSource = File.ReadAllText(Path.Combine(root, "src", "Definitions", "Maps", "SemanticMapMarkerType.cs"));
    string dataSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Maps", "SemanticMapMarkerData.cs"));
    string baseSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Maps", "SemanticMapMarker.cs"));
    string objectiveSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Maps", "ObjectiveZoneMapMarker.cs"));
    string objectiveScene = File.ReadAllText(Path.Combine(root, "scenes", "maps", "markers", "ObjectiveZoneMapMarker.tscn"));

    AssertTrue(typeSource.Contains("ObjectiveZone", StringComparison.Ordinal), "semantic marker type enum should expose objective zones");
    AssertTrue(dataSource.Contains("ObjectiveRole", StringComparison.Ordinal), "pure marker data should carry objective role");
    AssertTrue(
        baseSource.Contains("ResolvedObjectiveRole", StringComparison.Ordinal) &&
        objectiveSource.Contains("ResolvedMarkerType => SemanticMapMarkerType.ObjectiveZone", StringComparison.Ordinal),
        "objective marker authoring should use a business subclass instead of editing marker type per instance");
    AssertTrue(
        objectiveScene.Contains("ObjectiveZoneMapMarker.cs", StringComparison.Ordinal) &&
        objectiveScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal),
        "objective zone marker scene should inherit from the semantic marker base scene");
}

internal static void SiteMapLayoutAuthorityIsAccepted()
{
    string root = ProjectRoot();
    string systemIndex = File.ReadAllText(Path.Combine(root, "system-design", "README.md"));
    string siteLayout = File.ReadAllText(Path.Combine(root, "system-design", "site-map-layout-architecture.md"));
    string semantic = File.ReadAllText(Path.Combine(root, "system-design", "semantic-map-marker-architecture.md"));
    string strategic = File.ReadAllText(Path.Combine(root, "system-design", "strategic-management-system-architecture.md"));
    string activeIndex = File.ReadAllText(Path.Combine(root, "design-proposals", "active", "README.md"));
    string archiveIndex = File.ReadAllText(Path.Combine(root, "design-proposals", "archived", "README.md"));

    AssertTrue(
        systemIndex.Contains("site-map-layout-architecture.md", StringComparison.Ordinal) &&
        siteLayout.Contains("Status: Accepted Architecture", StringComparison.Ordinal) &&
        siteLayout.Contains("scenes/city/", StringComparison.Ordinal),
        "accepted authority should route future city-map work through site-map-layout architecture");
    AssertTrue(
        semantic.Contains("BridgeMapMarker.tscn", StringComparison.Ordinal) &&
        semantic.Contains("BridgeKind", StringComparison.Ordinal) &&
        semantic.Contains("ConnectionIds", StringComparison.Ordinal),
        "semantic marker authority should include bridge marker authoring fields");
    AssertTrue(
        strategic.Contains("site map layout ids", StringComparison.Ordinal) &&
        strategic.Contains("persistent location facts", StringComparison.Ordinal),
        "strategic management authority should bind locations to reusable layouts without making scenes persistent state");
    AssertTrue(
        !activeIndex.Contains("2026-06-17-site-map-layout-authoring", StringComparison.Ordinal) &&
        archiveIndex.Contains("2026-06-17-site-map-layout-authoring", StringComparison.Ordinal),
        "accepted SMLA-001 proposal should be archived, not active");
}

internal static void SemanticBridgeMarkerAuthoringIsAvailable()
{
    string root = ProjectRoot();
    string typeSource = File.ReadAllText(Path.Combine(root, "src", "Definitions", "Maps", "SemanticMapMarkerType.cs"));
    string bridgeKindSourcePath = Path.Combine(root, "src", "Definitions", "Maps", "SemanticBridgeKind.cs");
    string dataSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Maps", "SemanticMapMarkerData.cs"));
    string baseSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Maps", "SemanticMapMarker.cs"));
    string bridgeSourcePath = Path.Combine(root, "src", "Presentation", "Maps", "BridgeMapMarker.cs");
    string bridgeScenePath = Path.Combine(root, "scenes", "maps", "markers", "BridgeMapMarker.tscn");

    AssertTrue(Enum.GetNames(typeof(SemanticMapMarkerType)).Contains("Bridge"), "semantic marker type enum should expose bridge markers");
    AssertTrue(typeSource.Contains("Bridge", StringComparison.Ordinal), "semantic marker type source should name bridge markers for static readers");
    AssertTrue(File.Exists(bridgeKindSourcePath), "bridge kind enum should be explicit, not inferred from visual tile art");
    AssertTrue(File.Exists(bridgeSourcePath), "bridge marker authoring should have a business marker subclass");
    AssertTrue(File.Exists(bridgeScenePath), "bridge marker authoring should have a reusable PackedScene");

    string bridgeKindSource = File.ReadAllText(bridgeKindSourcePath);
    string bridgeSource = File.ReadAllText(bridgeSourcePath);
    string bridgeScene = File.ReadAllText(bridgeScenePath);

    AssertTrue(
        bridgeKindSource.Contains("RiverBridge", StringComparison.Ordinal) &&
        bridgeKindSource.Contains("HeightBridge", StringComparison.Ordinal),
        "bridge kind enum should distinguish same-height river bridges from height-changing bridges");
    AssertTrue(
        dataSource.Contains("SemanticBridgeKind BridgeKind", StringComparison.Ordinal) &&
        dataSource.Contains("ConnectionIds", StringComparison.Ordinal),
        "pure marker data should carry bridge kind and explicit connection ids");
    AssertTrue(
        baseSource.Contains("ResolvedBridgeKind", StringComparison.Ordinal) &&
        baseSource.Contains("ResolvedConnectionIds", StringComparison.Ordinal),
        "semantic marker base should copy bridge fields into pure marker data");
    AssertTrue(
        bridgeSource.Contains("BridgeMapMarker : SemanticMapMarker", StringComparison.Ordinal) &&
        bridgeSource.Contains("protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.Bridge", StringComparison.Ordinal) &&
        bridgeSource.Contains("public SemanticBridgeKind BridgeKind { get; set; } = SemanticBridgeKind.RiverBridge;", StringComparison.Ordinal) &&
        bridgeSource.Contains("public string[] ConnectionIds { get; set; }", StringComparison.Ordinal),
        "bridge marker subclass should own bridge type, kind, and explicit connection references");
    AssertTrue(
        bridgeScene.Contains("BridgeMapMarker.cs", StringComparison.Ordinal) &&
        bridgeScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal),
        "bridge marker scene should inherit from the semantic marker base scene");
}

internal static void PlainsCityBaseIsTerrainOnlyBattleMap()
{
    string root = ProjectRoot();
    string baseScenePath = Path.Combine(root, "scenes", "city", "base", "plains_city_base.tscn");
    AssertTrue(File.Exists(baseScenePath), "first plains city base terrain scene should exist");

    string scene = File.ReadAllText(baseScenePath);
    AssertTrue(
        scene.Contains("[node name=\"PlainsCityBase\" type=\"Node2D\"]", StringComparison.Ordinal) &&
        scene.Contains("BattleMapView.cs", StringComparison.Ordinal) &&
        scene.Contains("script = ExtResource", StringComparison.Ordinal),
        "plains city base should be a BattleMapView-backed map");
    foreach (string requiredLayer in new[]
    {
        "WaterFoundationLayer",
        "LowFoundationLayer",
        "HighFoundationLayer",
        "StairLayer",
        "OverlayLayer"
    })
    {
        AssertTrue(scene.Contains($"[node name=\"{requiredLayer}\" type=\"TileMapLayer\" parent=\".\"]", StringComparison.Ordinal), $"base terrain should author layer={requiredLayer}");
    }

    foreach (string forbidden in new[]
    {
        "SemanticMarkers",
        "BridgeMapMarker.tscn",
        "BuildingSlotMapMarker.tscn",
        "DeploymentZoneMapMarker.tscn",
        "ObjectiveZoneMapMarker.tscn",
        "MarkerId =",
        "BattleMapConnectionConfig"
    })
    {
        AssertTrue(!scene.Contains(forbidden, StringComparison.Ordinal), $"base terrain should not own layout content fragment={forbidden}");
    }
}

internal static void PlainsCityLayoutInheritsBaseAndOwnsContentMarkers()
{
    string root = ProjectRoot();
    string layoutScenePath = Path.Combine(root, "scenes", "city", "layouts", "plains_city_v0_layout.tscn");
    AssertTrue(File.Exists(layoutScenePath), "first plains city layout variant scene should exist");

    string scene = File.ReadAllText(layoutScenePath);
    AssertTrue(
        scene.Contains("path=\"res://scenes/city/base/plains_city_base.tscn\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"PlainsCityLayout\" instance=ExtResource(\"1_base\")]", StringComparison.Ordinal),
        "plains city layout should inherit the plains city base scene");
    AssertTrue(
        scene.Contains("metadata/layout_id = \"plains_city_v0\"", StringComparison.Ordinal) &&
        scene.Contains("metadata/base_terrain_id = \"plains_city_base\"", StringComparison.Ordinal),
        "layout scene should expose stable layout metadata for future strategic-location binding");
    AssertTrue(
        scene.Contains("BridgeVisualLayer", StringComparison.Ordinal) &&
        scene.Contains("DecorationLayer", StringComparison.Ordinal) &&
        scene.Contains("ObstacleLayer", StringComparison.Ordinal) &&
        scene.Contains("BattleMapConnectionConfig", StringComparison.Ordinal),
        "layout variant should own bridge visuals, decoration, obstacles, and explicit height connections");
    AssertTrue(
        scene.Contains("[node name=\"SemanticMarkers\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal) &&
        scene.Contains("BridgeMapMarker.tscn", StringComparison.Ordinal) &&
        scene.Contains("DeploymentZoneMapMarker.tscn", StringComparison.Ordinal) &&
        scene.Contains("ObjectiveZoneMapMarker.tscn", StringComparison.Ordinal),
        "layout variant should author tactical content markers through business marker scenes");
    AssertTrue(
        !scene.Contains("BuildingSlotMapMarker.tscn", StringComparison.Ordinal) &&
        !scene.Contains("SemanticMarkers/BuildingSlots", StringComparison.Ordinal),
        "layout variant should not retain retired building-slot marker authoring");

    foreach (string stableId in new[]
    {
        "plains_city_v0_river_bridge",
        "plains_city_v0_high_ramp",
        "plains_city_v0_player_deployment_west",
        "plains_city_v0_enemy_deployment_east",
        "plains_city_v0_resource_cache"
    })
    {
        AssertTrue(scene.Contains(stableId, StringComparison.Ordinal), $"layout should include stable id={stableId}");
    }

    string bridgeNode = ExtractSceneNodeBlock(scene, "[node name=\"river_bridge_west\" parent=\"SemanticMarkers/BridgeMarkers\"");
    AssertTrue(
        bridgeNode.Contains("MarkerId = \"plains_city_v0_river_bridge\"", StringComparison.Ordinal) &&
        bridgeNode.Contains("BridgeKind = 0", StringComparison.Ordinal) &&
        bridgeNode.Contains("CellHeight = 0", StringComparison.Ordinal),
        "river bridge marker should be same-height h=0 ordinary walkable ground");
    string playerDeployment = ExtractSceneNodeBlock(scene, "[node name=\"player_deployment_west\" parent=\"SemanticMarkers/DeploymentZones\"");
    string enemyDeployment = ExtractSceneNodeBlock(scene, "[node name=\"enemy_deployment_east\" parent=\"SemanticMarkers/DeploymentZones\"");
    AssertTrue(
        playerDeployment.Contains("DeploymentSide = 1", StringComparison.Ordinal) &&
        enemyDeployment.Contains("DeploymentSide = 2", StringComparison.Ordinal),
        "layout deployment markers should route player and enemy sides explicitly");
}

internal static void ReferenceSiteMapsStayOutOfCityLayoutSlice()
{
    string root = ProjectRoot();
    string demoSite = ReadDemoSiteScene();
    string bonefieldSite = File.ReadAllText(Path.Combine(root, "scenes", "world", "sites", "impl", "BonefieldSite.tscn"));

    AssertTrue(
        !demoSite.Contains("scenes/city", StringComparison.Ordinal) &&
        !bonefieldSite.Contains("scenes/city", StringComparison.Ordinal),
        "existing reference maps should not point at the new city layout module");
    AssertTrue(
        !demoSite.Contains("BridgeMapMarker.tscn", StringComparison.Ordinal) &&
        !bonefieldSite.Contains("BridgeMapMarker.tscn", StringComparison.Ordinal),
        "first city layout slice should not retrofit bridge markers into existing reference maps");
}

internal static void WorldSiteRootDoesNotUseSemanticBuildingSlotOrFacilitySlots()
{
    string source = ReadWorldSiteRootSource();
    AssertTrue(source.Contains("SemanticMapMarkerExtractor", StringComparison.Ordinal), "world site root should extract semantic markers");
    foreach (string forbidden in new[]
    {
        "SemanticMapMarkerType.BuildingSlot",
        "BuildFacilitySlotEntitiesFromSemanticMarkers",
        "RefreshLegacyFacilitySlotEntities",
        "FacilitySlotsRootName",
        "_siteFacilitySlotEntities",
        "_siteFacilitySlotLayouts",
        "_selectedFacilitySlotId",
        "TryHandleFacilitySlotInput",
        "WorldFacilitySlot",
        "WorldActionResolver"
    })
    {
        AssertTrue(!source.Contains(forbidden, StringComparison.Ordinal), $"world site root should not retain retired facility-slot path fragment={forbidden}");
    }
}

internal static void WorldSiteRootBuildsDeploymentCacheFromSemanticMarkers()
{
    string source = ReadWorldSiteRootSource();
    string loadSiteMapBody = ExtractMethodBody(source, "public void LoadSiteMap(PackedScene mapScene)");

    AssertTrue(source.Contains("ResolveSemanticDeploymentZoneMarkers", StringComparison.Ordinal), "world site root should expose deployment-zone marker filtering");
    AssertTrue(source.Contains("_deploymentCacheBuilder.Build(siteId, _activeGridMap, deploymentZoneMarkers)", StringComparison.Ordinal), "deployment cache should consume semantic deployment zone markers");
    AssertTrue(
        loadSiteMapBody.IndexOf("ExtractSemanticMapMarkers", StringComparison.Ordinal) <
        loadSiteMapBody.IndexOf("RebuildSiteDeploymentRuntimeCache", StringComparison.Ordinal),
        "semantic markers must be extracted before rebuilding the deployment cache");
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

private static string ExtractSceneNodeBlock(string scene, string nodeHeader)
{
    int start = scene.IndexOf(nodeHeader, StringComparison.Ordinal);
    AssertTrue(start >= 0, $"scene node missing header={nodeHeader}");

    int next = scene.IndexOf("\n[node ", start + nodeHeader.Length, StringComparison.Ordinal);
    return next < 0 ? scene[start..] : scene[start..next];
}

private static string ReadDemoSiteScene()
{
    return File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "sites", "impl", "demo_site.tscn"));
}
}
