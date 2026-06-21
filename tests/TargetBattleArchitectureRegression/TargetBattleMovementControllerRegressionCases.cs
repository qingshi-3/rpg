using System;
using System.IO;

internal static class TargetBattleMovementControllerRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime movement controller and actor movement shell are authored", RuntimeMovementControllerAndActorMovementShellAreAuthored);
        run("runtime tick resolver delegates movement phase ownership to actor controller", RuntimeTickResolverDelegatesMovementPhaseOwnershipToActorController);
        run("runtime movement controller preserves spatial commit authority boundaries", RuntimeMovementControllerPreservesSpatialCommitAuthorityBoundaries);
        run("runtime movement controller owns objective region proposal boundary", RuntimeMovementControllerOwnsObjectiveRegionProposalBoundary);
        run("runtime movement controller owns target local combat proposal boundary", RuntimeMovementControllerOwnsTargetLocalCombatProposalBoundary);
        run("runtime movement helpers do not depend on tick resolver factories", RuntimeMovementHelpersDoNotDependOnTickResolverFactories);
        run("runtime movement continuation policy is controller owned", RuntimeMovementContinuationPolicyIsControllerOwned);
        run("runtime movement continuation has one batch entry", RuntimeMovementContinuationHasOneBatchEntry);
        run("runtime movement cleanup is controller owned", RuntimeMovementCleanupIsControllerOwned);
    }

    private static void RuntimeMovementControllerAndActorMovementShellAreAuthored()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleMovementController.cs");
        string actorRuntimePath = Path.Combine(battleRuntimePath, "BattleActorRuntime.cs");

        AssertTrue(File.Exists(controllerPath), "Core Slice G should author BattleMovementController");
        string controllerSource = ReadMovementControllerSource(root);
        AssertContains(controllerSource, "class BattleMovementController", "BattleMovementController*.cs", "BattleMovementController should be a runtime class");
        AssertContains(controllerSource, "AdvanceMovementBoundary", "BattleMovementController*.cs", "movement controller should own moving-phase boundary advancement");
        AssertContains(controllerSource, "BuildContinuationContext", "BattleMovementController*.cs", "movement controller should own same-intent continuation context construction");
        AssertContains(controllerSource, "ClearEndedMovementChain", "BattleMovementController*.cs", "movement controller should own movement intent cleanup");

        string actorRuntimeSource = File.ReadAllText(actorRuntimePath);
        AssertContains(actorRuntimeSource, "BattleMovementController", ToRepoPath(root, actorRuntimePath), "BattleActorRuntime should hold or expose BattleMovementController");
        AssertContains(actorRuntimeSource, "MovementController", ToRepoPath(root, actorRuntimePath), "BattleActorRuntime should expose the actor movement controller by intent");
    }

    private static void RuntimeTickResolverDelegatesMovementPhaseOwnershipToActorController()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string[] resolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string combinedSource = string.Join(Environment.NewLine, resolverFiles.Select(File.ReadAllText));
        string runtimePhaseCoordinatorSource = ReadRuntimePhaseCoordinatorSource(root);

        AssertContains(combinedSource, "BattleRuntimeActionPhaseCoordinator.AdvanceActionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter actor movement boundary handling through the action phase coordinator");
        AssertContains(combinedSource, "BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter movement continuation construction through the decision phase coordinator");
        AssertContains(combinedSource, "BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter movement cleanup through the resolution phase coordinator");
        AssertContains(runtimePhaseCoordinatorSource, "MovementController", "BattleRuntime*PhaseCoordinator.cs", "runtime phase coordinators should call actor-local movement controller for movement phases");
        foreach (string resolverFile in resolverFiles)
        {
            string source = File.ReadAllText(resolverFile);
            string relativePath = ToRepoPath(root, resolverFile);
            AssertDoesNotContain(source, "private BattleDynamicOccupancy AdvanceMovementBoundaries", relativePath, "tick resolver should not define moving-phase boundary advancement");
            AssertDoesNotContain(source, "BattleMovementContinuationPlanner.BuildContinuationContexts", relativePath, "tick resolver should not call continuation planner directly");
            AssertDoesNotContain(source, "BattleMovementContinuationPlanner.ClearEndedMovementChains", relativePath, "tick resolver should not clear actor movement chains directly");
        }
    }

    private static void RuntimeMovementControllerPreservesSpatialCommitAuthorityBoundaries()
    {
        string root = ProjectRoot();
        string controllerPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleMovementController.cs");
        string source = ReadMovementControllerSource(root);
        string relativePath = "BattleMovementController*.cs";

        AssertDoesNotContain(source, "new BattleMovementReservationMap", relativePath, "movement controller should not own same-tick reservation maps");
        AssertDoesNotContain(source, "TryReserveMove", relativePath, "movement controller should not reserve movement directly");
        AssertDoesNotContain(source, "BattlePlanStateEmitter.SetPlanState", relativePath, "movement controller should not emit plan-state movement events");
        AssertDoesNotContain(source, "BattleDynamicOccupancy.FromActors", relativePath, "movement controller should not build world-owned occupancy snapshots");
        AssertDoesNotContain(source, "BattleEventKind.MovementStarted", relativePath, "movement controller should not emit movement-start events");
        AssertDoesNotContain(source, "BattleEventKind.MovementCompleted", relativePath, "movement controller should not emit movement-complete events");
        AssertDoesNotContain(source, "stream.Add", relativePath, "movement controller should not write event stream directly");
        AssertDoesNotContain(source, ".GridX =", relativePath, "movement controller should not directly mutate spatial anchors");
        AssertDoesNotContain(source, ".GridY =", relativePath, "movement controller should not directly mutate spatial anchors");
        AssertDoesNotContain(source, ".GridHeight =", relativePath, "movement controller should not directly mutate spatial anchors");
        AssertDoesNotContain(source, "HasReservedGridCell", relativePath, "movement controller should not own reservation state writes");
        AssertDoesNotContain(source, "BattleMovementCommitResolver", relativePath, "movement controller should not own movement commit resolution");
        AssertDoesNotContain(source, "BattleRuntimeEventFactory.CreateMovementEvent", relativePath, "movement controller should not author movement events");
        AssertDoesNotContain(source, "MarkMovementCommitted", relativePath, "movement controller should not commit movement");
        AssertDoesNotContain(source, "ReservedGridX =", relativePath, "movement controller should not write reservation state");
        AssertDoesNotContain(source, "ReservedGridY =", relativePath, "movement controller should not write reservation state");
        AssertDoesNotContain(source, "ReservedGridHeight =", relativePath, "movement controller should not write reservation state");
    }

    private static void RuntimeMovementControllerOwnsObjectiveRegionProposalBoundary()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleMovementController.cs");
        string controllerSource = ReadMovementControllerSource(root);

        AssertContains(controllerSource, "BuildMovementProposalContext", "BattleMovementController*.cs", "movement controller should expose the resolved request-to-proposal boundary");
        AssertContains(controllerSource, "request.Request.ActorId", "BattleMovementController*.cs", "movement controller should fail fast when a movement proposal request actor id does not match its actor");
        AssertContains(controllerSource, "BattleMovementProposalBuildRequest", "BattleMovementController*.cs", "movement proposal build request should make selected intent explicit");
        AssertContains(controllerSource, "BattleMovementProposalWorldInputs", "BattleMovementController*.cs", "movement proposal world inputs should keep topology and occupancy explicit");

        foreach (string runtimeFile in Directory.GetFiles(battleRuntimePath, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            string fileName = Path.GetFileName(runtimeFile);
            if (string.Equals(runtimeFile, controllerPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "BattleCombatJoinRegionPlanner.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertDoesNotContain(source, "BattleObjectiveAdvancePlanner.BuildObjectiveAdvanceContext", relativePath, "runtime movement code should route objective movement proposals through the actor movement controller");
            AssertDoesNotContain(source, "BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext", relativePath, "runtime movement code should route region movement proposals through the actor movement controller");
            AssertDoesNotContain(source, "BattleCombatJoinRegionPlanner.TryBuildOutsiderAdvanceContext", relativePath, "runtime movement code should route outsider region movement proposals through the actor movement controller");
            AssertDoesNotContain(source, "BattleCombatJoinRegionPlanner.TryBuildPressureAdvanceContext", relativePath, "runtime movement code should route pressure region movement proposals through the actor movement controller");
        }
        AssertContains(controllerSource, "BuildContinuationContext", "BattleMovementController*.cs", "movement controller should own objective/region continuation rebuilds");
        AssertNoContinuationPlannerToken(root, "BattleObjectiveAdvancePlanner.BuildObjectiveAdvanceContext", "movement continuation planner should not build objective proposals directly");
        AssertNoContinuationPlannerToken(root, "BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext", "movement continuation planner should not build region proposals directly");
        AssertNoContinuationPlannerToken(root, "BattleCombatJoinRegionPlanner.TryBuildOutsiderAdvanceContext", "movement continuation planner should not own outsider region proposal rebuilds");
        AssertNoContinuationPlannerToken(root, "BattleCombatJoinRegionPlanner.TryBuildPressureAdvanceContext", "movement continuation planner should not own pressure region proposal rebuilds");
    }

    private static void RuntimeMovementControllerOwnsTargetLocalCombatProposalBoundary()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleMovementController.cs");
        string controllerSource = ReadMovementControllerSource(root);

        AssertContains(controllerSource, "BuildTargetMovementProposalContext", "BattleMovementController*.cs", "movement controller should expose target/local-combat movement proposal construction");
        AssertContains(controllerSource, "BattleTargetMovementProposalBuildRequest", "BattleMovementController*.cs", "target/local-combat proposal build request should make selected target context explicit");
        AssertDoesNotContain(controllerSource, "IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> Facts", "BattleMovementController*.cs", "target/local-combat movement proposal requests should not carry the full tick-start fact map into the actor movement controller");
        AssertDoesNotContain(controllerSource, "request.Facts", "BattleMovementController*.cs", "movement controller should consume caller-resolved target/local-combat context instead of global facts");
        AssertDoesNotContain(controllerSource, "BattleTargetSelectionService.IsTargetEngagedBySameFactionActor", "BattleMovementController*.cs", "target engagement context should be resolved before crossing into the movement controller");
        AssertContains(controllerSource, "request.Request.TargetActorId", "BattleMovementController*.cs", "movement controller should fail fast when a target proposal request target id does not match its target fact");

        foreach (string runtimeFile in new[]
        {
            Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs")
        })
        {
            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertDoesNotContain(source, "BattleCombatSlotIntentResolver.TrySelectExecutableIntent", relativePath, "resolver/continuation should route local-combat slot proposal construction through the actor movement controller");
            AssertDoesNotContain(source, "BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget", relativePath, "resolver/continuation should route target movement proposal construction through the actor movement controller");
            AssertDoesNotContain(source, "BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot", relativePath, "resolver/continuation should route stored combat-slot movement proposal construction through the actor movement controller");
        }
    }

    private static void RuntimeMovementHelpersDoNotDependOnTickResolverFactories()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string contextFactoryPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickContextFactory.cs");
        string combatGeometryPath = Path.Combine(battleRuntimePath, "BattleCombatGeometry.cs");
        string tickResolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs");

        AssertTrue(File.Exists(contextFactoryPath), "movement context construction should live outside the center tick resolver");
        AssertTrue(File.Exists(combatGeometryPath), "combat geometry helpers should live outside the center tick resolver");
        string tickResolverSource = File.ReadAllText(tickResolverPath);
        AssertDoesNotContain(tickResolverSource, "CreateContext(", ToRepoPath(root, tickResolverPath), "center tick resolver should not define the shared runtime tick context factory");
        AssertDoesNotContain(tickResolverSource, "GetOrthogonalAttackGap(", ToRepoPath(root, tickResolverPath), "center tick resolver should not define shared combat geometry helpers");

        foreach (string runtimeFile in Directory.GetFiles(battleRuntimePath, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
        {
            string fileName = Path.GetFileName(runtimeFile);
            if (string.Equals(fileName, "BattleRuntimeTickResolver.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.CreateContext", relativePath, "runtime movement helpers should use BattleRuntimeTickContextFactory instead of the center tick resolver context factory");
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.GetOrthogonalAttackGap", relativePath, "runtime movement helpers should use BattleCombatGeometry instead of the center tick resolver attack-gap helper");
        }
    }

    private static void RuntimeMovementContinuationPolicyIsControllerOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleMovementController.cs");
        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string tickResolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs");
        string controllerSource = ReadMovementControllerSource(root);
        string decisionPhaseCoordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
        string resolutionPhaseCoordinatorSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
        string tickResolverSource = File.ReadAllText(tickResolverPath);

        AssertContains(controllerSource, "BuildContinuationContext", "BattleMovementController*.cs", "movement controller should expose the single-actor continuation primitive");
        AssertContains(controllerSource, "CommandStillMatches", "BattleMovementController*.cs", "movement controller should own same-intent command validation");
        AssertContains(controllerSource, "HasMovementIntentSnapshot", "BattleMovementController*.cs", "movement controller should own movement-intent snapshot continuation eligibility");
        AssertContains(controllerSource, "BattleTargetSelectionService.FindImmediateAttackOpportunityEnemyCorps", "BattleMovementController*.cs", "movement controller should stop continuation when immediate attack is available");
        AssertContains(controllerSource, "BattleObjectiveAdvancePlanner.IsObjectiveReached", "BattleMovementController*.cs", "movement controller should stop objective continuation at objective boundary");
        AssertContains(controllerSource, "RequiresLocalCombatSituation", "BattleMovementController*.cs", "movement controller should own local-combat continuation scope validation");
        AssertContains(decisionPhaseCoordinatorSource, "BattleMovementController.BuildContinuationContexts", ToRepoPath(root, decisionPhaseCoordinatorPath), "decision phase coordinator should enter movement continuation through the actor movement controller");
        AssertContains(resolutionPhaseCoordinatorSource, "BattleMovementController.ClearEndedMovementChains", ToRepoPath(root, resolutionPhaseCoordinatorPath), "resolution phase coordinator should clear ended movement chains through the actor movement controller");
        AssertDoesNotContain(controllerSource, "BattleMovementContinuationPlanner.TryBuildContinuationContext", "BattleMovementController*.cs", "movement controller should not delegate actor-local continuation policy to the continuation planner");
        AssertDoesNotContain(controllerSource, "BattleMovementContinuationPlanner.BuildContinuationContexts", "BattleMovementController*.cs", "movement controller should not re-enter the continuation planner batch wrapper for one actor");
        AssertDoesNotContain(controllerSource, "BattleMovementContinuationPlanner.ClearEndedMovementChains", "BattleMovementController*.cs", "movement controller should not re-enter the continuation planner batch cleanup wrapper for one actor");

        foreach (string policyToken in new[]
        {
            "CommandStillMatches",
            "ReachedObjectiveBoundary",
            "ReasonStillMatches",
            "HasLocallyPerceivedHostile",
            "IsRegionReached",
            "RequiresLocalCombatSituation",
            "CreateTargetRequest"
        })
        {
            AssertNoContinuationPlannerToken(root, policyToken, "continuation planner should not own actor-local continuation policy");
        }

        foreach (string runtimeFile in Directory.GetFiles(battleRuntimePath, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (string.Equals(runtimeFile, controllerPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(runtimeFile), "BattleMovementContinuationPlanner.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertDoesNotContain(source, "BattleMovementContinuationPlanner.TryBuildContinuationContext", relativePath, "runtime callers should reach the single-actor continuation primitive only through BattleMovementController");
            AssertDoesNotContain(source, "BattleMovementContinuationPlanner.ClearEndedMovementChain(", relativePath, "runtime callers should reach the single-actor cleanup primitive only through BattleMovementController");
        }
    }

    private static void RuntimeMovementContinuationHasOneBatchEntry()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string decisionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeDecisionPhaseCoordinator.cs");
        string resolutionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeResolutionPhaseCoordinator.cs");
        string tickResolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs");
        string controllerSource = ReadMovementControllerSource(root);
        string decisionPhaseCoordinatorSource = File.ReadAllText(decisionPhaseCoordinatorPath);
        string resolutionPhaseCoordinatorSource = File.ReadAllText(resolutionPhaseCoordinatorPath);
        string tickResolverSource = File.ReadAllText(tickResolverPath);

        AssertContains(controllerSource, "BuildContinuationContexts(", "BattleMovementController*.cs", "movement controller should own the only runtime batch continuation entry");
        AssertContains(controllerSource, "ClearEndedMovementChains(", "BattleMovementController*.cs", "movement controller should own the only runtime batch cleanup entry");
        AssertContains(decisionPhaseCoordinatorSource, "BattleMovementController.BuildContinuationContexts", ToRepoPath(root, decisionPhaseCoordinatorPath), "decision phase coordinator should enter continuation through the movement controller");
        AssertContains(resolutionPhaseCoordinatorSource, "BattleMovementController.ClearEndedMovementChains", ToRepoPath(root, resolutionPhaseCoordinatorPath), "resolution phase coordinator should clear ended movement chains through the movement controller");
        AssertNoContinuationPlannerToken(root, "BuildContinuationContexts(", "continuation planner should not expose a second batch continuation entry");
        AssertNoContinuationPlannerToken(root, "ClearEndedMovementChains(", "continuation planner should not expose a second batch cleanup entry");
    }

    private static void RuntimeMovementCleanupIsControllerOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerSource = ReadMovementControllerSource(root);

        AssertContains(controllerSource, "BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot", "BattleMovementController*.cs", "movement controller should own ended movement-chain cleanup");
        AssertContains(controllerSource, "MovementSteeringBudgetRemaining <= 0", "BattleMovementController*.cs", "movement controller should preserve exhausted obstacle-follow steering");
        AssertContains(controllerSource, "actor.Phase == BattleRuntimeActorPhase.Moving && actor.HasMovementTarget", "BattleMovementController*.cs", "movement controller should not clear a still-moving actor with an active target");
        AssertDoesNotContain(controllerSource, "BattleMovementContinuationPlanner.ClearEndedMovementChain", "BattleMovementController*.cs", "movement controller should not delegate actor-local cleanup to the continuation planner");
        AssertNoContinuationPlannerToken(root, "ClearEndedMovementChain(", "continuation planner should not own actor movement-state cleanup");
        AssertNoContinuationPlannerToken(root, "ClearMovementIntentSnapshot", "continuation planner should not clear actor movement intent state");
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

    private static string ReadMovementControllerSource(string root)
    {
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(battleRuntimePath, "BattleMovementController*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadRuntimePhaseCoordinatorSource(string root)
    {
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(battleRuntimePath, "BattleRuntime*PhaseCoordinator.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static void AssertNoContinuationPlannerToken(string root, string forbidden, string message)
    {
        string path = Path.Combine(root, "src", "Runtime", "Battle", "BattleMovementContinuationPlanner.cs");
        if (!File.Exists(path))
        {
            return;
        }

        AssertDoesNotContain(File.ReadAllText(path), forbidden, ToRepoPath(root, path), message);
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
