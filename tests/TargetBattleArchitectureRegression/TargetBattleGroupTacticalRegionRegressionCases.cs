using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleGroupTacticalRegionRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        TargetBattleLayeredRuntimeRegressionCases.Register(run);
        run("missing owner region is rejected with reason", MissingOwnerRegionIsRejectedWithReason);
        run("null region is rejected with reason", NullRegionIsRejectedWithReason);
        run("owner mismatch region is rejected with reason", OwnerMismatchRegionIsRejectedWithReason);
        run("invalid size region is rejected with reason", InvalidSizeRegionIsRejectedWithReason);
        run("player commanded groups reject enemy policy overwrite", PlayerCommandedGroupsRejectEnemyPolicyOverwrite);
        run("player commanded engagement cannot become enemy active assault", PlayerCommandedEngagementCannotBecomeEnemyActiveAssault);
        run("global snapshot cache is read only", GlobalSnapshotCacheIsReadOnly);
        run("tactical mutation events include context and unique ids", TacticalMutationEventsIncludeContextAndUniqueIds);
        run("invalid initial tactical seed is captured", InvalidInitialTacticalSeedIsCaptured);
        run("multiple initial tactical seeds are all captured", MultipleInitialTacticalSeedsAreAllCaptured);
        run("duplicate tactical seed group is rejected", DuplicateTacticalSeedGroupIsRejected);
        run("runtime initializes one tactical state per battle group", RuntimeInitializesOneTacticalStatePerBattleGroup);
        run("group perception summary uses alive members and perceived hostiles", GroupPerceptionSummaryUsesAliveMembersAndPerceivedHostiles);
        run("runtime builds group perception summaries", RuntimeBuildsGroupPerceptionSummaries);
        TargetBattleGroupEngagementRegressionCases.Register(run);
        run("runtime state does not expose tactical store publicly", RuntimeStateDoesNotExposeTacticalStorePublicly);
        run("battle entry seeds enemy offense fixed region by player density", BattleEntrySeedsEnemyOffenseFixedRegionByPlayerDensity);
        run("battle entry seeds enemy active defense from player offensive region", BattleEntrySeedsEnemyActiveDefenseFromPlayerOffensiveRegion);
        run("battle entry seeds enemy hold defense from hold posture", BattleEntrySeedsEnemyHoldDefenseFromHoldPosture);
        run("battle entry fixed region uses priority when player density ties", BattleEntryFixedRegionUsesPriorityWhenPlayerDensityTies);
        run("battle entry fixed region uses distance when density and priority tie", BattleEntryFixedRegionUsesDistanceWhenDensityAndPriorityTie);
        run("battle entry fixed region uses lexicographic id when score and distance tie", BattleEntryFixedRegionUsesLexicographicIdWhenScoreAndDistanceTie);
        run("battle entry no fixed candidate seeds mode without region", BattleEntryNoFixedCandidateSeedsModeWithoutRegion);
        run("battle entry preserves preauthored player tactical seed", BattleEntryPreservesPreauthoredPlayerTacticalSeed);
        run("enemy region movement ignores moving unit outside perception", EnemyRegionMovementIgnoresMovingUnitOutsidePerception);
        run("player objective movement still uses player plan", PlayerObjectiveMovementStillUsesPlayerPlan);
        TargetBattleTemporaryRegionRegressionCases.Register(run);
    }

    public static void MissingOwnerRegionIsRejectedWithReason()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense)
        });

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "enemy_group",
            BuildRegion("region_missing_owner", ownerBattleGroupId: ""),
            isEnemyPolicyMutation: true);

        AssertFalse(result.Accepted, "missing owner mutation should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedMissingOwner, result.ReasonCode, "missing owner reason");
        AssertEqual(BattleEventKind.BattleGroupTacticalRegionRejected, result.Event.Kind, "missing owner event kind");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedMissingOwner, result.Event.ReasonCode, "missing owner event reason");
        AssertTrue(store.GetRequiredSnapshot("enemy_group").SelectedRegion == null, "rejected region must not become store truth");
    }

    public static void NullRegionIsRejectedWithReason()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense)
        });

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "enemy_group",
            null,
            isEnemyPolicyMutation: true);

        AssertFalse(result.Accepted, "null region mutation should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedInvalidRegion, result.ReasonCode, "null region reason");
        AssertEqual(BattleEventKind.BattleGroupTacticalRegionRejected, result.Event.Kind, "null region event kind");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedInvalidRegion, result.Event.ReasonCode, "null region event reason");
        AssertTrue(store.GetRequiredSnapshot("enemy_group").SelectedRegion == null, "null region must not mutate store truth");
    }

    public static void OwnerMismatchRegionIsRejectedWithReason()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense)
        });

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "enemy_group",
            BuildRegion("region_wrong_owner", ownerBattleGroupId: "other_group"),
            isEnemyPolicyMutation: true);

        AssertFalse(result.Accepted, "owner mismatch mutation should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch, result.ReasonCode, "owner mismatch reason");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch, result.Event.ReasonCode, "owner mismatch event reason");
        AssertTrue(store.GetRequiredSnapshot("enemy_group").SelectedRegion == null, "owner mismatch must not mutate store truth");
    }

    public static void InvalidSizeRegionIsRejectedWithReason()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense)
        });
        BattleTacticalRegionSnapshot invalidRegion = BuildRegion("invalid_size_region", "enemy_group");
        invalidRegion.Width = 0;
        invalidRegion.Height = 0;

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "enemy_group",
            invalidRegion,
            isEnemyPolicyMutation: true);

        AssertFalse(result.Accepted, "invalid size mutation should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedInvalidSize, result.ReasonCode, "invalid size reason");
        AssertEqual(BattleEventKind.BattleGroupTacticalRegionRejected, result.Event.Kind, "invalid size event kind");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedInvalidSize, result.Event.ReasonCode, "invalid size event reason");
        AssertTrue(store.GetRequiredSnapshot("enemy_group").SelectedRegion == null, "invalid size must not mutate store truth");
    }

    public static void PlayerCommandedGroupsRejectEnemyPolicyOverwrite()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup(
                "player_group",
                BattleGroupTacticalMode.PlayerCommanded,
                BuildRegion("player_plan_region", "player_group", BattleTacticalRegionKind.FixedTarget))
        });

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "player_group",
            BuildRegion("enemy_policy_region", ownerBattleGroupId: "player_group"),
            isEnemyPolicyMutation: true);

        AssertFalse(result.Accepted, "enemy policy must not overwrite player-commanded groups");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedPlayerPolicyOverwrite, result.ReasonCode, "player overwrite reason");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedPlayerPolicyOverwrite, result.Event.ReasonCode, "player overwrite event reason");
        AssertEqual("player_plan_region", store.GetRequiredSnapshot("player_group").SelectedRegion?.RegionId, "player region intent stays unchanged");
    }

    public static void PlayerCommandedEngagementCannotBecomeEnemyActiveAssault()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("player_group", BattleGroupTacticalMode.PlayerCommanded)
        });

        bool convertedToEnemyAssault = InvokeTryApplyEngagementState(
            store,
            "player_group",
            BattleGroupEngagementState.Engaged,
            BattleGroupTacticalMode.EnemyActiveDefense);

        AssertFalse(convertedToEnemyAssault, "player commanded group must reject enemy-style engagement mode mutation");
        BattleGroupTacticalState afterRejected = store.GetRequiredSnapshot("player_group");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, afterRejected.TacticalMode, "player tactical mode remains player-commanded");
        AssertEqual(BattleGroupEngagementState.NotEngaged, afterRejected.EngagementState, "rejected enemy-style mutation must not change engagement state");

        bool enteredPlayerScopedEngagement = InvokeTryApplyEngagementState(
            store,
            "player_group",
            BattleGroupEngagementState.Engaged,
            BattleGroupTacticalMode.PlayerCommanded);

        AssertTrue(enteredPlayerScopedEngagement, "player group may still enter local combat response inside player command scope");
        BattleGroupTacticalState afterPlayerScopedEngagement = store.GetRequiredSnapshot("player_group");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, afterPlayerScopedEngagement.TacticalMode, "player scoped engagement keeps player tactical mode");
        AssertEqual(BattleGroupEngagementState.Engaged, afterPlayerScopedEngagement.EngagementState, "player scoped engagement can update engagement state");
    }

    public static void GlobalSnapshotCacheIsReadOnly()
    {
        BattleGroupTacticalStateStore store = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup(
                "enemy_group",
                BattleGroupTacticalMode.EnemyOffense,
                BuildRegion("initial_region", "enemy_group", BattleTacticalRegionKind.FixedTarget))
        });
        BattleGroupTacticalSnapshotCache cache = BattleGroupTacticalSnapshotCache.Capture(store);

        BattleGroupTacticalState cached = cache.GetRequiredSnapshot("enemy_group");
        cached.SelectedRegion!.RegionId = "mutated_from_cache";

        AssertEqual("initial_region", cache.GetRequiredSnapshot("enemy_group").SelectedRegion?.RegionId, "cache returns defensive copies");
        AssertEqual("initial_region", store.GetRequiredSnapshot("enemy_group").SelectedRegion?.RegionId, "cache mutation cannot affect store truth");

        BattleGroupTacticalState exposedStoreState = store.States["enemy_group"];
        exposedStoreState.SelectedRegion!.RegionId = "mutated_from_store_state";
        AssertEqual("initial_region", store.GetRequiredSnapshot("enemy_group").SelectedRegion?.RegionId, "store state API returns defensive copies");

        BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
            "enemy_group",
            BuildRegion("store_region", "enemy_group", BattleTacticalRegionKind.FixedTarget),
            isEnemyPolicyMutation: true);

        AssertTrue(result.Accepted, "authoritative mutation should go through store");
        AssertEqual("store_region", store.GetRequiredSnapshot("enemy_group").SelectedRegion?.RegionId, "store mutation updates store truth");
    }

    public static void TacticalMutationEventsIncludeContextAndUniqueIds()
    {
        BattleGroupTacticalStateStore directStore = BattleGroupTacticalStateStore.FromBattleGroups(new[]
        {
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense)
        });

        BattleGroupTacticalRegionMutationResult first = directStore.TrySetRegion(
            "enemy_group",
            BuildRegion("repeated_reject", ownerBattleGroupId: ""),
            isEnemyPolicyMutation: true);
        BattleGroupTacticalRegionMutationResult second = directStore.TrySetRegion(
            "enemy_group",
            BuildRegion("repeated_reject", ownerBattleGroupId: ""),
            isEnemyPolicyMutation: true);

        AssertTrue(first.Event.EventId != second.Event.EventId, "repeated rejected mutations should have unique event ids");
        AssertEqual("", first.Event.BattleId, "direct store event battle id defaults empty");

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildInvalidSeedSnapshot());
        BattleGroupTacticalRegionMutationResult runtimeSeedResult = controller.State.TacticalInitializationResults["enemy_group"][0];
        AssertEqual("battle_invalid_tactical_seed", runtimeSeedResult.Event.BattleId, "runtime seeded store events include battle id");
    }

    public static void InvalidInitialTacticalSeedIsCaptured()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildInvalidSeedSnapshot());

        AssertTrue(controller.State.TacticalStates["enemy_group"].SelectedRegion == null, "invalid seed must not mutate selected region");
        AssertEqual(1, controller.State.TacticalInitializationResults["enemy_group"].Count, "invalid seed exposes one initialization result");
        BattleGroupTacticalRegionMutationResult result = controller.State.TacticalInitializationResults["enemy_group"][0];
        AssertFalse(result.Accepted, "invalid seed result should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch, result.ReasonCode, "invalid seed reason");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch, result.Event.ReasonCode, "invalid seed event reason");
    }

    public static void MultipleInitialTacticalSeedsAreAllCaptured()
    {
        BattleGroupSnapshot group = BuildGroup(
            "enemy_group",
            BattleGroupTacticalMode.EnemyOffense,
            BuildRegion("valid_seed", "enemy_group", BattleTacticalRegionKind.FixedTarget));
        group.InitialTacticalRegions.Add(BuildRegion("invalid_later_seed", "other_group", BattleTacticalRegionKind.FixedTarget));
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildSeedSnapshot("snapshot_multi_seed", "battle_multi_seed", group));

        AssertEqual("valid_seed", controller.State.TacticalStates["enemy_group"].SelectedRegion?.RegionId, "valid seed remains selected after invalid later seed");
        AssertEqual(2, controller.State.TacticalInitializationResults["enemy_group"].Count, "each initial seed attempt exposes a result");
        AssertTrue(controller.State.TacticalInitializationResults["enemy_group"][0].Accepted, "first seed should be accepted");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch, controller.State.TacticalInitializationResults["enemy_group"][1].ReasonCode, "invalid later seed reason");
    }

    public static void DuplicateTacticalSeedGroupIsRejected()
    {
        BattleStartSnapshot snapshot = BuildSeedSnapshot(
            "snapshot_duplicate_group",
            "battle_duplicate_group",
            BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense, BuildRegion("first_seed", "enemy_group")));
        snapshot.BattleGroups.Add(BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyActiveDefense));
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertEqual(1, controller.State.TacticalStates.Count, "duplicate battle group id must not create a second state");
        AssertEqual(BattleGroupTacticalMode.EnemyOffense, controller.State.TacticalStates["enemy_group"].TacticalMode, "first group remains authoritative");
        AssertEqual("first_seed", controller.State.TacticalStates["enemy_group"].SelectedRegion?.RegionId, "first group selected region remains authoritative");
        AssertEqual(2, controller.State.TacticalInitializationResults["enemy_group"].Count, "duplicate group rejection is exposed even without duplicate seeds");
        AssertFalse(controller.State.TacticalInitializationResults["enemy_group"][1].Accepted, "duplicate group seed should reject");
        AssertEqual(BattleGroupTacticalReasonCode.RegionRejectedDuplicateGroup, controller.State.TacticalInitializationResults["enemy_group"][1].ReasonCode, "duplicate group reason");
    }

    public static void RuntimeInitializesOneTacticalStatePerBattleGroup()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_tactical_state_init",
            BattleId = "battle_tactical_state_init",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("player_group", BattleGroupTacticalMode.PlayerCommanded),
                BuildGroup(
                    "enemy_group",
                    BattleGroupTacticalMode.EnemyOffense,
                    BuildRegion("enemy_seed_region", "enemy_group", BattleTacticalRegionKind.FixedTarget))
            }
        };
        AddSurface(snapshot, 0, 0);
        AddSurface(snapshot, 1, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertEqual(2, controller.State.TacticalStates.Count, "runtime creates one tactical state per battle group");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, controller.State.TacticalStates["player_group"].TacticalMode, "missing tactical seed defaults to player-commanded");
        AssertEqual(BattleGroupTacticalMode.EnemyOffense, controller.State.TacticalStates["enemy_group"].TacticalMode, "enemy tactical mode initializes from snapshot");
        AssertEqual("enemy_seed_region", controller.State.TacticalStates["enemy_group"].SelectedRegion?.RegionId, "initial region seed initializes runtime state");
    }

    public static void GroupPerceptionSummaryUsesAliveMembersAndPerceivedHostiles()
    {
        IReadOnlyDictionary<string, BattleGroupPerceptionSummary> summaries =
            BattleGroupPerceptionSummaryBuilder.BuildForGroups(
                new[]
                {
                    BuildRuntimeActor("enemy_a", "enemy_group", "enemy", 0, 0),
                    BuildRuntimeActor("enemy_b", "enemy_group", "enemy", 2, 0),
                    BuildRuntimeActor("enemy_dead", "enemy_group", "enemy", 4, 0, hitPoints: 0),
                    BuildRuntimeActor("player_near", "player_group", "player", 5, 0),
                    BuildRuntimeActor("player_far", "player_far_group", "player", 10, 0),
                    BuildRuntimeActor("enemy_other", "enemy_other_group", "enemy", 3, 0)
                },
                runtimeTick: 7);

        BattleGroupPerceptionSummary summary = summaries["enemy_group"];

        AssertSequence(new[] { "player_near" }, summary.PerceivedHostileActorIds, "summary hostile ids");
        AssertEqual(2, summary.MemberCoverages.Count, "dead member must not contribute coverage");
        AssertEqual(0, summary.MinAnchorCellX, "group min x");
        AssertEqual(2, summary.MaxAnchorCellX, "group max x");
        AssertEqual(7, summary.LastBuiltRuntimeTick, "summary runtime tick");
        BattleGroupPerceptionMemberCoverage enemyA = summary.MemberCoverages.Single(item => item.ActorId == "enemy_a");
        BattleGroupPerceptionMemberCoverage enemyB = summary.MemberCoverages.Single(item => item.ActorId == "enemy_b");
        AssertEqual(0, enemyA.PerceivedHostileActorIds.Count, "enemy_a should not perceive the range-five hostile");
        AssertSequence(new[] { "player_near" }, enemyB.PerceivedHostileActorIds, "enemy_b coverage hostile ids");
    }

    public static void RuntimeBuildsGroupPerceptionSummaries()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildGroupPerceptionSnapshot());

        controller.AdvanceNextTick();

        AssertTrue(controller.State.GroupPerceptionSummaries.TryGetValue("enemy_group", out BattleGroupPerceptionSummary? summary), "runtime should expose enemy group perception summary");
        AssertTrue(summary!.PerceivedHostileActorIds.Contains("player_force:1"), "enemy summary should perceive nearby player corps");
    }

    public static void RuntimeStateDoesNotExposeTacticalStorePublicly()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Runtime", "Battle", "BattleRuntimeState.cs"));
        AssertFalse(
            source.Contains("public BattleGroupTacticalStateStore TacticalStateStore", StringComparison.Ordinal),
            "public runtime state must expose tactical snapshots only, not the authoritative store");
    }

    public static void BattleEntrySeedsEnemyOffenseFixedRegionByPlayerDensity()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_offense_density");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_west", "player_defensive_deployment", centerX: 0, centerY: 0, priority: 5));
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_east", "player_defensive_deployment", centerX: 10, centerY: 0, priority: 1));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 3, (0, 0), (10, 0), (11, 0)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (8, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        AssertEqual(BattleGroupTacticalMode.EnemyOffense, enemy.TacticalMode, "attacker enemy mode");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual(BattleTacticalRegionKind.FixedTarget, fixedRegion.Kind, "enemy offense seed region kind");
        AssertEqual(BattleCommanderGroupIdentity.Resolve(enemy), fixedRegion.OwnerBattleGroupId, "enemy offense seed owner");
        AssertEqual("defense_east", fixedRegion.SourceRegionId, "enemy offense chooses denser player defensive region");
        AssertEqual(BattleGroupTacticalReasonCode.RegionFixedSelectedPlayerDensity, fixedRegion.ReasonCode, "enemy offense density reason");
        AssertTrue(
            result.Snapshot.BattleGroups.Where(item => item.FactionId == "player").All(item => item.TacticalMode == BattleGroupTacticalMode.PlayerCommanded),
            "player groups remain player-commanded");
        AssertTrue(
            result.Snapshot.BattleGroups.Where(item => item.FactionId == "player").All(item => item.InitialTacticalRegions.Count == 0),
            "player groups do not receive enemy-policy regions");
    }

    public static void BattleEntrySeedsEnemyActiveDefenseFromPlayerOffensiveRegion()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_active_defense_offense_region");
        request.AttackerFactionId = "player";
        request.DefenderFactionId = "enemy";
        request.ObjectiveZones.Add(BuildObjectiveZone("player_assault_lane", "player_offensive_deployment", centerX: 3, centerY: 0, priority: 1));
        request.ObjectiveZones.Add(BuildObjectiveZone("enemy_deployment", "enemy_deployment", centerX: 9, centerY: 0, priority: 10, deploymentSide: "Enemy"));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (3, 0)));
        request.EnemyForces.Add(BuildForce("enemy_defender", "enemy", count: 1, (9, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot player = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "player_company");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_defender");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, player.TacticalMode, "player mode remains unchanged");
        AssertEqual(0, player.InitialTacticalRegions.Count, "player intent remains unchanged");
        AssertEqual(BattleGroupTacticalMode.EnemyActiveDefense, enemy.TacticalMode, "defender enemy active defense mode");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual(BattleTacticalRegionKind.FixedTarget, fixedRegion.Kind, "active defense seed is fixed before temporary regions exist");
        AssertEqual("player_assault_lane", fixedRegion.SourceRegionId, "active defense chooses player offensive region");
    }

    public static void BattleEntrySeedsEnemyHoldDefenseFromHoldPosture()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_hold_defense");
        request.AttackerFactionId = "player";
        request.DefenderFactionId = "enemy";
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (1, 0)));
        request.EnemyForces.Add(BuildForce("enemy_garrison", "enemy", count: 1, footprintWidth: 2, footprintHeight: 3, (7, 2)));
        request.EnemyBattleGroupPlan = new BattleGroupPlanSnapshot
        {
            EngagementRule = BattleEngagementRule.Hold
        };

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_garrison");
        AssertEqual(BattleGroupTacticalMode.EnemyHoldDefense, enemy.TacticalMode, "hold defender tactical mode");
        BattleTacticalRegionSnapshot holdRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual(BattleTacticalRegionKind.Hold, holdRegion.Kind, "hold defender region kind");
        AssertEqual(BattleCommanderGroupIdentity.Resolve(enemy), holdRegion.OwnerBattleGroupId, "hold region owner");
        AssertEqual(7, holdRegion.CenterCellX, "hold region uses deployed cell x");
        AssertEqual(2, holdRegion.CenterCellY, "hold region uses deployed cell y");
        AssertEqual(2, holdRegion.Width, "hold region uses deployed footprint width");
        AssertEqual(3, holdRegion.Height, "hold region uses deployed footprint height");
        AssertEqual($"{BattleCommanderGroupIdentity.Resolve(enemy)}:hold_seed", holdRegion.SourceRegionId, "hold region source falls back to deterministic hold seed id");
        AssertEqual(BattleGroupTacticalReasonCode.RegionHoldSeededPosture, holdRegion.ReasonCode, "hold region reason code");
    }

    public static void BattleEntryFixedRegionUsesPriorityWhenPlayerDensityTies()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_priority_tie");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_low_priority", "player_defensive_deployment", centerX: 8, centerY: 0, priority: 1));
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_high_priority", "player_defensive_deployment", centerX: 0, centerY: 0, priority: 100));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 2, (8, 0), (0, 0)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (8, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        AssertEqual(BattleGroupTacticalMode.EnemyOffense, enemy.TacticalMode, "attacker enemy mode");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual("defense_high_priority", fixedRegion.SourceRegionId, "priority breaks tied player density");
        AssertEqual(BattleGroupTacticalReasonCode.RegionFixedSelectedPriority, fixedRegion.ReasonCode, "priority fallback reason");
    }

    public static void BattleEntryFixedRegionUsesDistanceWhenDensityAndPriorityTie()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_distance_tie");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_far", "player_defensive_deployment", centerX: 0, centerY: 0, priority: 10));
        request.ObjectiveZones.Add(BuildObjectiveZone("defense_near", "player_defensive_deployment", centerX: 8, centerY: 0, priority: 10));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (20, 0)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (8, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual("defense_near", fixedRegion.SourceRegionId, "distance breaks equal density and priority");
        AssertEqual(BattleGroupTacticalReasonCode.RegionFixedSelectedPriority, fixedRegion.ReasonCode, "distance fallback still uses priority fallback reason in phase 2");
    }

    public static void BattleEntryFixedRegionUsesLexicographicIdWhenScoreAndDistanceTie()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_lexicographic_tie");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("zeta_defense", "player_defensive_deployment", centerX: 8, centerY: 0, priority: 10));
        request.ObjectiveZones.Add(BuildObjectiveZone("alpha_defense", "player_defensive_deployment", centerX: 0, centerY: 0, priority: 10));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (20, 0)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (5, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual("alpha_defense", fixedRegion.SourceRegionId, "lexicographic region id breaks equal score and distance");
        AssertEqual(BattleGroupTacticalReasonCode.RegionFixedSelectedPriority, fixedRegion.ReasonCode, "lexicographic fallback still uses priority fallback reason in phase 2");
    }

    public static void BattleEntryNoFixedCandidateSeedsModeWithoutRegion()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_no_fixed_candidate");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("enemy_only_zone", "enemy_deployment", centerX: 8, centerY: 0, priority: 100, deploymentSide: "Enemy"));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (0, 0)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (8, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        AssertEqual(BattleGroupTacticalMode.EnemyOffense, enemy.TacticalMode, "enemy mode still seeds without fixed candidate");
        AssertEqual(0, enemy.InitialTacticalRegions.Count, "no fixed candidate should not invent hidden coordinates");
    }

    public static void BattleEntryPreservesPreauthoredPlayerTacticalSeed()
    {
        BattleGroupSnapshot player = BuildGroup(
            "player_group",
            BattleGroupTacticalMode.PlayerCommanded,
            BuildRegion("player_authored_seed", "player_group", BattleTacticalRegionKind.FixedTarget));
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_preserve_player_seed",
            BattleId = "battle_preserve_player_seed",
            TargetLocationId = "site_1",
            BattleGroups = { player }
        };
        BattleStartRequest request = BuildBattleEntryRequest("entry_preserve_player_seed");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";

        InvokeBattleEntryTacticalSeeds(snapshot, request, ("player_group", "player", "player_force", "Player"));

        BattleGroupSnapshot seededPlayer = snapshot.BattleGroups.Single(item => item.BattleGroupId == "player_group");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, seededPlayer.TacticalMode, "player tactical mode remains player-commanded");
        BattleTacticalRegionSnapshot preservedRegion = seededPlayer.InitialTacticalRegions.Single();
        AssertEqual("player_authored_seed", preservedRegion.RegionId, "player preauthored tactical seed is preserved");
        AssertEqual("player_group", preservedRegion.OwnerBattleGroupId, "player preauthored tactical seed owner is preserved");
    }

    public static void EnemyRegionMovementIgnoresMovingUnitOutsidePerception()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildEnemyRegionMovementSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1");
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_force:1");

        AssertTrue(move != null, "non-engaged enemy should start moving toward its selected tactical region");
        AssertEqual("enemy_fixed_north", move!.TargetId, "region movement event target id");
        AssertEqual("region_fixed_advance", move.ReasonCode, "region movement reason");
        AssertTrue(move.ToGridY > move.FromGridY, $"enemy should step toward the northern fixed region: fromY={move.FromGridY} toY={move.ToGridY}");
        AssertEqual("", enemy.TargetActorId, "region movement must not store an actor target id");
    }

    public static void PlayerObjectiveMovementStillUsesPlayerPlan()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildPlayerObjectiveMovementSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_force:1");

        AssertTrue(move != null, "player move-first plan should still emit objective movement");
        AssertEqual("objective_gate", move!.TargetId, "player objective movement target");
        AssertEqual("plan_objective_advance", move.ReasonCode, "player objective movement reason");
        AssertTrue(move.ToGridX > move.FromGridX, $"player should step toward the planned objective: fromX={move.FromGridX} toX={move.ToGridX}");
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        BattleGroupTacticalMode tacticalMode,
        BattleTacticalRegionSnapshot? initialRegion = null)
    {
        BattleGroupSnapshot group = new()
        {
            BattleGroupId = groupId,
            FactionId = groupId.StartsWith("enemy", StringComparison.Ordinal) ? "enemy" : "player",
            SourceForceId = $"{groupId}_force",
            HeroId = $"{groupId}_hero",
            HeroDefinitionId = $"{groupId}_hero_definition",
            CorpsId = $"{groupId}_corps",
            CorpsDefinitionId = $"{groupId}_corps_definition",
            CorpsStrength = 80,
            MaxHitPoints = 80,
            AttackDamage = 1,
            SourceLocationId = "site_1",
            CellX = groupId.StartsWith("enemy", StringComparison.Ordinal) ? 1 : 0,
            CellY = 0,
            TacticalMode = tacticalMode
        };
        if (initialRegion != null)
        {
            group.InitialTacticalRegions.Add(initialRegion);
        }

        return group;
    }

    private static BattleStartSnapshot BuildInvalidSeedSnapshot()
    {
        return BuildSeedSnapshot(
            "snapshot_invalid_tactical_seed",
            "battle_invalid_tactical_seed",
            BuildGroup(
                "enemy_group",
                BattleGroupTacticalMode.EnemyOffense,
                BuildRegion("wrong_owner_seed", "other_group", BattleTacticalRegionKind.FixedTarget)));
    }

    private static BattleStartSnapshot BuildSeedSnapshot(string snapshotId, string battleId, BattleGroupSnapshot group)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = snapshotId,
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups = { group }
        };
        AddSurface(snapshot, 1, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildEnemyRegionMovementSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_enemy_region_movement",
            BattleId = "battle_enemy_region_movement",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    BattleGroupTacticalMode.EnemyOffense,
                    BuildRegion("enemy_fixed_north", "enemy_group", BattleTacticalRegionKind.FixedTarget, centerX: 0, centerY: 8)),
                new BattleGroupSnapshot
                {
                    BattleGroupId = "player_group",
                    FactionId = "player",
                    SourceForceId = "player_force",
                    HeroId = "player_hero",
                    HeroDefinitionId = "player_hero_definition",
                    CorpsId = "player_corps",
                    CorpsDefinitionId = "player_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = 0,
                    CellY = 8,
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded
                }
            }
        };

        snapshot.BattleGroups[0].SourceForceId = "enemy_force";
        snapshot.BattleGroups[0].CellX = 0;
        snapshot.BattleGroups[0].CellY = 0;
        for (int y = 0; y <= 8; y++)
        {
            for (int x = 0; x <= 8; x++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildPlayerObjectiveMovementSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_player_objective_region_guard",
            BattleId = "battle_player_objective_region_guard",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "objective_gate",
                    CellX = 3,
                    CellY = 0,
                    Width = 1,
                    Height = 1
                }
            },
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "player_group",
                    FactionId = "player",
                    SourceForceId = "player_force",
                    HeroId = "player_hero",
                    HeroDefinitionId = "player_hero_definition",
                    CorpsId = "player_corps",
                    CorpsDefinitionId = "player_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = 0,
                    CellY = 0,
                    Plan = new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "player_group",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst
                    },
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "enemy_group",
                    FactionId = "enemy",
                    SourceForceId = "enemy_force",
                    HeroId = "enemy_hero",
                    HeroDefinitionId = "enemy_hero_definition",
                    CorpsId = "enemy_corps",
                    CorpsDefinitionId = "enemy_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = 7,
                    CellY = 0,
                    InitialCorpsCommandId = "HoldLine",
                    TacticalMode = BattleGroupTacticalMode.EnemyHoldDefense
                }
            }
        };

        for (int x = 0; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildGroupPerceptionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_group_perception",
            BattleId = "battle_group_perception",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("player_group", BattleGroupTacticalMode.PlayerCommanded)
            }
        };

        snapshot.BattleGroups[0].SourceForceId = "enemy_force";
        snapshot.BattleGroups[0].CellX = 0;
        snapshot.BattleGroups[0].CellY = 0;
        snapshot.BattleGroups[1].SourceForceId = "player_force";
        snapshot.BattleGroups[1].CellX = 3;
        snapshot.BattleGroups[1].CellY = 0;
        for (int x = 0; x <= 5; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleTacticalRegionSnapshot BuildRegion(
        string regionId,
        string ownerBattleGroupId,
        BattleTacticalRegionKind kind = BattleTacticalRegionKind.FixedTarget,
        int centerX = 0,
        int centerY = 0)
    {
        return new BattleTacticalRegionSnapshot
        {
            RegionId = regionId,
            OwnerBattleGroupId = ownerBattleGroupId,
            Kind = kind,
            CenterCellX = centerX,
            CenterCellY = centerY,
            CenterCellHeight = 0,
            Width = 1,
            Height = 1
        };
    }

    private static BattleRuntimeActor BuildRuntimeActor(
        string actorId,
        string battleGroupId,
        string factionId,
        int x,
        int y,
        int hitPoints = 10)
    {
        return new BattleRuntimeActor
        {
            ActorId = actorId,
            BattleGroupId = battleGroupId,
            FactionId = factionId,
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = hitPoints,
            GridX = x,
            GridY = y,
            GridHeight = 0
        };
    }

    private static BattleStartRequest BuildBattleEntryRequest(string id)
    {
        return new BattleStartRequest
        {
            RequestId = id,
            ContextId = id,
            TargetSiteId = "site_1",
            BattleKind = BattleKind.AssaultSite
        };
    }

    private static BattleForceRequest BuildForce(
        string forceId,
        string factionId,
        int count,
        params (int X, int Y)[] placements)
    {
        return BuildForce(forceId, factionId, count, footprintWidth: 1, footprintHeight: 1, placements);
    }

    private static BattleForceRequest BuildForce(
        string forceId,
        string factionId,
        int count,
        int footprintWidth,
        int footprintHeight,
        params (int X, int Y)[] placements)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = $"{forceId}_unit",
            FactionId = factionId,
            Count = count,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight,
            MaxHitPoints = 80,
            AttackDamage = 1
        };

        foreach ((int x, int y) in placements)
        {
            force.PreferredPlacements.Add(new BattleForcePlacementRequest
            {
                CellX = x,
                CellY = y
            });
        }

        return force;
    }

    private static BattleObjectiveZoneSnapshot BuildObjectiveZone(
        string zoneId,
        string role,
        int centerX,
        int centerY,
        int priority,
        string deploymentSide = "Player")
    {
        return new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = zoneId,
            ObjectiveRole = role,
            DeploymentSide = deploymentSide,
            FactionId = deploymentSide.Equals("Player", StringComparison.OrdinalIgnoreCase) ? "player" : "enemy",
            Priority = priority,
            CellX = centerX,
            CellY = centerY,
            Width = 3,
            Height = 3
        };
    }

    private static void InvokeBattleEntryTacticalSeeds(
        BattleStartSnapshot snapshot,
        BattleStartRequest request,
        params (string GroupId, string FactionId, string SourceForceId, string PlanSide)[] metadataRows)
    {
        Type serviceType = typeof(BattleGroupSessionProbeService);
        Type seedType = serviceType.GetNestedType("ProbeSeed", System.Reflection.BindingFlags.NonPublic) ??
            throw new InvalidOperationException("ProbeSeed type not found");
        Type metadataType = serviceType.GetNestedType("ProbeGroupMetadata", System.Reflection.BindingFlags.NonPublic) ??
            throw new InvalidOperationException("ProbeGroupMetadata type not found");
        Type planSideType = serviceType.GetNestedType("BattlePlanSide", System.Reflection.BindingFlags.NonPublic) ??
            throw new InvalidOperationException("BattlePlanSide type not found");
        object seed = Activator.CreateInstance(seedType, nonPublic: true) ??
            throw new InvalidOperationException("ProbeSeed could not be created");
        System.Collections.IDictionary metadata = seedType.GetProperty("GroupMetadata")?.GetValue(seed) as System.Collections.IDictionary ??
            throw new InvalidOperationException("ProbeSeed metadata dictionary not found");

        foreach ((string groupId, string factionId, string sourceForceId, string planSide) in metadataRows)
        {
            object row = Activator.CreateInstance(metadataType, nonPublic: true) ??
                throw new InvalidOperationException("ProbeGroupMetadata could not be created");
            metadataType.GetProperty("FactionId")?.SetValue(row, factionId);
            metadataType.GetProperty("SourceForceId")?.SetValue(row, sourceForceId);
            metadataType.GetProperty("PlanSide")?.SetValue(row, Enum.Parse(planSideType, planSide));
            metadata.Add(groupId, row);
        }

        serviceType.GetMethod("ApplyBattleEntryTacticalSeeds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(
            null,
            new object[] { snapshot, request, seed });
    }

    private static void AddSurface(BattleStartSnapshot snapshot, int x, int y)
    {
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
        {
            X = x,
            Y = y,
            Height = 0,
            MoveCost = 1
        });
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

    private static bool InvokeTryApplyEngagementState(
        BattleGroupTacticalStateStore store,
        string battleGroupId,
        BattleGroupEngagementState engagementState,
        BattleGroupTacticalMode tacticalMode)
    {
        System.Reflection.MethodInfo? method = typeof(BattleGroupTacticalStateStore).GetMethod(
            "TryApplyEngagementState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        AssertTrue(method != null, "tactical state store should keep a narrow internal engagement mutation API");
        object? result = method!.Invoke(store, new object[] { battleGroupId, engagementState, tacticalMode });
        return result is bool accepted && accepted;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
        }
    }
}
