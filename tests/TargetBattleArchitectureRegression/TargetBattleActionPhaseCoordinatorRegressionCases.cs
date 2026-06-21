using System;
using System.IO;
using System.Linq;

internal static class TargetBattleActionPhaseCoordinatorRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime action phase coordination is service owned", RuntimeActionPhaseCoordinationIsServiceOwned);
    }

    private static void RuntimeActionPhaseCoordinationIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string coordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeActionPhaseCoordinator.cs");
        string[] tickResolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(File.Exists(coordinatorPath), "Core Slice H20 should author BattleRuntimeActionPhaseCoordinator");
        AssertTrue(tickResolverFiles.Length > 0, "BattleRuntimeTickResolver partial files should exist");

        string coordinatorSource = File.ReadAllText(coordinatorPath);
        string combinedTickResolverSource = string.Join(Environment.NewLine, tickResolverFiles.Select(File.ReadAllText));
        string coordinatorRelativePath = ToRepoPath(root, coordinatorPath);

        AssertContains(coordinatorSource, "class BattleRuntimeActionPhaseCoordinator", coordinatorRelativePath, "action phase coordinator should be an explicit runtime service");
        AssertContains(coordinatorSource, "AdvanceActionPhase", coordinatorRelativePath, "action phase coordinator should expose one tick-phase entry");
        AssertContains(coordinatorSource, "BattleMovementBoundaryCoordinator.AdvanceBoundaries", coordinatorRelativePath, "action phase coordinator should own movement boundary advancement");
        AssertContains(coordinatorSource, "BattleRuntimeEventFactory.CreateMovementEvent", coordinatorRelativePath, "action phase coordinator should emit movement-completed events");
        AssertContains(coordinatorSource, "BattleActionController.AdvanceAttackRecoveryBoundaries", coordinatorRelativePath, "action phase coordinator should own attack recovery boundary advancement");
        AssertContains(coordinatorSource, "BattleAbilityTickCoordinator.ResolvePending", coordinatorRelativePath, "action phase coordinator should enter ability ticking");
        AssertContains(coordinatorSource, "SkillConsumedActorIds", coordinatorRelativePath, "action phase coordinator should return actors consumed by skill release or movement-boundary waiting");
        AssertContains(coordinatorSource, "MovementCompletedActorIds", coordinatorRelativePath, "action phase coordinator should return movement boundary actors for later continuation filtering");
        AssertContains(coordinatorSource, "Occupancy", coordinatorRelativePath, "action phase coordinator should return movement-boundary occupancy for later decisions");

        AssertContains(combinedTickResolverSource, "BattleRuntimeActionPhaseCoordinator.AdvanceActionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter action phase through the coordinator");
        foreach (string tickResolverFile in tickResolverFiles)
        {
            string tickResolverSource = File.ReadAllText(tickResolverFile);
            string tickResolverRelativePath = ToRepoPath(root, tickResolverFile);
            AssertDoesNotContain(tickResolverSource, "BattleMovementBoundaryCoordinator.AdvanceBoundaries", tickResolverRelativePath, "tick resolver partials must not directly advance movement boundaries after H20");
            AssertDoesNotContain(tickResolverSource, "BattleRuntimeEventFactory.CreateMovementEvent", tickResolverRelativePath, "tick resolver partials must not directly emit movement-completed events after H20");
            AssertDoesNotContain(tickResolverSource, "BattleActionController.AdvanceAttackRecoveryBoundaries", tickResolverRelativePath, "tick resolver partials must not directly advance attack recovery after H20");
            AssertDoesNotContain(tickResolverSource, "BattleAbilityTickCoordinator.ResolvePending", tickResolverRelativePath, "tick resolver partials must not directly enter ability ticking after H20");
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
