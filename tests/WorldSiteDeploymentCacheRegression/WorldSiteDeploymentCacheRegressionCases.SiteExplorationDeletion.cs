internal static partial class WorldSiteDeploymentCacheRegressionCases
{
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
}
