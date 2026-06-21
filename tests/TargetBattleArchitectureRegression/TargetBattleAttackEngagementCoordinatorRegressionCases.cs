using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class TargetBattleAttackEngagementCoordinatorRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime attack engagement coordination is service owned", RuntimeAttackEngagementCoordinationIsServiceOwned);
    }

    private static void RuntimeAttackEngagementCoordinationIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string coordinatorPath = Path.Combine(battleRuntimePath, "BattleAttackEngagementCoordinator.cs");
        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string legacyResolverEngagementPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.Engagement.cs");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string combinedResolverSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));

        var failures = new List<string>();
        if (File.Exists(legacyResolverEngagementPath))
        {
            failures.Add("BattleRuntimeTickResolver.Engagement.cs should not remain as a resolver-owned attack engagement partial");
        }

        if (!File.Exists(coordinatorPath))
        {
            failures.Add("BattleAttackEngagementCoordinator source file should exist: src/Runtime/Battle/BattleAttackEngagementCoordinator.cs");
        }
        else
        {
            string coordinatorSource = File.ReadAllText(coordinatorPath);
            string coordinatorRelativePath = ToRepoPath(root, coordinatorPath);
            if (!coordinatorSource.Contains("class BattleAttackEngagementCoordinator", StringComparison.Ordinal))
            {
                failures.Add($"BattleAttackEngagementCoordinator should define the runtime service class: file={coordinatorRelativePath}");
            }

            if (!Regex.IsMatch(coordinatorSource, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+Resolve\s*\("))
            {
                failures.Add($"BattleAttackEngagementCoordinator should expose a resolve entry point: file={coordinatorRelativePath}");
            }

            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "BattleAttackResolver.Resolve", "attack engagement coordinator should call the basic attack resolver");
            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers", "attack engagement coordinator should apply post-attack engagement triggers");
            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "BattleEventKind.DamageApplied", "attack engagement coordinator should collect damage events emitted by attack resolution");
            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "firstAttackEventIndex", "attack engagement coordinator should slice only attack-resolve events from the stream");
            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "Skip(firstAttackEventIndex)", "attack engagement coordinator should slice only attack-resolve events from the stream");
            AssertRequiredPresent(coordinatorSource, coordinatorRelativePath, failures, "ArgumentNullException.ThrowIfNull(stream)", "attack engagement coordinator should fail fast on a missing event stream");
            AssertTokenOrder(
                coordinatorSource,
                coordinatorRelativePath,
                failures,
                new[]
                {
                    "firstAttackEventIndex",
                    "BattleAttackResolver.Resolve",
                    "Skip(firstAttackEventIndex)",
                    "BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers"
                },
                "attack engagement coordinator should slice events around the basic attack resolver call");
            if (!Regex.IsMatch(
                    coordinatorSource,
                    @"ApplyPostAttackEngagementTriggers\s*\(\s*state\s*,\s*attackEvents\s*,",
                    RegexOptions.Singleline))
            {
                failures.Add($"attack engagement coordinator should pass filtered attackEvents into post-attack engagement triggers: file={coordinatorRelativePath}");
            }

            AssertCoordinatorDoesNotOwnOtherRuntimeAuthority(coordinatorSource, coordinatorRelativePath, failures);
        }

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            if (Regex.IsMatch(source, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+ResolveAttackProposalsAndEngagementTriggers\s*\("))
            {
                failures.Add($"BattleRuntimeTickResolver should not define attack engagement coordination logic: file={relativePath} method=ResolveAttackProposalsAndEngagementTriggers");
            }

            AssertResolverDoesNotRegrowAttackEngagementBody(source, relativePath, failures);
        }

        if (!combinedResolverSource.Contains("BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase", StringComparison.Ordinal))
        {
            failures.Add("BattleRuntimeTickResolver should enter attack engagement through BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase");
        }

        if (!File.Exists(resolutionPhaseCoordinatorPath))
        {
            failures.Add("BattleRuntimeResolutionPhaseCoordinator source file should exist: src/Runtime/Battle/BattleRuntimeResolutionPhaseCoordinator.cs");
        }
        else
        {
            string resolutionSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
            string resolutionRelativePath = ToRepoPath(root, resolutionPhaseCoordinatorPath);
            if (!Regex.IsMatch(resolutionSource, @"\bBattleAttackEngagementCoordinator\s*\.\s*Resolve\s*\("))
            {
                failures.Add($"BattleRuntimeResolutionPhaseCoordinator should call BattleAttackEngagementCoordinator through a service entry point: file={resolutionRelativePath}");
            }
        }

        AssertTrue(
            failures.Count == 0,
            "Core Slice H10 attack engagement coordinator guard failed: " + string.Join("; ", failures));
    }

    private static void AssertCoordinatorDoesNotOwnOtherRuntimeAuthority(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementCommitResolver", "attack engagement coordinator must not commit movement");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementController", "attack engagement coordinator must not build movement proposals or continuations");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementBoundaryCoordinator", "attack engagement coordinator must not advance movement boundaries");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeDecisionContextBuilder", "attack engagement coordinator must not build AI decision contexts");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleDecisionOutcomeApplier", "attack engagement coordinator must not apply decision outcomes");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEffectResolver", "attack engagement coordinator must not execute skill effects");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleChannelDamageResolver", "attack engagement coordinator must not scan channel damage");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEffectReceiver", "attack engagement coordinator must not receive effects for actors");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleHealthComponent", "attack engagement coordinator must not own health mutation");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAbilityController", "attack engagement coordinator must not own ability lifecycle");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeHeroSkillCommandResolver", "attack engagement coordinator must not own hero skill commands");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleCommitBuffer", "attack engagement coordinator must not own commit-buffer phases");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeEventFactory", "attack engagement coordinator must not create runtime events");
        AssertForbiddenAbsent(source, relativePath, failures, "stream.Add", "attack engagement coordinator must not append runtime events directly");
        AssertForbiddenAbsent(source, relativePath, failures, ".HitPoints =", "attack engagement coordinator must not mutate health directly");
        AssertForbiddenAbsent(source, relativePath, failures, "MarkDefeated", "attack engagement coordinator must not mark defeat directly");
        AssertForbiddenAbsent(source, relativePath, failures, "CommitDisplacement", "attack engagement coordinator must not own displacement");
        AssertForbiddenAbsent(source, relativePath, failures, "MarkMovementCommitted", "attack engagement coordinator must not commit actor movement");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridX =", "attack engagement coordinator must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridY =", "attack engagement coordinator must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridHeight =", "attack engagement coordinator must not mutate spatial anchors");
    }

    private static void AssertResolverDoesNotRegrowAttackEngagementBody(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAttackResolver.Resolve", "tick resolver should not call the basic attack resolver directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers", "tick resolver should not apply post-attack engagement triggers directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventKind.DamageApplied", "tick resolver should not scan damage events for engagement triggers");
        AssertForbiddenAbsent(source, relativePath, failures, "firstAttackEventIndex", "tick resolver should not own post-attack event stream slicing");
        AssertForbiddenAbsent(source, relativePath, failures, "Skip(firstAttackEventIndex)", "tick resolver should not own post-attack event stream slicing");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleGroupEngagementStateMachine", "tick resolver should not mutate engagement state directly");
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

    private static void AssertTokenOrder(
        string source,
        string relativePath,
        List<string> failures,
        IReadOnlyList<string> orderedTokens,
        string message)
    {
        int previousIndex = -1;
        foreach (string token in orderedTokens)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                failures.Add($"{message}: file={relativePath} missing={token}");
                return;
            }

            if (index <= previousIndex)
            {
                failures.Add($"{message}: file={relativePath} out_of_order={token}");
                return;
            }

            previousIndex = index;
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
