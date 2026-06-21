using System;
using System.IO;
using System.Linq;

internal static class TargetBattleDecisionPhaseCoordinatorRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime decision phase coordination is service owned", RuntimeDecisionPhaseCoordinationIsServiceOwned);
    }

    private static void RuntimeDecisionPhaseCoordinationIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string coordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string[] tickResolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(File.Exists(coordinatorPath), "Core Slice H21 should author BattleRuntimeDecisionPhaseCoordinator");
        AssertTrue(tickResolverFiles.Length > 0, "BattleRuntimeTickResolver partial files should exist");

        string coordinatorSource = File.ReadAllText(coordinatorPath);
        string combinedTickResolverSource = string.Join(Environment.NewLine, tickResolverFiles.Select(File.ReadAllText));
        string coordinatorRelativePath = ToRepoPath(root, coordinatorPath);

        AssertContains(coordinatorSource, "class BattleRuntimeDecisionPhaseCoordinator", coordinatorRelativePath, "decision phase coordinator should be an explicit runtime service");
        AssertContains(coordinatorSource, "AdvanceDecisionPhase", coordinatorRelativePath, "decision phase coordinator should expose one tick-phase entry");
        AssertContains(coordinatorSource, "BattleTacticalObservationUpdater.RefreshAtTickStart", coordinatorRelativePath, "decision phase coordinator should own tick-start tactical observation entry");
        AssertContains(coordinatorSource, "BattleTickStartProjectionBuilder.BuildFactMap", coordinatorRelativePath, "decision phase coordinator should own tick-start fact projection entry");
        AssertContains(coordinatorSource, "BattleRuntimeDecisionContextBuilder.Build", coordinatorRelativePath, "decision phase coordinator should build actor decision contexts through the builder service");
        AssertContains(coordinatorSource, "BattleDecisionOutcomeApplier.Apply", coordinatorRelativePath, "decision phase coordinator should apply actor decision outcomes through the applier service");
        AssertContains(coordinatorSource, "BattleMovementController.BuildContinuationContexts", coordinatorRelativePath, "decision phase coordinator should build movement continuation contexts through the actor movement controller");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(state)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing runtime state");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(stream)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing event stream");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(navigationGraph)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing navigation graph");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(aiExecutor)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing AI executor");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(occupancy)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing occupancy");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(movementCompletedActorIds)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing movement-completed actor ids");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(skillConsumedActorIds)", coordinatorRelativePath, "decision phase coordinator should fail fast on missing skill-consumed actor ids");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(tickStartFacts)", coordinatorRelativePath, "decision phase result should preserve non-null tick-start fact invariants");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(contexts)", coordinatorRelativePath, "decision phase result should preserve non-null context invariants");
        AssertDoesNotContain(coordinatorSource, "movementCompletedActorIds?.Contains", coordinatorRelativePath, "decision phase coordinator should not treat missing movement-completed ids as empty");
        AssertDoesNotContain(coordinatorSource, "skillConsumedActorIds?.Contains", coordinatorRelativePath, "decision phase coordinator should not treat missing skill-consumed ids as empty");
        AssertDoesNotContain(coordinatorSource, "movementCompletedActorIds ??", coordinatorRelativePath, "decision phase coordinator should not replace a missing movement-completed set with empty");
        AssertDoesNotContain(coordinatorSource, "tickStartFacts ??", coordinatorRelativePath, "decision phase result should not normalize missing tick-start facts");
        AssertDoesNotContain(coordinatorSource, "contexts ??", coordinatorRelativePath, "decision phase result should not normalize missing contexts");
        AssertContains(coordinatorSource, "TickStartFacts", coordinatorRelativePath, "decision phase coordinator should return tick-start facts for later commit phases");
        AssertContains(coordinatorSource, "Contexts", coordinatorRelativePath, "decision phase coordinator should return ordered contexts for later attack and movement phases");
        AssertContains(coordinatorSource, "DecisionReadyActorCount", coordinatorRelativePath, "decision phase coordinator should return the pre-movement decision-ready count for diagnostics");
        AssertContains(coordinatorSource, "HasLivingActors", coordinatorRelativePath, "decision phase coordinator should tell the tick resolver whether later phases should continue");

        AssertContains(combinedTickResolverSource, "BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter decision phase through the coordinator");
        AssertContains(combinedTickResolverSource, "ArgumentNullException.ThrowIfNull(aiExecutor)", "BattleRuntimeTickResolver*.cs", "tick resolver should fail fast when constructed without its AI executor");
        AssertDoesNotContain(combinedTickResolverSource, "new DefaultBattleRuntimeAiExecutor()", "BattleRuntimeTickResolver*.cs", "tick resolver should not hide missing AI executor wiring behind an internal fallback");
        foreach (string tickResolverFile in tickResolverFiles)
        {
            string tickResolverSource = File.ReadAllText(tickResolverFile);
            string tickResolverRelativePath = ToRepoPath(root, tickResolverFile);
            AssertDoesNotContain(tickResolverSource, "BattleTacticalObservationUpdater.RefreshAtTickStart", tickResolverRelativePath, "tick resolver partials must not directly enter tactical observation after H21");
            AssertDoesNotContain(tickResolverSource, "BattleTickStartProjectionBuilder.BuildFactMap", tickResolverRelativePath, "tick resolver partials must not directly build tick-start facts after H21");
            AssertDoesNotContain(tickResolverSource, "BattleRuntimeDecisionContextBuilder.Build", tickResolverRelativePath, "tick resolver partials must not directly build decision contexts after H21");
            AssertDoesNotContain(tickResolverSource, "BattleDecisionOutcomeApplier.Apply", tickResolverRelativePath, "tick resolver partials must not directly apply decision outcomes after H21");
            AssertDoesNotContain(tickResolverSource, "BattleMovementController.BuildContinuationContexts", tickResolverRelativePath, "tick resolver partials must not directly build movement continuation contexts after H21");
        }
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("project root not found");
    }

    private static string ToRepoPath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static void AssertContains(string source, string expected, string relativePath, string message)
    {
        AssertTrue(source.Contains(expected, StringComparison.Ordinal), $"{message}: file={relativePath} expected={expected}");
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath, string message)
    {
        AssertTrue(!source.Contains(forbidden, StringComparison.Ordinal), $"{message}: file={relativePath} forbidden={forbidden}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
