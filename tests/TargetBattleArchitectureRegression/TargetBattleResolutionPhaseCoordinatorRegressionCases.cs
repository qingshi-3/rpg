using System;
using System.IO;
using System.Linq;

internal static class TargetBattleResolutionPhaseCoordinatorRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime resolution phase coordination is service owned", RuntimeResolutionPhaseCoordinationIsServiceOwned);
    }

    private static void RuntimeResolutionPhaseCoordinationIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string coordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string[] tickResolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(File.Exists(coordinatorPath), "Core Slice H22 should author BattleRuntimeResolutionPhaseCoordinator");
        AssertTrue(tickResolverFiles.Length > 0, "BattleRuntimeTickResolver partial files should exist");

        string coordinatorSource = File.ReadAllText(coordinatorPath);
        string combinedTickResolverSource = string.Join(Environment.NewLine, tickResolverFiles.Select(File.ReadAllText));
        string coordinatorRelativePath = ToRepoPath(root, coordinatorPath);

        AssertContains(coordinatorSource, "class BattleRuntimeResolutionPhaseCoordinator", coordinatorRelativePath, "resolution phase coordinator should be an explicit runtime service");
        AssertContains(coordinatorSource, "AdvanceResolutionPhase", coordinatorRelativePath, "resolution phase coordinator should expose one tick-phase entry");
        AssertContains(coordinatorSource, "BattleAttackEngagementCoordinator.Resolve", coordinatorRelativePath, "resolution phase coordinator should enter attack engagement");
        AssertContains(coordinatorSource, "BattleMovementCommitResolver.Resolve", coordinatorRelativePath, "resolution phase coordinator should enter movement commit");
        AssertContains(coordinatorSource, "BattleStaleAdvanceRetargeting.CreateCallback", coordinatorRelativePath, "resolution phase coordinator should provide stale-target retargeting callback to movement commit");
        AssertContains(coordinatorSource, "BattleMovementController.ClearEndedMovementChains", coordinatorRelativePath, "resolution phase coordinator should clear completed movement chains");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(state)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing runtime state");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(state.Actors)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing actor list");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(stream)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing event stream");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(navigationGraph)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing navigation graph");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(decisionPhase)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing decision phase result");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(occupancy)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing occupancy");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(movementCompletedActorIds)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing movement-completed actor ids");
        AssertContains(coordinatorSource, "ArgumentNullException.ThrowIfNull(aiExecutor)", coordinatorRelativePath, "resolution phase coordinator should fail fast on missing AI executor");
        AssertDoesNotContain(coordinatorSource, "return BattleRuntimeResolutionPhaseResult.Empty", coordinatorRelativePath, "resolution phase coordinator should not silently skip required phase inputs");
        AssertContains(coordinatorSource, "RecordMovementResolveElapsedTicks", coordinatorRelativePath, "resolution phase coordinator should own movement resolve timing counters");
        AssertContains(coordinatorSource, "RecordActorsReadyNoMoveLastAdvance", coordinatorRelativePath, "resolution phase coordinator should own no-move diagnostic counters");
        AssertContains(coordinatorSource, "Contexts", coordinatorRelativePath, "resolution phase coordinator should return contexts for diagnostics");
        AssertTokenOrder(
            coordinatorSource,
            coordinatorRelativePath,
            new[]
            {
                "BattleAttackEngagementCoordinator.Resolve",
                "BattleMovementCommitResolver.Resolve",
                "RecordMovementResolveElapsedTicks",
                "RecordActorsReadyNoMoveLastAdvance",
                "BattleMovementController.ClearEndedMovementChains"
            },
            "resolution phase coordinator should preserve attack -> movement commit -> counters -> cleanup order");

        AssertContains(combinedTickResolverSource, "BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter resolution phase through the coordinator");
        foreach (string tickResolverFile in tickResolverFiles)
        {
            string tickResolverSource = File.ReadAllText(tickResolverFile);
            string tickResolverRelativePath = ToRepoPath(root, tickResolverFile);
            AssertDoesNotContain(tickResolverSource, "BattleAttackEngagementCoordinator.Resolve", tickResolverRelativePath, "tick resolver partials must not directly enter attack engagement after H22");
            AssertDoesNotContain(tickResolverSource, "BattleMovementCommitResolver.Resolve", tickResolverRelativePath, "tick resolver partials must not directly enter movement commit after H22");
            AssertDoesNotContain(tickResolverSource, "BattleStaleAdvanceRetargeting.CreateCallback", tickResolverRelativePath, "tick resolver partials must not directly create stale-target retargeting callbacks after H22");
            AssertDoesNotContain(tickResolverSource, "BattleMovementController.ClearEndedMovementChains", tickResolverRelativePath, "tick resolver partials must not directly clear movement chains after H22");
            AssertDoesNotContain(tickResolverSource, "RecordMovementResolveElapsedTicks", tickResolverRelativePath, "tick resolver partials must not own movement resolve timing counters after H22");
            AssertDoesNotContain(tickResolverSource, "RecordActorsReadyNoMoveLastAdvance", tickResolverRelativePath, "tick resolver partials must not own no-move diagnostic counters after H22");
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

    private static void AssertTokenOrder(string source, string relativePath, string[] orderedTokens, string message)
    {
        int previousIndex = -1;
        foreach (string token in orderedTokens)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            AssertTrue(index >= 0, $"{message}: file={relativePath} missing={token}");
            AssertTrue(index > previousIndex, $"{message}: file={relativePath} out_of_order={token}");
            previousIndex = index;
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
