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
        string heroSkillThunderPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs");
        string stateMachinePath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeActorStateMachine.cs");

        AssertTrue(File.Exists(boundaryPath), "Core Slice H17 should author BattleDisplacementCommitBoundary");

        string boundarySource = File.ReadAllText(boundaryPath);
        string effectResolverSource = File.ReadAllText(effectResolverPath);
        string heroSkillThunderSource = File.ReadAllText(heroSkillThunderPath);
        string boundaryRelativePath = ToRepoPath(root, boundaryPath);
        string effectResolverRelativePath = ToRepoPath(root, effectResolverPath);
        string heroSkillThunderRelativePath = ToRepoPath(root, heroSkillThunderPath);

        AssertContains(boundarySource, "class BattleDisplacementCommitBoundary", boundaryRelativePath, "displacement boundary should be an explicit runtime service");
        AssertContains(boundarySource, "ValidateThunderMarkTeleportDestination", boundaryRelativePath, "displacement boundary should own shared Thunder Fold validation");
        AssertContains(boundarySource, "CommitThunderMarkTeleport", boundaryRelativePath, "displacement boundary should own Thunder Fold release commit");
        AssertContains(boundarySource, "BattleRuntimeActorStateMachine.CommitDisplacement", boundaryRelativePath, "displacement boundary should call the low-level actor displacement primitive");
        AssertContains(boundarySource, "BattleEventKind.ThunderMarkTeleported", boundaryRelativePath, "displacement boundary should create teleport events");

        AssertContains(effectResolverSource, "BattleDisplacementCommitBoundary.CommitThunderMarkTeleport", effectResolverRelativePath, "effect resolver should dispatch teleport effect to displacement boundary");
        AssertDoesNotContain(effectResolverSource, "BattleDynamicOccupancy.FromActors", effectResolverRelativePath, "effect resolver must not build world occupancy for displacement");
        AssertDoesNotContain(effectResolverSource, "BattleRuntimeActorStateMachine.CommitDisplacement", effectResolverRelativePath, "effect resolver must not directly mutate actor displacement state");
        AssertDoesNotContain(effectResolverSource, "BattleEventKind.ThunderMarkTeleported", effectResolverRelativePath, "effect resolver must not create teleport events directly");
        AssertDoesNotContain(effectResolverSource, "BattleRuntimeThunderFoldDisplacementCommitted", effectResolverRelativePath, "effect resolver must not own displacement diagnostics");

        AssertContains(heroSkillThunderSource, "BattleDisplacementCommitBoundary.ValidateThunderMarkTeleportDestination", heroSkillThunderRelativePath, "command-time Thunder Fold validation should reuse the displacement boundary");
        AssertDoesNotContain(heroSkillThunderSource, "BattleDynamicOccupancy.FromActors", heroSkillThunderRelativePath, "hero skill command validation must not duplicate displacement occupancy rules");
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
