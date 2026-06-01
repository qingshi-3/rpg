using System;
using System.IO;
using System.Linq;

internal static class TargetBattleTickResolverDecompositionGuard
{
    internal static void RuntimeTickResolverDelegatesAttackAndMovementMutationToServices()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(resolverFiles.Length > 0, "BattleRuntimeTickResolver source files should exist");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleAttackResolver.cs")),
            "TD-003 attack resolver service file should exist: src/Runtime/Battle/BattleAttackResolver.cs");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleMovementCommitResolver.cs")),
            "TD-003 movement resolver service file should exist: src/Runtime/Battle/BattleMovementCommitResolver.cs");

        string combinedResolverSource = "";
        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            combinedResolverSource += source;

            // Keep the legal ResolveAttackProposalsAndEngagementTriggers wrapper out of scope.
            AssertDoesNotContain(source, "void ResolveAttackProposals(", relativePath);
            AssertDoesNotContain(source, "int ResolveMovementProposals(", relativePath);
            AssertDoesNotContain(source, "MarkMovementCommitted", relativePath);
            AssertDoesNotContain(source, "MarkAttackRecovery", relativePath);
        }

        AssertTrue(
            combinedResolverSource.Contains("BattleAttackResolver.Resolve(", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should delegate attack resolution to BattleAttackResolver.Resolve(");
        AssertTrue(
            combinedResolverSource.Contains("BattleMovementCommitResolver.Resolve(", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should delegate movement resolution to BattleMovementCommitResolver.Resolve(");
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath)
    {
        AssertTrue(
            !source.Contains(forbidden, StringComparison.Ordinal),
            $"resolver decomposition guard failed: file={relativePath} forbidden={forbidden}");
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

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
