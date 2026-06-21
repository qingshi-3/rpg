using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class TargetBattleDecisionOutcomeApplierRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime decision outcome application is service owned", RuntimeDecisionOutcomeApplicationIsServiceOwned);
    }

    private static void RuntimeDecisionOutcomeApplicationIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string applierPath = Path.Combine(battleRuntimePath, "BattleDecisionOutcomeApplier.cs");
        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string failureStateBoundaryPath = Path.Combine(battleRuntimePath, "BattleAdvanceFailureStateBoundary.cs");
        string aiRequestBuilderPath = Path.Combine(battleRuntimePath, "BattleAiActionRequestBuilder.cs");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string combinedResolverSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));

        var failures = new List<string>();
        if (!File.Exists(failureStateBoundaryPath))
        {
            failures.Add("advance-failure state callback should live in a neutral boundary file: src/Runtime/Battle/BattleAdvanceFailureStateBoundary.cs");
        }
        else
        {
            string boundarySource = File.ReadAllText(failureStateBoundaryPath);
            if (!boundarySource.Contains("delegate void RecordAdvanceFailureCallback", StringComparison.Ordinal))
            {
                failures.Add($"advance-failure state boundary should define RecordAdvanceFailureCallback: file={ToRepoPath(root, failureStateBoundaryPath)}");
            }
        }

        if (File.Exists(aiRequestBuilderPath))
        {
            string aiRequestBuilderSource = File.ReadAllText(aiRequestBuilderPath);
            if (aiRequestBuilderSource.Contains("delegate void RecordAdvanceFailureCallback", StringComparison.Ordinal))
            {
                failures.Add($"AI request builder should not own the shared advance-failure state callback: file={ToRepoPath(root, aiRequestBuilderPath)}");
            }

            if (aiRequestBuilderSource.Contains("RecordAdvanceFailureCallback", StringComparison.Ordinal))
            {
                failures.Add($"AI request builder should not depend on advance-failure state callbacks: file={ToRepoPath(root, aiRequestBuilderPath)}");
            }
        }

        if (!File.Exists(applierPath))
        {
            failures.Add("BattleDecisionOutcomeApplier source file should exist: src/Runtime/Battle/BattleDecisionOutcomeApplier.cs");
        }
        else
        {
            string applierSource = File.ReadAllText(applierPath);
            string applierRelativePath = ToRepoPath(root, applierPath);
            if (!applierSource.Contains("class BattleDecisionOutcomeApplier", StringComparison.Ordinal))
            {
                failures.Add($"BattleDecisionOutcomeApplier should define the runtime service class: file={applierRelativePath}");
            }

            if (!Regex.IsMatch(applierSource, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+(?:Apply|ApplyDecisionOutcomes)\s*\("))
            {
                failures.Add($"BattleDecisionOutcomeApplier should expose an Apply entry point for actor decision outcomes: file={applierRelativePath}");
            }

            if (!applierSource.Contains("ArgumentNullException.ThrowIfNull(stream)", StringComparison.Ordinal))
            {
                failures.Add($"BattleDecisionOutcomeApplier should fail fast on a missing event stream: file={applierRelativePath}");
            }

            AssertApplierDoesNotOwnWorldAuthority(applierSource, applierRelativePath, failures);
        }

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            if (Regex.IsMatch(source, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+ApplyDecisionOutcomes\s*\("))
            {
                failures.Add($"BattleRuntimeTickResolver should not define actor decision outcome application logic: file={relativePath} method=ApplyDecisionOutcomes");
            }

            AssertResolverDoesNotRegrowDecisionOutcomeBody(source, relativePath, failures);
            AssertForbiddenAbsent(source, relativePath, failures, "BattleDecisionOutcomeApplier.Apply", "tick resolver should enter decision outcomes through BattleRuntimeDecisionPhaseCoordinator");
        }

        if (!combinedResolverSource.Contains("BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", StringComparison.Ordinal))
        {
            failures.Add("BattleRuntimeTickResolver should enter decision outcome application through BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase");
        }

        if (!File.Exists(decisionPhaseCoordinatorPath))
        {
            failures.Add("BattleRuntimeDecisionPhaseCoordinator source file should exist: src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs");
        }
        else
        {
            string coordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
            string coordinatorRelativePath = ToRepoPath(root, decisionPhaseCoordinatorPath);
            if (!Regex.IsMatch(coordinatorSource, @"\bBattleDecisionOutcomeApplier\s*\.\s*(?:Apply|ApplyDecisionOutcomes)\s*\("))
            {
                failures.Add($"BattleRuntimeDecisionPhaseCoordinator should call BattleDecisionOutcomeApplier through a service entry point: file={coordinatorRelativePath}");
            }
        }

        AssertTrue(
            failures.Count == 0,
            "Core Slice H8 decision outcome applier guard failed: " + string.Join("; ", failures));
    }

    private static void AssertApplierDoesNotOwnWorldAuthority(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAiActionRequestBuilder", "decision outcome applier must not build AI requests");
        AssertForbiddenAbsent(source, relativePath, failures, ".ChooseAction(", "decision outcome applier must not choose AI actions");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleTargetSelectionService", "decision outcome applier must not select targets");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementCommitResolver", "decision outcome applier must not commit movement");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementController", "decision outcome applier must not build movement proposals or continuations");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementBoundaryCoordinator", "decision outcome applier must not advance movement boundaries");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAttackResolver", "decision outcome applier must not resolve attacks");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEffectResolver", "decision outcome applier must not execute effects");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleCommitBuffer", "decision outcome applier must not own commit-buffer phases");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAbilityController", "decision outcome applier must not own ability lifecycle");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeHeroSkillCommandResolver", "decision outcome applier must not own hero skill commands");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventKind.DamageApplied", "decision outcome applier must not emit damage events");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventKind.EffectApplied", "decision outcome applier must not emit effect events");
        AssertForbiddenAbsent(source, relativePath, failures, ".HitPoints =", "decision outcome applier must not mutate health");
        AssertForbiddenAbsent(source, relativePath, failures, "MarkDefeated", "decision outcome applier must not mark defeat");
        AssertForbiddenAbsent(source, relativePath, failures, "CommitDisplacement", "decision outcome applier must not own spatial displacement");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridX =", "decision outcome applier must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridY =", "decision outcome applier must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridHeight =", "decision outcome applier must not mutate spatial anchors");
    }

    private static void AssertResolverDoesNotRegrowDecisionOutcomeBody(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleGroupPlanRuntimeState.TargetLocked", "tick resolver should not apply target-lock plan states directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleGroupPlanRuntimeState.AdvancingToObjective", "tick resolver should not apply objective plan states directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeActorStateMachine.MarkWaitingForCharge", "tick resolver should not apply wait-for-charge outcomes directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeActorStateMachine.MarkHolding", "tick resolver should not apply hold/failure outcomes directly");
        AssertForbiddenAbsent(source, relativePath, failures, "context.ActorFact.Actor.TargetActorId =", "tick resolver should not write decision target outcomes directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeAiActionResult.Succeeded(context.Request, \"held\")", "tick resolver should not apply hold outcome results directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeAiActionResult.Failed(context.Request, \"unsupported_action\")", "tick resolver should not apply unsupported-action outcome results directly");
    }

    private static void AssertForbiddenAbsent(string source, string relativePath, List<string> failures, string forbidden, string message)
    {
        if (source.Contains(forbidden, StringComparison.Ordinal))
        {
            failures.Add($"{message}: file={relativePath} forbidden={forbidden}");
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
