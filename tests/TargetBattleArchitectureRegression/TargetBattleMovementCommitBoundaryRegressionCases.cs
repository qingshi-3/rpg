using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class TargetBattleMovementCommitBoundaryRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime accepted movement commit application is boundary owned", RuntimeAcceptedMovementCommitApplicationIsBoundaryOwned);
    }

    private static void RuntimeAcceptedMovementCommitApplicationIsBoundaryOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string boundaryPath = Path.Combine(battleRuntimePath, "BattleMovementCommitBoundary.cs");
        string resolverPath = Path.Combine(battleRuntimePath, "BattleMovementCommitResolver.cs");
        var failures = new List<string>();

        if (!File.Exists(boundaryPath))
        {
            failures.Add("BattleMovementCommitBoundary source file should exist: src/Runtime/Battle/BattleMovementCommitBoundary.cs");
        }
        else
        {
            string boundarySource = File.ReadAllText(boundaryPath);
            string relativePath = ToRepoPath(root, boundaryPath);
            AssertRequiredPresent(boundarySource, relativePath, failures, "class BattleMovementCommitBoundary", "movement commit boundary should define the service class");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ApplyAcceptedMove", "movement commit boundary should expose the accepted-move apply entry");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ArgumentNullException.ThrowIfNull(context)", "movement commit boundary should fail fast on a missing context before mutation");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ArgumentNullException.ThrowIfNull(stream)", "movement commit boundary should fail fast on a missing stream before mutation");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ArgumentNullException.ThrowIfNull(actor)", "movement commit boundary should fail fast on a missing actor before mutation");
            AssertRequiredPresent(boundarySource, relativePath, failures, "HasReservedGridCell = true", "movement commit boundary should own accepted reservation state writes");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ReservedGridX =", "movement commit boundary should own accepted reservation X writes");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ReservedGridY =", "movement commit boundary should own accepted reservation Y writes");
            AssertRequiredPresent(boundarySource, relativePath, failures, "ReservedGridHeight =", "movement commit boundary should own accepted reservation height writes");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattlePlanStateEmitter.SetPlanState", "actor movement commit boundary must not emit commander plan-state directly");
            AssertRequiredPresent(boundarySource, relativePath, failures, "BattleRuntimeActorStateMachine.MarkMovementCommitted", "movement commit boundary should own accepted movement phase transition");
            AssertRequiredPresent(boundarySource, relativePath, failures, "BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState", "movement commit boundary should reset advance-failure state after accepted movement");
            AssertRequiredPresent(boundarySource, relativePath, failures, "BattleRuntimeAiActionResult.Succeeded", "movement commit boundary should mark the accepted context successful");
            AssertRequiredPresent(boundarySource, relativePath, failures, "BattleRuntimeEventFactory.CreateMovementEvent", "movement commit boundary should create movement-start events");
            AssertRequiredPresent(boundarySource, relativePath, failures, "BattleEventKind.MovementStarted", "movement commit boundary should emit movement-start event semantics");
            AssertRequiredPresent(boundarySource, relativePath, failures, "RecordMovementEvent", "movement commit boundary should keep movement performance event recording with event emission");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "new BattleMovementReservationMap", "movement commit boundary must not own same-tick reservation maps");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "TryReserveMove", "movement commit boundary must not own reservation attempts");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattleRuntimeDecisionContextBuilder.Build", "movement commit boundary must not rebuild decision contexts");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "TryRetargetStaleAdvanceContextCallback", "movement commit boundary must not own stale-target retarget callbacks");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattleObjectiveAdvancePlanner", "movement commit boundary must not depend on objective movement planners for event payloads");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattleMovementController", "movement commit boundary must not route back into actor movement proposal construction");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattleAttackResolver", "movement commit boundary must not resolve attacks");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "BattleEffectResolver", "movement commit boundary must not execute effects");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, ".HitPoints =", "movement commit boundary must not mutate health");
            AssertForbiddenAbsent(boundarySource, relativePath, failures, "MarkDefeated", "movement commit boundary must not mark defeat");
        }

        string resolverSource = File.Exists(resolverPath) ? File.ReadAllText(resolverPath) : "";
        string resolverRelativePath = ToRepoPath(root, resolverPath);
        AssertRequiredPresent(resolverSource, resolverRelativePath, failures, "BattleMovementCommitBoundary.ApplyAcceptedMove", "movement commit resolver should delegate accepted movement application to the boundary");
        AssertRequiredPresent(resolverSource, resolverRelativePath, failures, "BattleMovementReservationMap reservations", "movement commit resolver should retain same-tick reservation map ownership");
        AssertRequiredPresent(resolverSource, resolverRelativePath, failures, "TryReserveMove", "movement commit resolver should retain reservation attempt ownership");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "HasReservedGridCell = true", "movement commit resolver should not write accepted reservation state directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "ReservedGridX =", "movement commit resolver should not write accepted reservation X directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "ReservedGridY =", "movement commit resolver should not write accepted reservation Y directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "ReservedGridHeight =", "movement commit resolver should not write accepted reservation height directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattlePlanStateEmitter.SetPlanState", "movement commit resolver should not emit accepted movement plan-state directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattleRuntimeCombatSlotIntent", "movement commit resolver should not own accepted movement combat-slot trace logging");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "GameLog.Trace", "movement commit resolver should not own accepted movement trace logging");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattleRuntimeActorStateMachine.MarkMovementCommitted", "movement commit resolver should not own accepted movement phase transition");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattleRuntimeEventFactory.CreateMovementEvent", "movement commit resolver should not create movement-start events directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattleEventKind.MovementStarted", "movement commit resolver should not own movement-start event semantics");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "BattleRuntimeAiActionResult.Succeeded", "movement commit resolver should not mark accepted movement success directly");
        AssertForbiddenAbsent(resolverSource, resolverRelativePath, failures, "RecordMovementEvent", "movement commit resolver should not record accepted movement events directly");

        string commanderCoordinatorPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleGroupCommanderTransitionCoordinator.cs");
        string commanderCoordinatorSource = File.Exists(commanderCoordinatorPath) ? File.ReadAllText(commanderCoordinatorPath) : "";
        AssertRequiredPresent(commanderCoordinatorSource, ToRepoPath(root, commanderCoordinatorPath), failures, "BattlePlanStateEmitter.SetPlanState", "group commander coordinator should own accepted execution plan-state emission");

        AssertTrue(
            failures.Count == 0,
            "Core Slice H13 movement commit boundary guard failed: " + string.Join("; ", failures));
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
