using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class TargetBattleStaleAdvanceRetargetingRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime stale advance retargeting is service owned", RuntimeStaleAdvanceRetargetingIsServiceOwned);
    }

    private static void RuntimeStaleAdvanceRetargetingIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string servicePath = Path.Combine(battleRuntimePath, "BattleStaleAdvanceRetargeting.cs");
        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var failures = new List<string>();
        if (!File.Exists(servicePath))
        {
            failures.Add("BattleStaleAdvanceRetargeting service file should exist: src/Runtime/Battle/BattleStaleAdvanceRetargeting.cs");
        }
        else
        {
            string serviceSource = File.ReadAllText(servicePath);
            string relativePath = ToRepoPath(root, servicePath);
            AssertRequiredPresent(serviceSource, relativePath, failures, "class BattleStaleAdvanceRetargeting", "stale advance retargeting should be a standalone runtime service");
            AssertRequiredPresent(serviceSource, relativePath, failures, "CreateCallback", "stale advance retargeting service should build the commit-boundary callback");
            AssertRequiredPresent(serviceSource, relativePath, failures, "TryRetarget", "stale advance retargeting service should expose a narrow retarget entry point");
            AssertRequiredPresent(serviceSource, relativePath, failures, "BattleRuntimeDecisionContextBuilder.Build", "stale advance retargeting should rebuild contexts through the accepted decision-context builder");
            AssertRequiredPresent(serviceSource, relativePath, failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "stale advance retargeting should reset failure state through the neutral boundary");
            AssertRequiredPresent(serviceSource, relativePath, failures, "IsRetargetableStaleAdvance", "stale advance retargeting should keep retarget eligibility local to the service");
            AssertRequiredPresent(serviceSource, relativePath, failures, "ArgumentNullException.ThrowIfNull(aiExecutor)", "stale advance retargeting should fail fast on a missing AI executor");
            AssertRequiredPresent(serviceSource, relativePath, failures, "BattleRuntimeAiActionKind.AdvanceTowardTarget", "stale advance retargeting should preserve advance retarget eligibility");
            AssertRequiredPresent(serviceSource, relativePath, failures, "BattleRuntimeAiActionKind.JoinLocalCombat", "stale advance retargeting should preserve local-combat retarget eligibility");
            AssertRequiredPresent(serviceSource, relativePath, failures, "BattleRuntimeAiActionKind.HoldSupport", "stale advance retargeting should preserve support retarget eligibility");
            AssertRequiredPresent(serviceSource, relativePath, failures, "context.ActorFact.Actor.HitPoints <= 0", "stale advance retargeting should reject dead actors before rebuilding");
            AssertRequiredPresent(serviceSource, relativePath, failures, "refreshed.TargetFact == null", "stale advance retargeting should reject missing refreshed targets");
            AssertRequiredPresent(serviceSource, relativePath, failures, "refreshed.TargetFact.Value.Actor.HitPoints <= 0", "stale advance retargeting should reject dead refreshed targets");
            AssertRequiredPresent(serviceSource, relativePath, failures, "!IsRetargetableStaleAdvance(refreshed.Request.Kind)", "stale advance retargeting should reject non-retargetable refreshed requests");
            AssertRequiredPresent(serviceSource, relativePath, failures, "!refreshed.Proposal.HasMoveTo", "stale advance retargeting should require a refreshed movement proposal");
            AssertRequiredPresent(serviceSource, relativePath, failures, "!string.IsNullOrWhiteSpace(refreshed.Proposal.FailureReason)", "stale advance retargeting should reject refreshed proposals with failure reasons");
            AssertForbiddenAbsent(serviceSource, relativePath, failures, "partial class BattleRuntimeTickResolver", "stale advance retargeting service should not be a tick-resolver partial");
            AssertForbiddenAbsent(serviceSource, relativePath, failures, "_aiExecutor", "stale advance retargeting service should receive the AI executor explicitly");
            AssertForbiddenAbsent(serviceSource, relativePath, failures, "new DefaultBattleRuntimeAiExecutor", "stale advance retargeting should not create hidden AI executor fallbacks");
        }

        string combinedResolverSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));
        AssertRequiredPresent(combinedResolverSource, "BattleRuntimeTickResolver*.cs", failures, "BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase", "tick resolver should enter stale advance retargeting through the resolution phase coordinator");
        if (!File.Exists(resolutionPhaseCoordinatorPath))
        {
            failures.Add("BattleRuntimeResolutionPhaseCoordinator source file should exist: src/Runtime/Battle/BattleRuntimeResolutionPhaseCoordinator.cs");
        }
        else
        {
            string resolutionPhaseCoordinatorSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
            AssertRequiredPresent(resolutionPhaseCoordinatorSource, "src/Runtime/Battle/BattleRuntimeResolutionPhaseCoordinator.cs", failures, "BattleStaleAdvanceRetargeting.CreateCallback", "resolution phase coordinator should route stale advance retargeting through the service boundary");
        }

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            AssertForbiddenAbsent(source, relativePath, failures, "BattleStaleAdvanceRetargeting.CreateCallback", "tick resolver should not directly create stale advance retargeting callbacks after H22");
            AssertForbiddenAbsent(source, relativePath, failures, "TryRetargetStaleAdvanceContext(", "tick resolver should not define stale advance retargeting callbacks");
            AssertForbiddenAbsent(source, relativePath, failures, "IsRetargetableStaleAdvance(", "tick resolver should not own stale advance retarget eligibility");
            AssertForbiddenAbsent(source, relativePath, failures, "context.TargetFact = refreshed.TargetFact", "tick resolver should not mutate refreshed retarget contexts");
            AssertForbiddenAbsent(source, relativePath, failures, "context.Proposal = refreshed.Proposal", "tick resolver should not mutate refreshed retarget proposals");
        }

        string movementCommitSource = File.ReadAllText(Path.Combine(battleRuntimePath, "BattleMovementCommitResolver.cs"));
        AssertRequiredPresent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "TryRetargetStaleAdvanceContextCallback", "movement commit should keep the narrow stale-retarget callback boundary");
        AssertRequiredPresent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "AllowStaleTargetRetarget", "movement commit should remain the stale-target detection boundary");
        AssertForbiddenAbsent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "BattleRuntimeDecisionContextBuilder.Build", "movement commit should not rebuild stale retarget decision contexts directly");
        AssertForbiddenAbsent(movementCommitSource, "src/Runtime/Battle/BattleMovementCommitResolver.cs", failures, "new DefaultBattleRuntimeAiExecutor", "movement commit should not create hidden AI executor fallbacks");

        AssertTrue(
            failures.Count == 0,
            "Core Slice H12 stale advance retargeting boundary guard failed: " + string.Join("; ", failures));
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
