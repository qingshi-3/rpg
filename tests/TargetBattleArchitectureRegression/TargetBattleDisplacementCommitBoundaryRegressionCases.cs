using System;
using System.IO;
using System.Linq;

internal static class TargetBattleDisplacementCommitBoundaryRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime spatial displacement commit is boundary owned", RuntimeSpatialDisplacementCommitIsBoundaryOwned);
    }

    private static void RuntimeSpatialDisplacementCommitIsBoundaryOwned()
    {
        string root = ProjectRoot();
        string boundaryPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleDisplacementCommitBoundary.cs");
        string effectResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "BattleEffectResolver.cs");
        string markTeleportExecutorPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "TeleportToMarkSkillEffectExecutor.cs");
        string heroSkillMarkPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.MarkTeleport.cs");
        string stateMachinePath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeActorStateMachine.cs");

        AssertTrue(File.Exists(boundaryPath), "Core Slice H17 should author BattleDisplacementCommitBoundary");
        AssertTrue(!File.Exists(Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs")), "command validation should not keep the old ThunderMark partial after mark teleport generalization");
        AssertTrue(File.Exists(heroSkillMarkPath), "command-time mark teleport validation partial should exist");

        string boundarySource = File.ReadAllText(boundaryPath);
        string effectResolverSource = File.ReadAllText(effectResolverPath);
        string markTeleportExecutorSource = File.ReadAllText(markTeleportExecutorPath);
        string heroSkillMarkSource = File.ReadAllText(heroSkillMarkPath);
        string boundaryRelativePath = ToRepoPath(root, boundaryPath);
        string effectResolverRelativePath = ToRepoPath(root, effectResolverPath);
        string markTeleportExecutorRelativePath = ToRepoPath(root, markTeleportExecutorPath);
        string heroSkillMarkRelativePath = ToRepoPath(root, heroSkillMarkPath);

        AssertContains(boundarySource, "class BattleDisplacementCommitBoundary", boundaryRelativePath, "displacement boundary should be an explicit runtime service");
        AssertContains(boundarySource, "ValidateMarkTeleportDestination", boundaryRelativePath, "displacement boundary should own shared mark-teleport validation");
        AssertContains(boundarySource, "CommitMarkTeleport", boundaryRelativePath, "displacement boundary should own mark-teleport release commit");
        AssertContains(boundarySource, "BattleRuntimeActorStateMachine.CommitDisplacement", boundaryRelativePath, "displacement boundary should call the low-level actor displacement primitive");
        AssertContains(boundarySource, "BattleEventKind.ThunderMarkTeleported", boundaryRelativePath, "displacement boundary should create teleport events");

        AssertContains(markTeleportExecutorSource, "BattleDisplacementCommitBoundary.CommitMarkTeleport", markTeleportExecutorRelativePath, "typed teleport executor should dispatch mark teleport effect to displacement boundary");
        AssertDoesNotContain(effectResolverSource, "CommitMarkTeleport", effectResolverRelativePath, "effect resolver should not dispatch mark teleport effects after typed executor migration");
        AssertDoesNotContain(effectResolverSource, "BattleDynamicOccupancy.FromActors", effectResolverRelativePath, "effect resolver must not build world occupancy for displacement");
        AssertDoesNotContain(effectResolverSource, "BattleRuntimeActorStateMachine.CommitDisplacement", effectResolverRelativePath, "effect resolver must not directly mutate actor displacement state");
        AssertDoesNotContain(effectResolverSource, "BattleEventKind.ThunderMarkTeleported", effectResolverRelativePath, "effect resolver must not create teleport events directly");
        AssertDoesNotContain(effectResolverSource, "BattleRuntimeThunderFoldDisplacementCommitted", effectResolverRelativePath, "effect resolver must not own displacement diagnostics");

        AssertContains(heroSkillMarkSource, "BattleDisplacementCommitBoundary.ValidateMarkTeleportDestination", heroSkillMarkRelativePath, "command-time mark teleport validation should reuse the displacement boundary");
        AssertDoesNotContain(heroSkillMarkSource, "BattleDynamicOccupancy.FromActors", heroSkillMarkRelativePath, "hero skill command validation must not duplicate displacement occupancy rules");
        AssertDirectDisplacementCommitOnlyInsideBoundary(root, boundaryPath, stateMachinePath);
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

    private static void AssertDirectDisplacementCommitOnlyInsideBoundary(
        string root,
        string boundaryPath,
        string stateMachinePath)
    {
        string runtimeRoot = Path.Combine(root, "src", "Runtime", "Battle");
        string normalizedBoundaryPath = Path.GetFullPath(boundaryPath);
        string normalizedStateMachinePath = Path.GetFullPath(stateMachinePath);
        foreach (string path in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string normalizedPath = Path.GetFullPath(path);
            if (string.Equals(normalizedPath, normalizedBoundaryPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedPath, normalizedStateMachinePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            AssertDoesNotContain(
                source,
                "BattleRuntimeActorStateMachine.CommitDisplacement",
                ToRepoPath(root, path),
                "direct spatial displacement commits must stay behind BattleDisplacementCommitBoundary");
        }
    }
}
