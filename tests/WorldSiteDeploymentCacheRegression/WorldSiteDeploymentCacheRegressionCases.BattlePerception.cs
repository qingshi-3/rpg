internal static partial class WorldSiteDeploymentCacheRegressionCases
{
    internal static void BattleRuntimePerceptionOverlayTogglesWithE()
    {
        string root = ProjectRoot();
        string siteRootSource = ReadWorldSiteRootSource();
        string overlaySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "World",
            "Sites",
            "WorldSiteRoot.BattlePerceptionOverlay.cs"));
        string highlightKindSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "Battle",
            "BattleGridHighlightKind.cs"));
        string highlightSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "Battle",
            "BattleGridHighlightOverlay.cs"));
        string policySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Application",
            "Battle",
            "BattlePerceptionPolicy.cs"));
        string runtimeSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "BattleRuntimeTickResolver.cs"));

        AssertTrue(
            policySource.Contains("DefaultLocalPerceptionRange = 4", StringComparison.Ordinal) &&
            runtimeSource.Contains("BattlePerceptionPolicy.DefaultLocalPerceptionRange", StringComparison.Ordinal),
            "runtime and presentation should share the same local perception range policy");
        AssertTrue(
            siteRootSource.Contains("TryHandleBattlePerceptionOverlayInput(@event)", StringComparison.Ordinal) &&
            overlaySource.Contains("Key.E", StringComparison.Ordinal) &&
            overlaySource.Contains("BattlePerceptionOverlayToggled", StringComparison.Ordinal),
            "battle runtime should toggle perception range debug overlay with E");
        AssertTrue(
            highlightKindSource.Contains("FriendlyPerception", StringComparison.Ordinal) &&
            highlightKindSource.Contains("EnemyPerception", StringComparison.Ordinal) &&
            highlightSource.Contains("FriendlyPerceptionColor", StringComparison.Ordinal) &&
            highlightSource.Contains("EnemyPerceptionColor", StringComparison.Ordinal),
            "highlight overlay should expose separate player and enemy perception layers");
        AssertTrue(
            overlaySource.Contains("SetCellsBatch", StringComparison.Ordinal) &&
            overlaySource.Contains("BattleGridHighlightKind.FriendlyPerception", StringComparison.Ordinal) &&
            overlaySource.Contains("BattleGridHighlightKind.EnemyPerception", StringComparison.Ordinal) &&
            overlaySource.Contains("GetAxisGap", StringComparison.Ordinal),
            "perception overlay should draw footprint-aware ranges for both factions without presentation pathfinding");
    }
}
