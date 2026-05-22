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
    string buildingSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "BuildingSlotMapMarker.cs"));
    string deploymentSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "DeploymentZoneMapMarker.cs"));
    string baseScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "SemanticMapMarker.tscn"));
    string buildingScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "BuildingSlotMapMarker.tscn"));
    string deploymentScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "maps", "markers", "DeploymentZoneMapMarker.tscn"));

    AssertTrue(
        baseSource.Contains("abstract partial class SemanticMapMarker", StringComparison.Ordinal),
        "generic semantic marker script should be an abstract authoring base");
    AssertTrue(
        !baseSource.Contains("public SemanticMapMarkerType MarkerType { get; set; }", StringComparison.Ordinal) &&
        !baseSource.Contains("public SemanticDeploymentSide DeploymentSide { get; set; }", StringComparison.Ordinal),
        "generic marker base should not expose business-specific marker type or deployment side fields");
    AssertTrue(
        buildingSource.Contains("protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.BuildingSlot;", StringComparison.Ordinal),
        "building slot marker subclass should own the building slot type");
    AssertTrue(
        deploymentSource.Contains("protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.DeploymentZone;", StringComparison.Ordinal),
        "deployment marker subclass should own the deployment zone type");
    AssertTrue(
        baseScene.Contains("Abstract semantic marker scene", StringComparison.Ordinal) &&
        buildingScene.Contains("instance=ExtResource(\"1_base_marker\")", StringComparison.Ordinal) &&
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

internal static void DemoSiteBuildingSlotsAreAuthoredAsSemanticMarkers()
{
    string scene = ReadDemoSiteScene();
    string markerSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Maps", "BuildingSlotMapMarker.cs"));
    string mineSlotNode = ExtractSceneNodeBlock(scene, "[node name=\"mine_slot_01\" parent=\"SemanticMarkers\"");
    string towerSlotNode = ExtractSceneNodeBlock(scene, "[node name=\"tower_slot_01\" parent=\"SemanticMarkers\"");
    bool markerSourceUsesBuildingSlotDefault =
        markerSource.Contains("ResolvedMarkerType => SemanticMapMarkerType.BuildingSlot;", StringComparison.Ordinal);

    AssertTrue(scene.Contains("[node name=\"SemanticMarkers\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal), "demo site should have SemanticMarkers root");
    AssertTrue(scene.Contains("parent=\"SemanticMarkers\" instance=ExtResource", StringComparison.Ordinal), "demo site should place marker child scene instances under SemanticMarkers");
    AssertTrue(scene.Contains("BuildingSlotMapMarker.tscn", StringComparison.Ordinal), "demo site building slots should instance the building slot marker child scene");
    AssertTrue(
        mineSlotNode.Contains("MarkerType = 0", StringComparison.Ordinal) ||
        (!mineSlotNode.Contains("MarkerType =", StringComparison.Ordinal) && markerSourceUsesBuildingSlotDefault),
        "mine building slot marker enum value should be explicit or come from the SemanticMapMarker default");
    AssertTrue(
        towerSlotNode.Contains("MarkerType = 0", StringComparison.Ordinal) ||
        (!towerSlotNode.Contains("MarkerType =", StringComparison.Ordinal) && markerSourceUsesBuildingSlotDefault),
        "building slot marker enum value should be explicit or come from the SemanticMapMarker default");
    AssertTrue(scene.Contains("MarkerId = \"mine_slot_01\"", StringComparison.Ordinal), "mine slot marker should be authored");
    AssertTrue(scene.Contains("MarkerId = \"tower_slot_01\"", StringComparison.Ordinal), "tower slot marker should be authored");
    AssertTrue(scene.Contains("Width = 3", StringComparison.Ordinal) && scene.Contains("Height = 2", StringComparison.Ordinal), "mine slot footprint should be visible as 3x2");
    AssertTrue(scene.Contains("Width = 2", StringComparison.Ordinal) && scene.Contains("Height = 2", StringComparison.Ordinal), "tower slot footprint should be visible as 2x2");
}

internal static void DemoSiteDeploymentZonesAreAuthoredAsSemanticMarkers()
{
    string scene = ReadDemoSiteScene();
    string playerNode = ExtractSceneNodeBlock(scene, "[node name=\"player_deployment_zone_west\" parent=\"SemanticMarkers\"");
    string enemyNode = ExtractSceneNodeBlock(scene, "[node name=\"undead_deployment_zone_east\" parent=\"SemanticMarkers\"");

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

internal static void WorldSiteRootPrefersSemanticBuildingSlotMarkers()
{
    string source = ReadWorldSiteRootSource();
    AssertTrue(source.Contains("SemanticMapMarkerExtractor", StringComparison.Ordinal), "world site root should extract semantic markers");
    AssertTrue(source.Contains("SemanticMapMarkerType.BuildingSlot", StringComparison.Ordinal), "world site root should filter building slot markers");
    AssertTrue(source.Contains("BuildFacilitySlotEntitiesFromSemanticMarkers", StringComparison.Ordinal), "semantic building slot path should be explicit");
    AssertTrue(source.Contains("RefreshLegacyFacilitySlotEntities", StringComparison.Ordinal), "legacy slot path should remain a named fallback");
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
