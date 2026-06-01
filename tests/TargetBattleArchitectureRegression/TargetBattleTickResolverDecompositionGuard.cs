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

        AssertRuntimeTickResolverDelegatesTacticalObservationToUpdater(root, battleRuntimePath, resolverFiles);
    }

    private static void AssertRuntimeTickResolverDelegatesTacticalObservationToUpdater(
        string root,
        string battleRuntimePath,
        string[] resolverFiles)
    {
        AssertTrue(resolverFiles.Length > 0, "BattleRuntimeTickResolver source files should exist");
        AssertServiceFileExists(battleRuntimePath, "BattleTargetSelectionService.cs");
        AssertServiceFileExists(battleRuntimePath, "BattleAiActionRequestBuilder.cs");
        AssertServiceFileExists(battleRuntimePath, "BattleTacticalObservationUpdater.cs");
        AssertServiceFileExists(battleRuntimePath, "BattleTargetLockLifecycle.cs");

        string combinedResolverSource = "";
        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            combinedResolverSource += source;

            AssertDoesNotContain(source, "BattleGroupPerceptionSummaryBuilder", relativePath);
            AssertDoesNotContain(source, "BattleGroupEngagementStateMachine", relativePath);
            AssertDoesNotContain(source, "BattleLocalCombatRegionBuilder", relativePath);
            AssertDoesNotContain(source, "BattleTemporaryTargetRegionBuilder", relativePath);

            AssertDoesNotContain(source, "CaptureLivingCorpsAndRefreshPerceptionSummaries", relativePath);
            AssertDoesNotContain(source, "RefreshEngagedLocalCombatRegions", relativePath);
            AssertDoesNotContain(source, "RefreshEnemyTemporaryTargetRegions", relativePath);
            AssertDoesNotContain(source, "ClearTargetLocksForEngagementExits", relativePath);
            AssertDoesNotContain(source, "BuildCommandScopedAiActionRequest", relativePath);

            // New-name definition signatures allow legal service calls while
            // blocking equivalent method bodies from growing back in resolver partials.
            AssertDoesNotContain(source, "BattleRuntimeActor[] RefreshAtTickStart(", relativePath);
            AssertDoesNotContain(source, "void ApplyPostAttackEngagementTriggers(", relativePath);
            AssertDoesNotContain(source, "void ClearForEngagementExits(", relativePath);
            AssertDoesNotContain(source, "BattleRuntimeAiActionRequest BuildCommandScopedRequest(", relativePath);

            // The resolver may call BattleTargetSelectionService.FindEnemyCorpsForCommand(...);
            // this guard locks the extracted method body out of resolver partials.
            AssertDoesNotContain(source, "BattleRuntimeTickStartActorFact? FindEnemyCorpsForCommand(", relativePath);
            AssertDoesNotContain(source, "static BattleRuntimeTickStartActorFact? FindEnemyCorpsForCommand(", relativePath);

            AssertDoesNotContain(source, "EngagementExitNoGroupPerception", relativePath);
            AssertDoesNotContain(source, "ClearForEngagementExits(", relativePath);
        }

        AssertTrue(
            combinedResolverSource.Contains("BattleTacticalObservationUpdater.RefreshAtTickStart", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should delegate tick-start tactical observation to BattleTacticalObservationUpdater.RefreshAtTickStart");
        AssertTrue(
            combinedResolverSource.Contains("BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should delegate post-attack engagement triggers to BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers");

        string engagementStateMachinePath = Path.Combine(battleRuntimePath, "Tactics", "BattleGroupEngagementStateMachine.cs");
        string engagementStateMachineSource = File.ReadAllText(engagementStateMachinePath);
        AssertDoesNotContain(engagementStateMachineSource, "TargetActorId", ToRepoPath(root, engagementStateMachinePath));

        string lifecyclePath = Path.Combine(battleRuntimePath, "BattleTargetLockLifecycle.cs");
        string lifecycleSource = File.ReadAllText(lifecyclePath);
        AssertTrue(
            lifecycleSource.Contains("ClearForEngagementExits(", StringComparison.Ordinal),
            "BattleTargetLockLifecycle should define ClearForEngagementExits(");
        AssertTrue(
            lifecycleSource.Contains("EngagementExitNoGroupPerception", StringComparison.Ordinal) &&
            lifecycleSource.Contains("actor.TargetActorId = \"\";", StringComparison.Ordinal),
            "BattleTargetLockLifecycle should own engagement-exit target-lock clearing");
    }

    private static void AssertServiceFileExists(string battleRuntimePath, string fileName)
    {
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, fileName)),
            $"TD-002 extracted service file should exist: src/Runtime/Battle/{fileName}");
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
