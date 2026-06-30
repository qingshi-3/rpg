internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void StrategicSaveIntelAndRaidRuntimeAreDeleted()
{
    string root = ProjectRoot();
    string[] deletedPaths =
    {
        Path.Combine(root, "src", "Application", "World", "StrategicWorldSaveService.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldSiteIntelService.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldSiteIntelViewModel.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldThreatService.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldBattleProgressionService.cs"),
        Path.Combine(root, "src", "Definitions", "World", "WorldSiteIntelDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "World", "WorldSiteIntelPolicy.cs"),
        Path.Combine(root, "src", "Definitions", "World", "WorldSiteObscurationDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "World", "ThreatRuleDefinition.cs"),
        Path.Combine(root, "src", "Domain", "World", "StrategicWorldIntelState.cs"),
        Path.Combine(root, "src", "Domain", "World", "EnemyThreatPlan.cs"),
        Path.Combine(root, "src", "Domain", "World", "ThreatStage.cs"),
        Path.Combine(root, "src", "Domain", "World", "ThreatType.cs"),
        Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.FogIntel.cs"),
        Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.Persistence.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldBattleState.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldBattlePhase.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldBattleOutcome.cs"),
        Path.Combine(root, "scenes", "world", "ui", "BattleAlertDialog.tscn"),
        Path.Combine(root, "tests", "WorldSiteIntelRegression", "Program.cs")
    };

    foreach (string path in deletedPaths)
    {
        AssertTrue(!File.Exists(path), $"strategic save, intel, or raid runtime file should be deleted path={path}");
    }

    string applicationWorld = CombinedSourceForDeletionGuard(root, "src", "Application", "World");
    string domainWorld = CombinedSourceForDeletionGuard(root, "src", "Domain", "World");
    string definitionWorld = CombinedSourceForDeletionGuard(root, "src", "Definitions", "World");
    string presentationWorld = CombinedSourceForDeletionGuard(root, "src", "Presentation", "World");

    string combinedWorldSource = string.Join("\n", applicationWorld, domainWorld, definitionWorld, presentationWorld);
    string[] forbiddenFragments =
    {
        "StrategicWorldSaveService",
        "WorldSiteIntel",
        "StrategicWorldIntelState",
        "WorldIntelVisibility",
        "Obscuration",
        "RevealedEntrance",
        "BuildDefenseRaidRequest",
        "TryEnterDefenseRaidBattle",
        "WorldThreatService",
        "WorldBattleProgressionService",
        "EnemyThreatPlan",
        "ThreatPlans",
        "PendingThreatIds",
        "ThreatRuleDefinition",
        "WorldBattleState",
        "WorldBattlePhase",
        "WorldBattleOutcome",
        "WorldSiteMode.Alert"
    };

    foreach (string fragment in forbiddenFragments)
    {
        AssertTrue(!combinedWorldSource.Contains(fragment, StringComparison.Ordinal), $"world source should not keep removed fragment={fragment}");
    }

    string strategicHudScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"));
    string siteHudScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    AssertTrue(!strategicHudScene.Contains("SaveButton", StringComparison.Ordinal), "strategic HUD should not expose save");
    AssertTrue(!strategicHudScene.Contains("LoadButton", StringComparison.Ordinal), "strategic HUD should not expose load");
    AssertTrue(!strategicHudScene.Contains("ThreatList", StringComparison.Ordinal), "strategic HUD should not expose raid threat tracking");
    AssertTrue(!siteHudScene.Contains("SiteThreatList", StringComparison.Ordinal), "site HUD should not expose raid threat tracking");
}

private static string CombinedSourceForDeletionGuard(string root, params string[] pathParts)
{
    string path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
    if (!Directory.Exists(path))
    {
        return "";
    }

    return string.Join("\n", Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .OrderBy(item => item, StringComparer.Ordinal)
        .Select(File.ReadAllText));
}

internal static void SiteExplorationRuntimeIsDeleted()
{
    string root = ProjectRoot();
    string[] deletedPaths =
    {
        Path.Combine(root, "src", "Application", "World", "WorldSiteExplorationService.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldSiteExplorationService.cs.uid"),
        Path.Combine(root, "src", "Domain", "World", "WorldSiteExplorationState.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldSiteExplorationState.cs.uid"),
        Path.Combine(root, "src", "Domain", "World", "SiteExplorationPatrolState.cs"),
        Path.Combine(root, "src", "Domain", "World", "SiteExplorationPatrolState.cs.uid"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationPointDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationPointDefinition.cs.uid"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationPatrolDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationPatrolDefinition.cs.uid"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationActionDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "World", "SiteExplorationActionDefinition.cs.uid"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationBattle.cs"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationBattle.cs.uid"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationFlow.cs"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationFlow.cs.uid"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationPresentation.cs"),
        Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteExplorationPresentation.cs.uid"),
        Path.Combine(root, "scenes", "world", "sites", "SiteExplorationHud.tscn"),
        Path.Combine(root, "tests", "BattleHitFeedbackRegression", "BattleHitFeedbackRegressionCases.Exploration.cs"),
        Path.Combine(root, "tests", "BattleHitFeedbackRegression", "BattleHitFeedbackRegressionCases.Exploration.cs.uid")
    };

    foreach (string path in deletedPaths)
    {
        AssertTrue(!File.Exists(path), $"site exploration runtime file should be deleted path={path}");
    }

    string[] sourceFiles =
    {
        Path.Combine(root, "src", "Definitions", "World", "WorldSiteDefinition.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldSiteState.cs"),
        Path.Combine(root, "src", "Domain", "World", "WorldSiteRuntimeMode.cs"),
        Path.Combine(root, "src", "Application", "Battle", "BattleStartRequest.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldSiteBattleLauncher.cs"),
        Path.Combine(root, "src", "Application", "World", "WorldBattleResultApplier.cs"),
        Path.Combine(root, "src", "Application", "World", "StrategicWorldV1DefinitionFactory.cs")
    };
    string[] forbiddenSourceFragments =
    {
        "ExplorationPoints",
        "ExplorationPatrols",
        "WorldSiteExplorationState",
        "SiteExploration",
        "ExplorationTriggerPatrolId",
        "ExplorationPointId",
        "ExplorationEntryCell",
        "ExplorationAlertLevel",
        "ExplorationAdvantageTags"
    };

    foreach (string file in sourceFiles)
    {
        string text = File.ReadAllText(file);
        foreach (string fragment in forbiddenSourceFragments)
        {
            AssertTrue(!text.Contains(fragment, StringComparison.Ordinal), $"source should not keep site exploration fragment={fragment} file={file}");
        }
    }

    string worldSiteRoot = ReadWorldSiteRootSource();
    AssertTrue(!worldSiteRoot.Contains("WorldSiteRuntimeMode.Exploration", StringComparison.Ordinal), "WorldSiteRoot should not expose a site exploration runtime mode");
    AssertTrue(!worldSiteRoot.Contains("SiteExplorationHud", StringComparison.Ordinal), "WorldSiteRoot should not bind a site exploration HUD");
    AssertTrue(!worldSiteRoot.Contains("RequestSiteExploration", StringComparison.Ordinal), "WorldSiteRoot should not keep exploration battle/action entry points");

    string gameplay = File.ReadAllText(Path.Combine(root, "gameplay-design", "content-systems-long-term-design.md"));
    string presentation = File.ReadAllText(Path.Combine(root, "system-design", "presentation-ui-layout-architecture.md"));
    string semanticMarkers = File.ReadAllText(Path.Combine(root, "system-design", "semantic-map-marker-architecture.md"));
    string battleArchitecture = File.ReadAllText(Path.Combine(root, "system-design", "hero-led-light-rts-system-architecture.md"));

    AssertTrue(!gameplay.Contains("exploration site", StringComparison.OrdinalIgnoreCase), "gameplay authority should not describe strategic locations as exploration sites");
    AssertTrue(!gameplay.Contains("exploration progress", StringComparison.OrdinalIgnoreCase), "gameplay authority should not keep dungeon exploration progress");
    AssertTrue(!presentation.Contains("SiteExploration", StringComparison.Ordinal), "presentation authority should not keep SiteExploration mode");
    AssertTrue(!semanticMarkers.Contains("ExplorationPoint", StringComparison.Ordinal), "semantic marker authority should not keep exploration point marker type");
    AssertTrue(!battleArchitecture.Contains("exploration patrol AI", StringComparison.OrdinalIgnoreCase), "battle AI architecture should not keep an exploration patrol phase");
}

internal static void StrategicFogRemainsMapVisibilityOnly()
{
    string root = ProjectRoot();
    string fogServicePath = Path.Combine(root, "src", "Application", "World", "StrategicFogOfWarService.cs");
    string fogRootPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.Fog.cs");

    AssertTrue(File.Exists(fogServicePath), "strategic map fog service should remain");
    AssertTrue(File.Exists(fogRootPath), "strategic root should keep fog presentation partial");

    string fogServiceSource = File.ReadAllText(fogServicePath);
    string refreshFogBody = ExtractMethodBody(fogServiceSource, "public static StrategicFogRefreshResult RefreshVisibility(");
    string getVisibilityBody = ExtractMethodBody(fogServiceSource, "public static StrategicFogVisibility GetPositionVisibility(");
    AssertTrue(refreshFogBody.Contains("VisibleCells", StringComparison.Ordinal), "strategic fog should still calculate the current visible cells");
    AssertTrue(refreshFogBody.Contains("ExploredCells.Clear()", StringComparison.Ordinal), "strategic fog should clear revealed-history state in binary mode");
    AssertTrue(refreshFogBody.Contains("Array.Empty<string>()", StringComparison.Ordinal), "strategic fog refresh should stop reporting newly explored cells");
    AssertTrue(getVisibilityBody.Contains("StrategicFogVisibility.Visible", StringComparison.Ordinal), "strategic fog visibility should still report current visible cells");
    AssertTrue(getVisibilityBody.Contains("StrategicFogVisibility.Unknown", StringComparison.Ordinal), "strategic fog visibility should only distinguish visible and unknown");
    AssertTrue(
        !getVisibilityBody.Contains("StrategicFogVisibility.Revealed", StringComparison.Ordinal),
        "strategic fog visibility should no longer expose revealed residual state");
    AssertTrue(
        !getVisibilityBody.Contains("ExploredCells.Contains", StringComparison.Ordinal),
        "strategic fog visibility should not consult explored-history cells in binary mode");
}

internal static void StrategicFogUsesBinaryVisibilityOnly()
{
    string root = ProjectRoot();
    string fogRootPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.Fog.cs");
    string fogServicePath = Path.Combine(root, "src", "Application", "World", "StrategicFogOfWarService.cs");

    string rootSource = File.ReadAllText(fogRootPath);
    string serviceSource = File.ReadAllText(fogServicePath);
    string refreshFogBody = ExtractMethodBody(rootSource, "private bool RefreshStrategicFog()");
    string refreshOverlayBody = ExtractMethodBody(rootSource, "private void RefreshStrategicFogOverlay()");

    AssertTrue(
        !rootSource.Contains("_pendingStrategicFogExploredCells", StringComparison.Ordinal),
        "strategic fog refresh should not keep a pending explored-cell queue");
    AssertTrue(
        !rootSource.Contains("QueueStrategicFogExploredCells", StringComparison.Ordinal),
        "strategic fog refresh should not enqueue explored cells for later flushing");
    AssertTrue(
        !rootSource.Contains("FlushPendingStrategicFogExploredMask", StringComparison.Ordinal),
        "strategic fog refresh should not batch explored-history commits");
    AssertTrue(
        !rootSource.Contains("RefreshStrategicFogExploredMaskIncremental", StringComparison.Ordinal),
        "strategic fog refresh should not expose an incremental explored-mask path");
    AssertTrue(
        refreshFogBody.Contains("RefreshStrategicFogVisibleCircles", StringComparison.Ordinal),
        "strategic fog refresh should keep current visible circles updated");
    AssertTrue(
        refreshOverlayBody.Contains("SetFog(", StringComparison.Ordinal),
        "strategic fog overlay should rebuild from visible circles only");
    AssertTrue(
        refreshOverlayBody.Contains("BuildStrategicFogOverlayCircles", StringComparison.Ordinal),
        "strategic fog overlay should still project the current visible circles");
    AssertTrue(
        serviceSource.Contains("ExploredCells.Clear()", StringComparison.Ordinal),
        "strategic fog service should clear revealed-history state in binary mode");
    AssertTrue(
        !refreshFogBody.Contains("newlyExploredCells", StringComparison.Ordinal),
        "strategic fog refresh should not calculate newly explored cells in binary mode");

    string mapSetupSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.MapSetup.cs"));
    string updateWorldCameraViewBody = ExtractMethodBody(mapSetupSource, "private bool UpdateWorldCameraView(bool force = false)");
    AssertTrue(
        updateWorldCameraViewBody.Contains("_worldMapOverlay.Position = _worldMapRoot.GlobalPosition", StringComparison.Ordinal) &&
        updateWorldCameraViewBody.Contains("_worldMapOverlay.Scale = _worldMapRoot.GlobalScale", StringComparison.Ordinal) &&
        !updateWorldCameraViewBody.Contains("RefreshStrategicFogOverlay()", StringComparison.Ordinal),
        "strategic camera changes should only sync the world canvas transform and leave fog rebuilds to world-state changes");

    string uiBootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    string resetWorldBody = ExtractMethodBody(uiBootstrapSource, "private void ResetWorld()");
    AssertTrue(
        resetWorldBody.Contains("ResetStrategicFogMaskCache", StringComparison.Ordinal),
        "strategic world reset should invalidate the fog mask cache before rebuilding a new runtime state");
}

internal static void StrategicFogOverlayUsesMapBoundsSurface()
{
    string root = ProjectRoot();
    string overlayPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldFogOverlay.cs");
    string fogRootPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.Fog.cs");

    string overlaySource = File.ReadAllText(overlayPath);
    string fogRootSource = File.ReadAllText(fogRootPath);
    string refreshOverlayBody = ExtractMethodBody(fogRootSource, "private void RefreshStrategicFogOverlay()");

    AssertTrue(
        refreshOverlayBody.Contains("TryCalculateStrategicMapBounds(out Rect2 mapBounds)", StringComparison.Ordinal) &&
        refreshOverlayBody.Contains("_fogOverlay.SetFog(", StringComparison.Ordinal),
        "strategic fog refresh should pass the strategic map bounds into the fog overlay.");
    AssertTrue(
        overlaySource.Contains("private Rect2 _fogMapBounds", StringComparison.Ordinal) &&
        overlaySource.Contains("ApplyFogSurfaceBounds(bounds)", StringComparison.Ordinal) &&
        overlaySource.Contains("new Rect2(Vector2.Zero, bounds.Size)", StringComparison.Ordinal),
        "strategic fog overlay should size and position its drawing surface from map bounds instead of keeping a viewport-sized ColorRect.");
    AssertTrue(
        overlaySource.Contains("BuildCircleParameters(visibleCircles, -bounds.Position", StringComparison.Ordinal) &&
        overlaySource.Contains("BuildCircleParameters(visibleCircles, GetFogCircleOffset()", StringComparison.Ordinal),
        "strategic fog overlay should convert map-space visibility circles into the map-bounds surface local space before passing them to the shader.");
}
}
