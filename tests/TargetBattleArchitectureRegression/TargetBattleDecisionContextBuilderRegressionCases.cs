using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class TargetBattleDecisionContextBuilderRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime decision context construction is service owned", RuntimeDecisionContextConstructionIsServiceOwned);
    }

    private static void RuntimeDecisionContextConstructionIsServiceOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string builderPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionContextBuilder.cs");
        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string staleRetargetPath = Path.Combine(battleRuntimePath, "BattleStaleAdvanceRetargeting.cs");
        string combatZoneJoinRetargetingPath = Path.Combine(battleRuntimePath, "BattleCombatZoneJoinRetargeting.cs");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string combinedResolverSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));

        var failures = new List<string>();
        if (!File.Exists(builderPath))
        {
            failures.Add("BattleRuntimeDecisionContextBuilder source file should exist: src/Runtime/Battle/BattleRuntimeDecisionContextBuilder.cs");
        }
        else
        {
            string builderSource = File.ReadAllText(builderPath);
            string builderRelativePath = ToRepoPath(root, builderPath);
            if (!builderSource.Contains("class BattleRuntimeDecisionContextBuilder", StringComparison.Ordinal))
            {
                failures.Add($"BattleRuntimeDecisionContextBuilder should define the runtime service class: file={builderRelativePath}");
            }

            if (!Regex.IsMatch(builderSource, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+(?:Build|BuildForActor|BuildDecisionContext)\s*\("))
            {
                failures.Add($"BattleRuntimeDecisionContextBuilder should expose a build entry point: file={builderRelativePath}");
            }

            AssertBuilderOwnsDecisionContextInputs(builderSource, builderRelativePath, failures);
            AssertBuilderDoesNotOwnWorldAuthority(builderSource, builderRelativePath, failures);
            AssertBuilderDoesNotOwnOutcomeOrExecutorFallback(builderSource, builderRelativePath, failures);
        }

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            if (Regex.IsMatch(source, @"\b(?:private|internal|public)\s+(?:static\s+)?\w[\w<>,\s\?\[\]]*\s+BuildTickContext\s*\("))
            {
                failures.Add($"BattleRuntimeTickResolver should not define actor decision context construction: file={relativePath} method=BuildTickContext");
            }

            AssertResolverDoesNotRegrowDecisionContextBody(source, relativePath, failures);
            AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeDecisionContextBuilder.Build", "tick resolver should enter decision context construction through BattleRuntimeDecisionPhaseCoordinator");
        }

        if (!combinedResolverSource.Contains("BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", StringComparison.Ordinal))
        {
            failures.Add("BattleRuntimeTickResolver should enter decision context construction through BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase");
        }

        if (!File.Exists(decisionPhaseCoordinatorPath))
        {
            failures.Add("BattleRuntimeDecisionPhaseCoordinator source file should exist: src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs");
        }
        else
        {
            string coordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
            string coordinatorRelativePath = ToRepoPath(root, decisionPhaseCoordinatorPath);
            if (!Regex.IsMatch(coordinatorSource, @"\bBattleRuntimeDecisionContextBuilder\s*\.\s*(?:Build|BuildForActor|BuildDecisionContext)\s*\("))
            {
                failures.Add($"BattleRuntimeDecisionPhaseCoordinator should call BattleRuntimeDecisionContextBuilder through a service entry point: file={coordinatorRelativePath}");
            }
        }

        if (!File.Exists(staleRetargetPath))
        {
            failures.Add("BattleStaleAdvanceRetargeting source file should exist");
        }
        else
        {
            string staleRetargetSource = File.ReadAllText(staleRetargetPath);
            string relativePath = ToRepoPath(root, staleRetargetPath);
            AssertForbiddenAbsent(staleRetargetSource, relativePath, failures, "BuildTickContext(", "stale-target retargeting should not call a hidden resolver context builder");
            if (!Regex.IsMatch(staleRetargetSource, @"\bBattleRuntimeDecisionContextBuilder\s*\.\s*(?:Build|BuildForActor|BuildDecisionContext)\s*\("))
            {
                failures.Add($"stale-target retargeting should refresh through BattleRuntimeDecisionContextBuilder: file={relativePath}");
            }
        }

        if (File.Exists(combatZoneJoinRetargetingPath))
        {
            string combatZoneJoinRetargetingSource = File.ReadAllText(combatZoneJoinRetargetingPath);
            string relativePath = ToRepoPath(root, combatZoneJoinRetargetingPath);
            AssertForbiddenAbsent(combatZoneJoinRetargetingSource, relativePath, failures, "BattleCombatSlotIntentResolver", "alternate combat-zone retargeting should not bypass the actor movement controller slot proposal boundary");
            AssertForbiddenAbsent(combatZoneJoinRetargetingSource, relativePath, failures, "BattleCrowdMovementPlanner", "alternate combat-zone retargeting should not bypass the actor movement controller crowd proposal boundary");
            AssertForbiddenAbsent(combatZoneJoinRetargetingSource, relativePath, failures, "BattleRuntimeTickContextFactory.Create", "alternate combat-zone retargeting should not build movement contexts outside the actor movement controller");
        }

        AssertTrue(
            failures.Count == 0,
            "Core Slice H9 decision context builder guard failed: " + string.Join("; ", failures));
    }

    private static void AssertBuilderOwnsDecisionContextInputs(string source, string relativePath, List<string> failures)
    {
        AssertRequiredPresent(source, relativePath, failures, "BattleAiActionRequestBuilder.BuildCommandScopedRequest", "decision context builder should build command-scoped AI requests");
        AssertRequiredPresent(source, relativePath, failures, "BattleTargetSelectionService", "decision context builder should own target candidate scoping calls");
        AssertRequiredPresent(source, relativePath, failures, "LocalCombatSituationBuilder.Build", "decision context builder should build local-combat decision situations");
        AssertRequiredPresent(source, relativePath, failures, "BattleMovementController", "decision context builder should route movement proposal construction through the actor movement controller");
        AssertRequiredPresent(source, relativePath, failures, "ResolveRequestedTarget", "decision context builder should own requested-target resolution for the contexts it builds");
    }

    private static void AssertBuilderDoesNotOwnWorldAuthority(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementCommitResolver", "decision context builder must not commit movement");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAttackResolver", "decision context builder must not resolve attacks");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEffectResolver", "decision context builder must not execute effects");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleCommitBuffer", "decision context builder must not own commit-buffer phases");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleMovementBoundaryCoordinator", "decision context builder must not advance movement boundaries");
        AssertForbiddenAbsent(source, relativePath, failures, "AdvanceAttackRecoveryBoundaries", "decision context builder must not advance attack recovery");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleCombatSlotIntentResolver", "decision context builder must not bypass actor movement controller slot proposal construction");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleCrowdMovementPlanner", "decision context builder must not bypass actor movement controller crowd proposal construction");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAbilityController", "decision context builder must not own ability lifecycle");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeHeroSkillCommandResolver", "decision context builder must not own hero skill commands");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventStream", "decision context builder must not write runtime events");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeEventFactory", "decision context builder must not create runtime events");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventKind.DamageApplied", "decision context builder must not emit damage events");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleEventKind.EffectApplied", "decision context builder must not emit effect events");
        AssertForbiddenAbsent(source, relativePath, failures, ".HitPoints =", "decision context builder must not mutate health");
        AssertForbiddenAbsent(source, relativePath, failures, "MarkDefeated", "decision context builder must not mark defeat");
        AssertForbiddenAbsent(source, relativePath, failures, "CommitDisplacement", "decision context builder must not own spatial displacement");
        AssertForbiddenAbsent(source, relativePath, failures, "MarkMovementCommitted", "decision context builder must not commit actor movement");
        AssertForbiddenAbsent(source, relativePath, failures, "TryReserveMove", "decision context builder must not reserve movement");
        AssertForbiddenAbsent(source, relativePath, failures, ".HasReservedGridCell =", "decision context builder must not mutate reservations");
        AssertForbiddenAbsent(source, relativePath, failures, ".ReservedGrid", "decision context builder must not mutate reservations");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridX =", "decision context builder must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridY =", "decision context builder must not mutate spatial anchors");
        AssertForbiddenAbsent(source, relativePath, failures, ".GridHeight =", "decision context builder must not mutate spatial anchors");
    }

    private static void AssertBuilderDoesNotOwnOutcomeOrExecutorFallback(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "new DefaultBattleRuntimeAiExecutor", "decision context builder should use the injected AI executor instead of creating a default executor");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeActorStateMachine.MarkHolding", "decision context builder must not apply hold outcomes");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleRuntimeActorStateMachine.MarkWaitingForCharge", "decision context builder must not apply wait-for-charge outcomes");
        AssertForbiddenAbsent(source, relativePath, failures, "BattlePlanStateEmitter.SetPlanState", "decision context builder must not emit plan state changes");
        AssertForbiddenAbsent(source, relativePath, failures, ".Result =", "decision context builder must not set action results");
        AssertForbiddenAbsent(source, relativePath, failures, ".TargetActorId =", "decision context builder must not write decision target outcomes");
    }

    private static void AssertResolverDoesNotRegrowDecisionContextBody(string source, string relativePath, List<string> failures)
    {
        AssertForbiddenAbsent(source, relativePath, failures, "BattleAiActionRequestBuilder.BuildCommandScopedRequest", "tick resolver should not build command-scoped AI requests directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BuildDecisionFacts", "tick resolver should not build AI decision facts directly");
        AssertForbiddenAbsent(source, relativePath, failures, ".ChooseAction(", "tick resolver should not choose AI actions directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleTargetSelectionService.Build", "tick resolver should not build target candidate sets directly");
        AssertForbiddenAbsent(source, relativePath, failures, "LocalCombatSituationBuilder.Build", "tick resolver should not build local-combat situations directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleLocalCombatRegionResolver.ResolveRegionMovementGoal", "tick resolver should not resolve region movement goals directly");
        AssertForbiddenAbsent(source, relativePath, failures, "BattleGroupActionZoneResolver.ResolveActorCombatJoinActionZone", "tick resolver should not resolve combat join action zones directly");
        AssertForbiddenAbsent(source, relativePath, failures, "TryBuildAlternateCombatZoneJoinContext", "tick resolver should not own alternate combat-zone join fallback construction");
        AssertForbiddenAbsent(source, relativePath, failures, "IsBlockedLocalCombatHold", "tick resolver should not own blocked local-combat hold classification");
        AssertForbiddenAbsent(source, relativePath, failures, "ResolveRequestedTarget", "tick resolver should not own requested-target resolution");
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
