using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class TargetBattleAdvanceFailureStateBoundaryRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime advance failure state is boundary owned", RuntimeAdvanceFailureStateIsBoundaryOwned);
    }

    private static void RuntimeAdvanceFailureStateIsBoundaryOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string boundaryPath = Path.Combine(battleRuntimePath, "BattleAdvanceFailureStateBoundary.cs");
        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var failures = new List<string>();
        if (!File.Exists(boundaryPath))
        {
            failures.Add("BattleAdvanceFailureStateBoundary source file should exist: src/Runtime/Battle/BattleAdvanceFailureStateBoundary.cs");
        }
        else
        {
            string boundarySource = File.ReadAllText(boundaryPath);
            string boundaryRelativePath = ToRepoPath(root, boundaryPath);
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "delegate void RecordAdvanceFailureCallback", "advance-failure boundary should retain the callback delegate");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "class BattleAdvanceFailureStateBoundary", "advance-failure boundary should define the runtime boundary class");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "RecordAdvanceFailure", "advance-failure boundary should own failure recording");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "ResetAdvanceFailureState", "advance-failure boundary should own failure reset");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "ConsecutiveAdvanceFailures++", "advance-failure recording should preserve counter increment semantics");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "LastAdvanceFailureReason", "advance-failure boundary should preserve last failure reason semantics");
            AssertRequiredPresent(boundarySource, boundaryRelativePath, failures, "advance_failed", "advance-failure boundary should preserve fallback reason semantics");
        }

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            if (Regex.IsMatch(source, @"\b(?:private|internal|public)\s+(?:static\s+)?void\s+RecordAdvanceFailure\s*\("))
            {
                failures.Add($"BattleRuntimeTickResolver should not define advance-failure recording: file={relativePath}");
            }

            if (Regex.IsMatch(source, @"\b(?:private|internal|public)\s+(?:static\s+)?void\s+ResetAdvanceFailureState\s*\("))
            {
                failures.Add($"BattleRuntimeTickResolver should not define advance-failure reset: file={relativePath}");
            }

            AssertForbiddenAbsent(source, relativePath, failures, "ConsecutiveAdvanceFailures++", "BattleRuntimeTickResolver should not mutate advance-failure counters directly");
            AssertForbiddenAbsent(source, relativePath, failures, "ConsecutiveAdvanceFailures = 0", "BattleRuntimeTickResolver should not reset advance-failure counters directly");
            AssertForbiddenAbsent(source, relativePath, failures, "LastAdvanceFailureReason =", "BattleRuntimeTickResolver should not mutate advance-failure reason directly");
        }

        foreach (string runtimeFile in Directory.GetFiles(battleRuntimePath, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (string.Equals(runtimeFile, boundaryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeTickResolver.RecordAdvanceFailure", "runtime files should not reach advance-failure recording through the tick resolver");
            AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeTickResolver.ResetAdvanceFailureState", "runtime files should not reach advance-failure reset through the tick resolver");
            AssertForbiddenRegexAbsent(source, relativePath, failures, @"\.\s*ConsecutiveAdvanceFailures\s*(?:\+\+|--|\+=|-=|=)", "runtime files should not mutate advance-failure counters directly outside the boundary");
            AssertForbiddenRegexAbsent(source, relativePath, failures, @"\.\s*LastAdvanceFailureReason\s*=", "runtime files should not mutate advance-failure reasons directly outside the boundary");
        }

        string tickResolverSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));
        AssertRequiredPresent(tickResolverSource, "BattleRuntimeTickResolver*.cs", failures, "BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", "tick resolver should enter decision-phase advance-failure callbacks through the decision phase coordinator");

        if (!File.Exists(decisionPhaseCoordinatorPath))
        {
            failures.Add("BattleRuntimeDecisionPhaseCoordinator source file should exist: src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs");
        }
        else
        {
            string coordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
            AssertRequiredPresent(coordinatorSource, "src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs", failures, "BattleAdvanceFailureStateBoundary.RecordAdvanceFailure", "decision phase coordinator should pass the neutral advance-failure record callback into decision outcome application");
            AssertRequiredPresent(coordinatorSource, "src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs", failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "decision phase coordinator should pass the neutral advance-failure reset callback into decision outcome application");
        }

        string movementCommitSource = File.ReadAllText(Path.Combine(battleRuntimePath, "BattleMovementCommitResolver.cs"));
        AssertRequiredPresent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "BattleAdvanceFailureStateBoundary.RecordAdvanceFailure", "movement commit failures should record through the neutral boundary");
        AssertForbiddenAbsent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "ConsecutiveAdvanceFailures++", "movement commit should not mutate advance-failure counters directly");
        AssertForbiddenAbsent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "ConsecutiveAdvanceFailures = 0", "movement commit should not reset advance-failure counters directly");
        AssertForbiddenAbsent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "LastAdvanceFailureReason =", "movement commit should not mutate advance-failure reason directly");

        string movementCommitBoundarySource = File.ReadAllText(Path.Combine(battleRuntimePath, "BattleMovementCommitBoundary.cs"));
        AssertRequiredPresent(movementCommitBoundarySource, "src/Runtime/Battle/BattleMovementCommitBoundary.cs", failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "movement commit success should reset through the accepted-commit boundary");

        string actionControllerSource = File.ReadAllText(Path.Combine(battleRuntimePath, "BattleActionController.cs"));
        AssertRequiredPresent(actionControllerSource, "src/Runtime/Battle/BattleActionController.cs", failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "basic attack start should reset through the neutral boundary");

        string staleRetargetSource = File.ReadAllText(Path.Combine(battleRuntimePath, "BattleStaleAdvanceRetargeting.cs"));
        AssertRequiredPresent(staleRetargetSource, "src/Runtime/Battle/BattleStaleAdvanceRetargeting.cs", failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "stale-target retarget cleanup should reset through the neutral boundary");

        AssertTrue(
            failures.Count == 0,
            "Core Slice H11 advance failure state boundary guard failed: " + string.Join("; ", failures));
    }

    private static void AssertRequiredPresent(string source, string relativePath, List<string> failures, string required, string message)
    {
        if (!source.Contains(required, StringComparison.Ordinal))
        {
            failures.Add($"{message}: file={relativePath} required={required}");
        }
    }

    private static void AssertForbiddenAbsent(string source, string relativePath, List<string> failures, string forbidden, string message)
    {
        if (source.Contains(forbidden, StringComparison.Ordinal))
        {
            failures.Add($"{message}: file={relativePath} forbidden={forbidden}");
        }
    }

    private static void AssertForbiddenRegexAbsent(string source, string relativePath, List<string> failures, string forbiddenPattern, string message)
    {
        if (Regex.IsMatch(source, forbiddenPattern))
        {
            failures.Add($"{message}: file={relativePath} forbiddenPattern={forbiddenPattern}");
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

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
