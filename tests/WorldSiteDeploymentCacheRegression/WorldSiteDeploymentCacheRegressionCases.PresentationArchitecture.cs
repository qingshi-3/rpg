internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void DeploymentMoveHeightWriteBelongsToDeploymentService()
{
    string root = ProjectRoot();
    string evaluatorSource = File.ReadAllText(Path.Combine(root, "src", "Application", "World", "WorldSiteDeploymentTargetEvaluator.cs"));
    string serviceSource = File.ReadAllText(Path.Combine(root, "src", "Application", "World", "WorldSiteDeploymentService.cs"));

    AssertTrue(
        !evaluatorSource.Contains("placement.CellHeight =", StringComparison.Ordinal),
        "deployment target evaluator should not split authoritative placement movement by writing CellHeight after the service move");
    AssertTrue(
        serviceSource.Contains("TryMovePlacementToSurface", StringComparison.Ordinal) &&
        serviceSource.Contains("placement.CellHeight = surface.Height", StringComparison.Ordinal),
        "WorldSiteDeploymentService should own the X/Y/height write for surface-backed placement moves");
}

internal static void WorldViewportLayoutUsesResolvedControlRects()
{
    string root = ProjectRoot();
    string siteRootSource = ReadWorldSiteRootSource();
    string strategicSource = ReadStrategicWorldRootSource();
    string strategicResolveBody = ExtractMethodBody(strategicSource, "private Rect2 ResolveMainWorldViewportRect()");
    string strategicLayoutBody = ExtractMethodBody(strategicSource, "private void UpdateMainWorldViewportLayout(Rect2 mapBounds)");
    string strategicScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "StrategicWorldRoot.tscn"));
    string worldSiteScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "sites", "WorldSiteRoot.tscn"));
    string worldSiteViewportHostBlock = ExtractSceneNodeBlock(worldSiteScene, "[node name=\"MainWorldViewportHost\"");

    AssertTrue(
        !siteRootSource.Contains("MainWorldViewportLeftWhenUiVisible", StringComparison.Ordinal) &&
        !siteRootSource.Contains("MainWorldViewportTopWhenUiVisible", StringComparison.Ordinal),
        "world-site viewport layout should not duplicate HUD dimensions as root constants");
    AssertTrue(
        siteRootSource.Contains("ResolveMainWorldViewportRect", StringComparison.Ordinal) &&
        siteRootSource.Contains("_sitePeacetimePanel.GetGlobalRect()", StringComparison.Ordinal) &&
        siteRootSource.Contains("rootViewportSize.X - position.X", StringComparison.Ordinal) &&
        !siteRootSource.Contains("_siteHudTopBar", StringComparison.Ordinal) &&
        !siteRootSource.Contains("gapAfterPanel", StringComparison.Ordinal) &&
        !siteRootSource.Contains("sideMargin", StringComparison.Ordinal) &&
        !siteRootSource.Contains("bottomMargin", StringComparison.Ordinal),
        "world-site viewport layout should derive a no-gutter right-side map rect from the left management workspace without a separate top header strip");
    AssertTrue(
        siteRootSource.Contains("ResolveWorldSiteHudViewportRect", StringComparison.Ordinal) &&
        !siteRootSource.Contains("ResolveBattleRuntimeViewportRect", StringComparison.Ordinal),
        "world-site battle viewport layout should reuse the same HUD workspace calculation instead of a separate full-width battle branch");
    AssertTrue(
        siteRootSource.Contains("SetFixedRect(_mainWorldViewportHost", StringComparison.Ordinal),
        "world-site viewport layout should pin the SubViewportContainer with the same fixed-rect Control contract as strategic world");
    AssertTrue(
        worldSiteViewportHostBlock.Contains("offset_left = 520.0", StringComparison.Ordinal) &&
        worldSiteViewportHostBlock.Contains("offset_top = 0.0", StringComparison.Ordinal) &&
        worldSiteViewportHostBlock.Contains("offset_right = 1920.0", StringComparison.Ordinal) &&
        worldSiteViewportHostBlock.Contains("offset_bottom = 1080.0", StringComparison.Ordinal),
        "world-site authored viewport should default to the right-side split-screen rectangle instead of the old centered gray-gutter preview");
    AssertTrue(
        !strategicSource.Contains("float mapLeft = DetailWidth + OuterMargin * 2.0f", StringComparison.Ordinal),
        "strategic viewport bounds should not duplicate left-panel dimensions in geometry code");
    AssertTrue(
        strategicSource.Contains("ResolveMainWorldViewportRect", StringComparison.Ordinal) &&
        strategicResolveBody.Contains("_mainWorldViewportHost.GetGlobalRect()", StringComparison.Ordinal) &&
        !strategicResolveBody.Contains("TryResolveFirstControlChildRect", StringComparison.Ordinal) &&
        !strategicResolveBody.Contains("_leftPrimaryPanelHost", StringComparison.Ordinal) &&
        !strategicResolveBody.Contains("_topBarHost", StringComparison.Ordinal) &&
        !strategicLayoutBody.Contains("SetFixedRect(_mainWorldViewportHost", StringComparison.Ordinal) &&
        !strategicLayoutBody.Contains("_worldMapOverlay.Size =", StringComparison.Ordinal) &&
        strategicLayoutBody.Contains("SetFullRect(_worldMapOverlay)", StringComparison.Ordinal) &&
        strategicScene.Contains("node name=\"MainWorldViewportHost\" type=\"SubViewportContainer\" parent=\".\"", StringComparison.Ordinal) &&
        strategicScene.Contains("anchor_right = 1.0", StringComparison.Ordinal) &&
        strategicScene.Contains("anchor_bottom = 1.0", StringComparison.Ordinal),
        "strategic viewport bounds should derive from the authored MainWorldViewportHost rect, not mutable HUD child geometry or code-overwritten host anchors");
}

internal static void BattleCameraUsesMapBoundsSourceContract()
{
    string root = ProjectRoot();
    string battleCameraSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "BattleCameraController.cs"));
    string interfaceSourcePath = Path.Combine(root, "src", "Presentation", "Battle", "IBattleMapBoundsSource.cs");

    AssertTrue(
        File.Exists(interfaceSourcePath),
        "battle camera should depend on a map-bounds provider contract instead of a concrete world-site root");
    AssertTrue(
        !battleCameraSource.Contains("Rpg.Presentation.World.Sites", StringComparison.Ordinal) &&
        !battleCameraSource.Contains("WorldSiteRoot", StringComparison.Ordinal),
        "BattleCameraController should not import or reference the concrete WorldSiteRoot type");
    AssertTrue(
        battleCameraSource.Contains("IBattleMapBoundsSource", StringComparison.Ordinal) &&
        battleCameraSource.Contains("BattleMapLoaded", StringComparison.Ordinal) &&
        battleCameraSource.Contains("ActiveBattleMap", StringComparison.Ordinal),
        "BattleCameraController should listen through the battle map-bounds source contract");
}

internal static void MapCameraClearBoundsKeepsFallbackExplicit()
{
    string root = ProjectRoot();
    string cameraSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "MapCameraController.cs"));

    AssertTrue(
        cameraSource.Contains("ClearRuntimeMapBounds", StringComparison.Ordinal) &&
        cameraSource.Contains("ClearMapBoundsAndApplyConfiguredFallback", StringComparison.Ordinal),
        "MapCameraController should expose separate APIs for true clearing and clear-with-configured-fallback");
    AssertTrue(
        !cameraSource.Contains("ApplyConfiguredMapBoundsFallback(\"clear\")", StringComparison.Ordinal),
        "ClearMapBounds should not silently leave HasMapBounds true through a configured fallback");
}
}
