internal static partial class WorldSiteDeploymentCacheRegressionCases
{
    internal static void BattleRuntimeStartsTweenMovementProbe()
    {
        string root = ProjectRoot();
        string runtimeSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "World",
            "Sites",
            "WorldSiteRoot.BattleRuntime.cs"));
        string managementSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "World",
            "Sites",
            "WorldSiteRoot.SiteManagementHud.cs"));
        string probeSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "World",
            "Sites",
            "WorldSiteRoot.BattleMovementTweenProbe.cs"));

        AssertTrue(
            runtimeSource.Contains("bool runtimeActivated = ActivateBattleGroupRuntime();", StringComparison.Ordinal) &&
            runtimeSource.Contains("PlayBattleMovementTweenProbe();", StringComparison.Ordinal),
            "battle runtime activation should launch the one-tween movement comparison probe after runtime starts");
        AssertTrue(
            managementSource.Contains("ClearBattleMovementTweenProbe();", StringComparison.Ordinal),
            "battle runtime teardown should clear the movement comparison probe because it is presentation-only");
        AssertTrue(
            probeSource.Contains("BattleMovementTweenProbeSeconds = 10.0", StringComparison.Ordinal) &&
            probeSource.Contains("EnumerateAliveFaction(BattleFaction.Player)", StringComparison.Ordinal) &&
            probeSource.Contains("_unitRoot.GetParent()?.AddChild(probe)", StringComparison.Ordinal) &&
            probeSource.Contains("PlayMove(restart: true)", StringComparison.Ordinal) &&
            probeSource.Contains("CreateTween()", StringComparison.Ordinal) &&
            probeSource.Contains("\"global_position\"", StringComparison.Ordinal) &&
            probeSource.Contains("Tween.TransitionType.Linear", StringComparison.Ordinal) &&
            probeSource.Contains("QueueFree()", StringComparison.Ordinal),
            "movement comparison probe should copy a player unit visually, move it left-to-right with one linear tween, then remove it");
    }
}
