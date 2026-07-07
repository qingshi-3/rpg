using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleCommandRegressionCases
{
    internal static void CommandRequestSupportsMultiGroupDestinationBeaconPayload()
    {
        CommandRequest request = new()
        {
            CommandId = "cmd_beacon_multi",
            BattleId = "battle_beacon_contract",
            BattleGroupId = "group_alpha",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 5,
            TargetGridY = 2,
            TargetGridHeight = 0,
            BeaconId = "beacon_shared"
        };
        request.BattleGroupIds.Add("group_alpha");
        request.BattleGroupIds.Add("group_beta");

        CommandValidationResult result = new BattleCommandApplicationValidator().Validate(
            request,
            new[] { "group_alpha", "group_beta" },
            allowHero: false,
            allowCorps: false,
            allowCombined: true);

        AssertTrue(result.Accepted, "application validation should accept a combined destination-beacon command for all selected groups");
        AssertEqual(2, request.BattleGroupIds.Count, "destination beacon command should carry all selected battle groups");
        AssertEqual("group_alpha", request.BattleGroupId, "legacy primary group id should remain available for compatibility diagnostics");
    }

    internal static void RuntimeDestinationBeaconCommandAcceptsSharedReachableBeacon()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildBeaconCommandSnapshot("battle_beacon_shared"));
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_shared",
            BattleId = "battle_beacon_shared",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 5,
            TargetGridY = 1,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a", "group_player_b" }
        });

        AssertTrue(submit.Accepted, $"reachable shared beacon should be accepted: {submit.ReasonCode}");
        AssertEqual(1, controller.State.DestinationBeacons.Count, "selected groups should share one runtime destination beacon");
        BattleRuntimeDestinationBeacon beacon = controller.State.DestinationBeacons.Single();
        AssertEqual("cmd_beacon_shared", beacon.CommandId, "beacon should preserve command identity");
        AssertEqual(5, beacon.Anchor.X, "beacon destination x");
        AssertEqual(1, beacon.Anchor.Y, "beacon destination y");
        AssertTrue(beacon.OwnerBattleGroupIds.SequenceEqual(new[] { "group_player_a", "group_player_b" }), "beacon should be shared by selected groups in deterministic order");
        AssertTrue(
            controller.State.Actors
                .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId is "group_player_a" or "group_player_b")
                .All(item => item.ActiveDestinationBeaconId == beacon.BeaconId),
            "selected corps should read the shared beacon as their active destination");
        AssertTrue(
            controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.SourceCommandId == "cmd_beacon_shared" &&
                item.TargetGridX == 5 &&
                item.TargetGridY == 1),
            "accepted beacon command should enter the runtime event stream with destination facts");
    }

    internal static void RuntimeDestinationBeaconCommandRejectsUnreachableMultiSelectAtomically()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildSplitTopologyBeaconSnapshot("battle_beacon_atomic_reject"));
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_unreachable",
            BattleId = "battle_beacon_atomic_reject",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 2,
            TargetGridY = 0,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a", "group_player_b" }
        });

        AssertTrue(!submit.Accepted, "multi-select beacon should reject when any selected group cannot reach the destination");
        AssertEqual("destination_unreachable", submit.ReasonCode, "atomic rejection reason");
        AssertEqual(0, controller.State.DestinationBeacons.Count, "atomic rejection must not create a partial beacon");
        AssertTrue(
            controller.State.Actors
                .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId.StartsWith("group_player_", StringComparison.Ordinal))
                .All(item => string.IsNullOrWhiteSpace(item.ActiveDestinationBeaconId)),
            "atomic rejection must leave every selected group on its previous destination");
    }

    internal static void RuntimeDestinationBeaconReplacementAffectsOnlySelectedGroups()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildBeaconCommandSnapshot("battle_beacon_replace"));
        BattleRuntimeCommandSubmitResult first = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_first",
            BattleId = "battle_beacon_replace",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 5,
            TargetGridY = 1,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a", "group_player_b" }
        });
        AssertTrue(first.Accepted, "first shared beacon should be accepted");
        string firstBeaconId = controller.State.DestinationBeacons.Single().BeaconId;

        BattleRuntimeCommandSubmitResult second = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_second",
            BattleId = "battle_beacon_replace",
            BattleGroupId = "group_player_b",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 1,
            TargetGridY = 3,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_b" }
        });

        AssertTrue(second.Accepted, "selected group should accept a replacement beacon");
        BattleRuntimeActor groupA = controller.State.Actors.Single(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId == "group_player_a");
        BattleRuntimeActor groupB = controller.State.Actors.Single(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId == "group_player_b");
        BattleRuntimeActor groupC = controller.State.Actors.Single(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId == "group_player_c");
        AssertEqual(firstBeaconId, groupA.ActiveDestinationBeaconId, "non-selected group should keep the previous shared beacon");
        AssertTrue(!string.Equals(firstBeaconId, groupB.ActiveDestinationBeaconId, StringComparison.Ordinal), "selected group should receive the replacement beacon");
        AssertEqual("", groupC.ActiveDestinationBeaconId, "unselected unrelated group should remain unaffected");
    }

    internal static void RuntimeDestinationBeaconReplacementRemovesOrphanedBeacons()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildBeaconCommandSnapshot("battle_beacon_orphan_cleanup"));
        BattleRuntimeCommandSubmitResult first = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_orphan_first",
            BattleId = "battle_beacon_orphan_cleanup",
            BattleGroupId = "group_player_b",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 5,
            TargetGridY = 1,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_b" }
        });
        BattleRuntimeCommandSubmitResult second = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_orphan_second",
            BattleId = "battle_beacon_orphan_cleanup",
            BattleGroupId = "group_player_b",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 1,
            TargetGridY = 3,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_b" }
        });

        AssertTrue(first.Accepted, "first selected-group beacon should be accepted");
        AssertTrue(second.Accepted, "replacement selected-group beacon should be accepted");
        AssertEqual(1, controller.State.DestinationBeacons.Count, "replacing a group's destination should remove ownerless old beacons");
        BattleRuntimeDestinationBeacon remainingBeacon = controller.State.DestinationBeacons.Single();
        AssertTrue(remainingBeacon.OwnerBattleGroupIds.SequenceEqual(new[] { "group_player_b" }), "remaining beacon should still belong to the replaced group");
        AssertTrue(controller.State.DestinationBeacons.All(item => item.OwnerBattleGroupIds.Count > 0), "runtime should not retain displayable destination beacons with no owner groups");
        BattleRuntimeActor groupB = controller.State.Actors.Single(item => item.Kind == BattleRuntimeActorKind.Corps && item.BattleGroupId == "group_player_b");
        AssertEqual(remainingBeacon.BeaconId, groupB.ActiveDestinationBeaconId, "selected group should point at the remaining replacement beacon");
    }

    internal static void RuntimeDestinationBeaconCommandDuringPauseDoesNotAdvanceTime()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildBeaconCommandSnapshot("battle_beacon_pause"));
        controller.SetPaused(true, "test_pause_beacon_command");
        double beforeCommand = controller.CurrentTimeSeconds;

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_pause",
            BattleId = "battle_beacon_pause",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 4,
            TargetGridY = 2,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a" }
        });
        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(submit.Accepted, "pause-time beacon commands should update command facts");
        AssertEqual(beforeCommand, controller.CurrentTimeSeconds, "submitting a beacon during tactical pause must not advance runtime time");
        AssertEqual(beforeCommand, advance.RuntimeTimeSeconds, "paused advance should keep runtime time frozen");
        AssertTrue(
            !advance.Events.Any(item => item.Kind == BattleEventKind.MovementStarted || item.Kind == BattleEventKind.MovementCompleted),
            "pause-time beacon command should not consume the flow field or move units until runtime resumes");
    }

    internal static void RuntimePreparationSeededDestinationBeaconInitializesActiveBeacon()
    {
        BattleStartSnapshot snapshot = BuildBeaconCommandSnapshot("battle_beacon_initial_seed");
        BattleGroupSnapshot seededGroup = snapshot.BattleGroups.Single(item => item.BattleGroupId == "group_player_a");
        SetPlanInitialDestinationBeacon(seededGroup.Plan, 0, 3, 0);

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeActor corps = controller.State.Actors.Single(item =>
            item.Kind == BattleRuntimeActorKind.Corps &&
            item.BattleGroupId == "group_player_a");
        BattleRuntimeDestinationBeacon beacon = controller.State.DestinationBeacons.Single(item =>
            item.OwnerBattleGroupIds.SequenceEqual(new[] { "group_player_a" }));
        AssertEqual(0, beacon.Anchor.X, "initial beacon x");
        AssertEqual(3, beacon.Anchor.Y, "initial beacon y");
        AssertEqual(beacon.BeaconId, corps.ActiveDestinationBeaconId, "corps should start with the preparation-seeded beacon active");
        AssertEqual(BattleGroupPlanRuntimeState.AdvancingToBeacon, corps.PlanState, "initial beacon should put the group in beacon advance state");
        AssertTrue(
            controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.TargetId == beacon.BeaconId &&
                item.ReasonCode == "initial_destination_beacon_seeded"),
            "runtime start should emit a command event for the preparation-seeded beacon");
    }

    internal static void RuntimeDestinationBeaconMovementUsesBeaconInsteadOfDistantEnemy()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildBeaconCommandSnapshot("battle_beacon_movement"));
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_movement",
            BattleId = "battle_beacon_movement",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 0,
            TargetGridY = 3,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a" }
        });
        AssertTrue(submit.Accepted, "beacon movement command should be accepted");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        BattleEvent move = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player_a:1");
        AssertTrue(move != null, "selected corps should start moving toward the accepted beacon");
        AssertEqual(0, move!.ToGridX, "beacon flow should choose the vertical step toward the destination, not the distant enemy");
        AssertEqual(1, move.ToGridY, "beacon flow should move toward the destination beacon on the first decision boundary");
    }

    internal static void RuntimeDestinationBeaconFlowFieldBuildsOnceForSharedProfile()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters).Begin(BuildBeaconCommandSnapshot("battle_beacon_flow_cache"));
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_beacon_flow_cache",
            BattleId = "battle_beacon_flow_cache",
            BattleGroupId = "group_player_a",
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 5,
            TargetGridY = 1,
            TargetGridHeight = 0,
            BattleGroupIds = { "group_player_a", "group_player_b" }
        });
        AssertTrue(submit.Accepted, "shared beacon command should be accepted");

        controller.AdvanceFixedTick();

        AssertEqual(1L, counters.FlowFieldBuildCount, "one shared beacon/profile should build exactly one flow field");
        AssertEqual(1L, counters.FlowFieldCacheMissCount, "first shared beacon/profile lookup should miss once");
        AssertTrue(counters.FlowFieldCacheHitCount >= 1, "second selected group should reuse the shared beacon flow field");
    }

    internal static void RuntimeHoldLineCommandKeepsPlayerCorpsFromAdvancing()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hold_line", 80, 80, enemyCellX: 3, enemyCellY: 0);
        snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player").InitialCorpsCommandId = "HoldLine";

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertTrue(
            !result.EventStream.Events.Any(item =>
                item.ActorId == "force_player:1" &&
                item.Kind == BattleEventKind.MovementCompleted),
            "hold-line player corps should not advance toward distant enemies");
        AssertTrue(
            result.EventStream.Events.Any(item =>
                item.ActorId == "force_enemy:1" &&
                item.Kind == BattleEventKind.MovementCompleted),
            "enemy assault posture should still advance so hold-line can create a defensive contact pattern");
    }

    internal static void RuntimeFocusFireCommandTargetsLowestHealthEnemyCorps()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_focus_fire",
            BattleId = "battle_focus_fire",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_player",
                    FactionId = "player",
                    SourceForceId = "force_player",
                    HeroId = "hero_player",
                    HeroDefinitionId = "hero_def_player",
                    CorpsId = "corps_player",
                    CorpsDefinitionId = "player_corps",
                    CorpsStrength = 100,
                    SourceLocationId = "city_player",
                    CellX = 0,
                    CellY = 0,
                    InitialCorpsCommandId = "FocusFire"
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy_high",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy_high",
                    HeroId = "hero_enemy_high",
                    HeroDefinitionId = "hero_def_enemy_high",
                    CorpsId = "corps_enemy_high",
                    CorpsDefinitionId = "enemy_high_corps",
                    CorpsStrength = 80,
                    SourceLocationId = "site_1",
                    CellX = 1,
                    CellY = 0
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy_low",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy_low",
                    HeroId = "hero_enemy_low",
                    HeroDefinitionId = "hero_def_enemy_low",
                    CorpsId = "corps_enemy_low",
                    CorpsDefinitionId = "enemy_low_corps",
                    CorpsStrength = 20,
                    SourceLocationId = "site_1",
                    CellX = 0,
                    CellY = 1
                }
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent firstPlayerDamage = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "force_player:1" &&
            item.Kind == BattleEventKind.DamageApplied);
        AssertTrue(firstPlayerDamage != null, "focus-fire player corps should attack");
        AssertEqual("force_enemy_low:1", firstPlayerDamage!.TargetId, "focus fire should choose the lowest-health enemy corps before nearest-id fallback");
    }

    internal static void BattleGroupSessionProbeCopiesInitialCorpsCommandToPlayerSnapshot()
    {
        BattleStartRequest request = new()
        {
            RequestId = "request_command_1",
            ContextId = "battle_command_1",
            TargetSiteId = "site_1",
            InitialCorpsCommandId = "HoldLine"
        };
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "force_player",
            UnitDefinitionId = "player_corps",
            FactionId = "player",
            Count = 1
        });
        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "force_enemy",
            UnitDefinitionId = "enemy_corps",
            FactionId = "enemy",
            Count = 1
        });
        TargetBattleTestTopology.CompileRequestRect(request, -2, -2, 10, 4);

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().Probe(request);

        AssertTrue(result.Success, "probe should accept opposed player and enemy forces");
        BattleGroupSnapshot playerGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemyGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        AssertEqual("HoldLine", playerGroup.InitialCorpsCommandId, "player force should receive the selected initial corps command");
        AssertEqual("", enemyGroup.InitialCorpsCommandId, "enemy force should not inherit the player's initial corps command");
    }

    internal static void BattleGroupSessionProbeCopiesBattleGroupPlanToPlayerSnapshot()
    {
        BattleStartRequest request = new()
        {
            RequestId = "request_plan_1",
            ContextId = "battle_plan_1",
            TargetSiteId = "site_1",
            PlayerBattleGroupPlan = new BattleGroupPlanSnapshot
            {
                ObjectiveZoneId = "objective_upper_flank",
                EngagementRule = BattleEngagementRule.MoveFirst,
                InitialFormationId = "default_line"
            }
        };
        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "objective_upper_flank",
            DisplayName = "上路侧翼",
            ObjectiveRole = "flank",
            CellX = 5,
            CellY = -1,
            Width = 2,
            Height = 1
        });
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "force_player",
            UnitDefinitionId = "player_corps",
            FactionId = "player",
            Count = 1
        });
        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "force_enemy",
            UnitDefinitionId = "enemy_corps",
            FactionId = "enemy",
            Count = 1
        });

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "probe snapshot preparation should accept opposed forces");
        AssertEqual(1, result.Snapshot.ObjectiveZones.Count, "probe snapshot should preserve request objective zones");
        AssertEqual("objective_upper_flank", result.Snapshot.ObjectiveZones[0].ObjectiveZoneId, "snapshot objective zone id");
        BattleGroupSnapshot playerGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemyGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        AssertEqual("objective_upper_flank", playerGroup.Plan.ObjectiveZoneId, "player force should receive selected objective zone");
        AssertEqual(BattleEngagementRule.MoveFirst, playerGroup.Plan.EngagementRule, "player force should receive selected engagement rule");
        AssertEqual("default_line", playerGroup.Plan.InitialFormationId, "player force should receive selected formation");
        AssertEqual("", enemyGroup.Plan.ObjectiveZoneId, "enemy force should not inherit the player's objective zone");
        AssertEqual(BattleEngagementRule.AttackFirst, enemyGroup.Plan.EngagementRule, "enemy force should keep default engagement");
    }

    internal static void BattleGroupSessionProbeAppliesPerCompanyObjectivePlans()
    {
        BattleStartRequest request = new()
        {
            RequestId = "request_plan_groups",
            ContextId = "battle_plan_groups",
            TargetSiteId = "site_1"
        };
        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_north",
            DisplayName = "敌方部署区 1",
            CellX = 8,
            CellY = -2,
            Width = 3,
            Height = 2
        });
        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_south",
            DisplayName = "敌方部署区 2",
            CellX = 8,
            CellY = 3,
            Width = 3,
            Height = 2
        });
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "army_1:hero",
            SourceKind = "PlayerArmy",
            SourceId = "army_1",
            UnitDefinitionId = "hero_corps",
            FactionId = "player",
            Count = 1
        });
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "army_2:hero",
            SourceKind = "PlayerArmy",
            SourceId = "army_2",
            UnitDefinitionId = "hero_corps",
            FactionId = "player",
            Count = 1
        });
        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "force_enemy",
            UnitDefinitionId = "enemy_corps",
            FactionId = "enemy",
            Count = 1
        });
        request.PlayerBattleGroupPlans["PlayerArmy:army_1"] = new BattleGroupPlanSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_north",
            EngagementRule = BattleEngagementRule.MoveFirst,
            InitialFormationId = "default_line"
        };
        request.PlayerBattleGroupPlans["PlayerArmy:army_2"] = new BattleGroupPlanSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_south",
            EngagementRule = BattleEngagementRule.Hold,
            InitialFormationId = "default_line"
        };

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "probe snapshot preparation should accept per-company plans");
        BattleGroupSnapshot first = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "army_1:hero");
        BattleGroupSnapshot second = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "army_2:hero");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        AssertEqual("enemy_deployment_north", first.Plan.ObjectiveZoneId, "first player company objective zone");
        AssertEqual(BattleEngagementRule.MoveFirst, first.Plan.EngagementRule, "first player company rule");
        AssertEqual("enemy_deployment_south", second.Plan.ObjectiveZoneId, "second player company objective zone");
        AssertEqual(BattleEngagementRule.Hold, second.Plan.EngagementRule, "second player company rule");
        AssertEqual("", enemy.Plan.ObjectiveZoneId, "enemy group should not inherit player company plans");
    }

    internal static void BattleGroupSessionProbeAppliesEnemyObjectivePlans()
    {
        BattleStartRequest request = new()
        {
            RequestId = "request_enemy_plan_groups",
            ContextId = "battle_enemy_plan_groups",
            TargetSiteId = "site_1"
        };
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "force_player",
            UnitDefinitionId = "player_corps",
            FactionId = "player",
            Count = 1
        });
        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "enemy_army:hero",
            SourceKind = "EnemyArmy",
            SourceId = "enemy_army",
            UnitDefinitionId = "enemy_corps",
            FactionId = "enemy",
            Count = 1
        });
        request.EnemyBattleGroupPlans["EnemyArmy:enemy_army"] = new BattleGroupPlanSnapshot
        {
            ObjectiveZoneId = "player_deployment_west_1",
            EngagementRule = BattleEngagementRule.MoveFirst,
            InitialFormationId = "default_line",
            HasObjectiveAnchor = true,
            ObjectiveCellX = 1,
            ObjectiveCellY = 0,
            ObjectiveWidth = 3,
            ObjectiveHeight = 2
        };

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "probe snapshot preparation should accept enemy-side objective plans");
        BattleGroupSnapshot player = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_army:hero");
        AssertEqual("", player.Plan.ObjectiveZoneId, "player group should not inherit enemy objective plan");
        AssertEqual("player_deployment_west_1", enemy.Plan.ObjectiveZoneId, "enemy group objective zone");
        AssertEqual(BattleEngagementRule.MoveFirst, enemy.Plan.EngagementRule, "enemy group rule");
        AssertTrue(enemy.Plan.HasObjectiveAnchor, "enemy direct sortie plan should carry an objective anchor");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int playerStrength,
        int enemyStrength,
        int enemyCellX = 6,
        int enemyCellY = 0)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_player",
                    FactionId = "player",
                    SourceForceId = "force_player",
                    HeroId = "hero_player",
                    HeroDefinitionId = "hero_def_player",
                    CorpsId = "corps_player",
                    CorpsDefinitionId = "player_corps",
                    CorpsStrength = playerStrength,
                    SourceLocationId = "city_player",
                    CellX = 0,
                    CellY = 0
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy",
                    HeroId = "hero_enemy",
                    HeroDefinitionId = "hero_def_enemy",
                    CorpsId = "corps_enemy",
                    CorpsDefinitionId = "enemy_corps",
                    CorpsStrength = enemyStrength,
                    SourceLocationId = "site_1",
                    CellX = enemyCellX,
                    CellY = enemyCellY
                }
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot);
        return snapshot;
    }

    private static BattleStartSnapshot BuildBeaconCommandSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildBeaconGroup("group_player_a", "player", "force_player_a", 0, 0),
                BuildBeaconGroup("group_player_b", "player", "force_player_b", 0, 2),
                BuildBeaconGroup("group_player_c", "player", "force_player_c", 0, 4),
                BuildBeaconGroup("group_enemy", "enemy", "force_enemy", 8, 2, 200)
            }
        };

        for (int x = 0; x <= 8; x++)
        {
            for (int y = 0; y <= 4; y++)
            {
                snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSplitTopologyBeaconSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildBeaconGroup("group_player_a", "player", "force_player_a", 0, 0),
                BuildBeaconGroup("group_player_b", "player", "force_player_b", 10, 0),
                BuildBeaconGroup("group_enemy", "enemy", "force_enemy", 12, 0, 200)
            }
        };
        for (int x = 0; x <= 2; x++)
        {
            snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = x,
                Y = 0,
                Height = 0,
                MoveCost = 1
            });
        }

        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot { X = 10, Y = 0, Height = 0, MoveCost = 1 });
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot { X = 12, Y = 0, Height = 0, MoveCost = 1 });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildBeaconGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints = 80)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = 1,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            CellHeight = 0
        };
    }

    private static void SetPlanInitialDestinationBeacon(
        BattleGroupPlanSnapshot plan,
        int x,
        int y,
        int height)
    {
        SetRequiredPlanProperty(plan, "HasInitialDestinationBeacon", true);
        SetRequiredPlanProperty(plan, "InitialDestinationCellX", x);
        SetRequiredPlanProperty(plan, "InitialDestinationCellY", y);
        SetRequiredPlanProperty(plan, "InitialDestinationCellHeight", height);
    }

    private static void SetRequiredPlanProperty<T>(
        BattleGroupPlanSnapshot plan,
        string propertyName,
        T value)
    {
        System.Reflection.PropertyInfo property = typeof(BattleGroupPlanSnapshot).GetProperty(propertyName);
        AssertTrue(property != null, $"BattleGroupPlanSnapshot should expose {propertyName}");
        property!.SetValue(plan, value);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
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
}
