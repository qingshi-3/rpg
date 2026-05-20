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
    string fogOverlayPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldFogOverlay.cs");
    string fogRootPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.Fog.cs");
    string shaderPath = Path.Combine(root, "assets", "world", "shaders", "strategic_fog_of_war.gdshader");

    AssertTrue(File.Exists(fogServicePath), "strategic map fog service should remain");
    AssertTrue(File.Exists(fogOverlayPath), "strategic map fog overlay should remain");
    AssertTrue(File.Exists(fogRootPath), "strategic root should keep fog presentation partial");
    AssertTrue(File.Exists(shaderPath), "strategic fog shader should remain");

    string fogSource = string.Join("\n", File.ReadAllText(fogServicePath), File.ReadAllText(fogRootPath));
    string[] forbiddenFogFragments =
    {
        "WorldSiteIntel",
        "StrategicWorldIntelState",
        "WorldIntelVisibility",
        "KnownSites",
        "ThreatPlans",
        "PendingThreatIds",
        "DefenseRaid",
        "WorldSiteMode.Alert"
    };

    foreach (string fragment in forbiddenFogFragments)
    {
        AssertTrue(!fogSource.Contains(fragment, StringComparison.Ordinal), $"strategic fog should not restore intel or raid fragment={fragment}");
    }
}
}
