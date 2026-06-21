using System;
using System.IO;
using System.Linq;

internal static class TargetBattleTickResolverDecompositionGuard
{
    internal static void RuntimeTickResolverStaysSlimAfterTd001Decomposition()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        AssertTrue(resolverFiles.Length > 0, "BattleRuntimeTickResolver source files should exist");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleRuntimeAdvanceDiagnostics.cs")),
            "TD-001 extracted diagnostics file should exist: src/Runtime/Battle/BattleRuntimeAdvanceDiagnostics.cs");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleLocalCombatRegionResolver.cs")),
            "TD-001 extracted local combat region resolver file should exist: src/Runtime/Battle/BattleLocalCombatRegionResolver.cs");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleObjectiveAdvancePlanner.cs")),
            "TD-001 extracted objective advance planner file should exist: src/Runtime/Battle/BattleObjectiveAdvancePlanner.cs");

        int totalResolverLines = resolverFiles.Sum(path => File.ReadAllLines(path).Length);
        AssertTrue(
            totalResolverLines < 900,
            $"TD-001 BattleRuntimeTickResolver*.cs total lines should stay below 900: actual={totalResolverLines}");

        string resolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs");
        string resolverSource = File.ReadAllText(resolverPath);
        int resolveTickLines = CountMethodLines(
            resolverSource,
            "internal void ResolveTick",
            ToRepoPath(root, resolverPath));
        AssertTrue(
            resolveTickLines < 140,
            $"TD-001 ResolveTick method span should stay below 140 lines: actual={resolveTickLines}");

        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);

            // Calls into BattleRuntimeAdvanceDiagnostics stay legal; the local method bodies must not regrow here.
            AssertDoesNotContain(source, "void LogAdvanceFailureDiagnostic(", relativePath);
            AssertDoesNotContain(source, "static void LogAdvanceFailureDiagnostic(", relativePath);
            AssertDoesNotContain(source, "void LogObjectiveAdvanceFailureDiagnostic(", relativePath);
            AssertDoesNotContain(source, "static void LogObjectiveAdvanceFailureDiagnostic(", relativePath);
        }
    }

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
            File.Exists(Path.Combine(battleRuntimePath, "BattleAttackEngagementCoordinator.cs")),
            "H10 attack engagement coordinator service file should exist: src/Runtime/Battle/BattleAttackEngagementCoordinator.cs");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleMovementCommitResolver.cs")),
            "TD-003 movement resolver service file should exist: src/Runtime/Battle/BattleMovementCommitResolver.cs");
        AssertTrue(
            File.Exists(Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs")),
            "H22 resolution phase coordinator service file should exist: src/Runtime/Battle/BattleRuntimeResolutionPhaseCoordinator.cs");

        string combinedResolverSource = "";
        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            combinedResolverSource += source;

            AssertDoesNotContain(source, "void ResolveAttackProposals(", relativePath);
            AssertDoesNotContain(source, "ResolveAttackProposalsAndEngagementTriggers", relativePath);
            AssertDoesNotContain(source, "int ResolveMovementProposals(", relativePath);
            AssertDoesNotContain(source, "MarkMovementCommitted", relativePath);
            AssertDoesNotContain(source, "MarkAttackRecovery", relativePath);
        }

        AssertTrue(
            combinedResolverSource.Contains("BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should enter attack and movement resolution through BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase");

        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string resolutionPhaseCoordinatorSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
        AssertTrue(
            resolutionPhaseCoordinatorSource.Contains("BattleAttackEngagementCoordinator.Resolve(", StringComparison.Ordinal),
            "BattleRuntimeResolutionPhaseCoordinator should delegate attack engagement coordination to BattleAttackEngagementCoordinator.Resolve(");
        AssertTrue(
            resolutionPhaseCoordinatorSource.Contains("BattleMovementCommitResolver.Resolve(", StringComparison.Ordinal),
            "BattleRuntimeResolutionPhaseCoordinator should delegate movement resolution to BattleMovementCommitResolver.Resolve(");

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
        AssertServiceFileExists(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");

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
            combinedResolverSource.Contains("BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", StringComparison.Ordinal),
            "BattleRuntimeTickResolver*.cs should enter tick-start tactical observation through BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase");

        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string decisionPhaseCoordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
        AssertTrue(
            decisionPhaseCoordinatorSource.Contains("BattleTacticalObservationUpdater.RefreshAtTickStart", StringComparison.Ordinal),
            "BattleRuntimeDecisionPhaseCoordinator should delegate tick-start tactical observation to BattleTacticalObservationUpdater.RefreshAtTickStart");

        string attackEngagementCoordinatorPath = Path.Combine(battleRuntimePath, "BattleAttackEngagementCoordinator.cs");
        string attackEngagementCoordinatorSource = File.ReadAllText(attackEngagementCoordinatorPath);
        AssertTrue(
            attackEngagementCoordinatorSource.Contains("BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers", StringComparison.Ordinal),
            "BattleAttackEngagementCoordinator should delegate post-attack engagement triggers to BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers");

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

    private static int CountMethodLines(string source, string methodSignature, string relativePath)
    {
        int methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
        AssertTrue(methodStart >= 0, $"method signature not found: file={relativePath} signature={methodSignature}");

        int openingBrace = source.IndexOf('{', methodStart);
        AssertTrue(openingBrace >= 0, $"method opening brace not found: file={relativePath} signature={methodSignature}");

        int closingBrace = FindMatchingBrace(source, openingBrace);
        AssertTrue(closingBrace >= 0, $"method closing brace not found: file={relativePath} signature={methodSignature}");

        int lines = 1;
        for (int index = methodStart; index <= closingBrace; index++)
        {
            if (source[index] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        int depth = 0;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int index = openingBrace; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            char afterNext = index + 2 < source.Length ? source[index + 2] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }

                continue;
            }

            if (inString)
            {
                if (!inVerbatimString && current == '\\')
                {
                    index++;
                    continue;
                }

                if (current == '"')
                {
                    if (inVerbatimString && next == '"')
                    {
                        index++;
                        continue;
                    }

                    inString = false;
                    inVerbatimString = false;
                }

                continue;
            }

            if (inChar)
            {
                if (current == '\\')
                {
                    index++;
                    continue;
                }

                if (current == '\'')
                {
                    inChar = false;
                }

                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
                continue;
            }

            if (current == '@' && next == '"')
            {
                inString = true;
                inVerbatimString = true;
                index++;
                continue;
            }

            if (current == '$' && next == '@' && afterNext == '"')
            {
                inString = true;
                inVerbatimString = true;
                index += 2;
                continue;
            }

            if (current == '@' && next == '$' && afterNext == '"')
            {
                inString = true;
                inVerbatimString = true;
                index += 2;
                continue;
            }

            if (current == '$' && next == '"')
            {
                inString = true;
                index++;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '\'')
            {
                inChar = true;
                continue;
            }

            if (current == '{')
            {
                depth++;
                continue;
            }

            if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
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
