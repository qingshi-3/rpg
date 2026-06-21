using System;
using System.IO;
using System.Linq;

internal static class TargetBattleActionDiagnosticsRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime action diagnostics are boundary owned", RuntimeActionDiagnosticsAreBoundaryOwned);
    }

    private static void RuntimeActionDiagnosticsAreBoundaryOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string diagnosticsPath = Path.Combine(battleRuntimePath, "BattleRuntimeActionDiagnostics.cs");
        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string staleDiagnosticsUidPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.Diagnostics.cs.uid");
        string[] tickResolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(File.Exists(diagnosticsPath), "Core Slice H23 should author BattleRuntimeActionDiagnostics");
        AssertTrue(File.Exists(resolutionPhaseCoordinatorPath), "Core Slice H22 should author BattleRuntimeResolutionPhaseCoordinator");
        AssertTrue(!File.Exists(staleDiagnosticsUidPath), "deleted TickResolver diagnostics script should not leave stale Godot uid metadata");
        AssertTrue(tickResolverFiles.Length > 0, "BattleRuntimeTickResolver partial files should exist");

        string diagnosticsSource = File.ReadAllText(diagnosticsPath);
        string resolutionPhaseCoordinatorSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
        string diagnosticsRelativePath = ToRepoPath(root, diagnosticsPath);
        string resolutionPhaseCoordinatorRelativePath = ToRepoPath(root, resolutionPhaseCoordinatorPath);
        string combinedTickResolverSource = string.Join(Environment.NewLine, tickResolverFiles.Select(File.ReadAllText));

        AssertContains(diagnosticsSource, "class BattleRuntimeActionDiagnostics", diagnosticsRelativePath, "action diagnostics should be an explicit runtime boundary");
        AssertContains(diagnosticsSource, "LogTickActionResults", diagnosticsRelativePath, "action diagnostics should own per-tick context logging");
        AssertContains(diagnosticsSource, "ArgumentNullException.ThrowIfNull(contexts)", diagnosticsRelativePath, "action diagnostics should fail fast on a missing context handoff");
        AssertContains(diagnosticsSource, "LogRuntimeActionResult", diagnosticsRelativePath, "action diagnostics should own one-context log formatting");
        AssertContains(diagnosticsSource, "unresolved_action", diagnosticsRelativePath, "action diagnostics should preserve unresolved-action fallback assignment");
        AssertContains(diagnosticsSource, "BattleRuntimeAction battle=", diagnosticsRelativePath, "action diagnostics should preserve runtime action log text");
        AssertContains(diagnosticsSource, "\"BattleRuntimeTickResolver\"", diagnosticsRelativePath, "action diagnostics should preserve the existing trace category");
        AssertContains(diagnosticsSource, "WaitForAttackCharge", diagnosticsRelativePath, "action diagnostics should preserve attack-charge wait suppression");
        AssertContains(diagnosticsSource, "OrderBy(item => item.ActorFact.Actor.ActorId", diagnosticsRelativePath, "action diagnostics should preserve deterministic action-result log ordering");
        AssertContains(resolutionPhaseCoordinatorSource, "ArgumentNullException.ThrowIfNull(contexts)", resolutionPhaseCoordinatorRelativePath, "resolution phase result should preserve non-null context invariants");
        AssertDoesNotContain(diagnosticsSource, "contexts ??", diagnosticsRelativePath, "action diagnostics should not normalize a missing context handoff");
        AssertDoesNotContain(resolutionPhaseCoordinatorSource, "contexts ??", resolutionPhaseCoordinatorRelativePath, "resolution phase result should not normalize missing contexts");

        AssertContains(combinedTickResolverSource, "BattleRuntimeActionDiagnostics.LogTickActionResults", "BattleRuntimeTickResolver*.cs", "tick resolver should enter action diagnostics through the boundary");
        foreach (string tickResolverFile in tickResolverFiles)
        {
            string source = File.ReadAllText(tickResolverFile);
            string relativePath = ToRepoPath(root, tickResolverFile);
            AssertTrue(!Path.GetFileName(tickResolverFile).Equals("BattleRuntimeTickResolver.Diagnostics.cs", StringComparison.Ordinal), "tick resolver diagnostics partial should stay deleted after H23");
            AssertDoesNotContain(source, "private void LogTickActionResults", relativePath, "tick resolver should not own per-tick action diagnostics");
            AssertDoesNotContain(source, "LogRuntimeActionResult(", relativePath, "tick resolver should not own one-context action diagnostics");
            AssertDoesNotContain(source, "unresolved_action", relativePath, "tick resolver should not own unresolved-action diagnostic fallback");
            AssertDoesNotContain(source, "GameLog.Trace(", relativePath, "tick resolver should not directly emit action diagnostics");
            AssertDoesNotContain(source, "BattleRuntimeAction battle=", relativePath, "tick resolver should not own runtime action diagnostic log text");
            AssertDoesNotContain(source, "ResolveAttackDamage", relativePath, "tick resolver should not keep stale attack-damage helpers");
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
