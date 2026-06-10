using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleCombatZoneRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime builds global combat zones and group action zones with area logs", RuntimeBuildsGlobalCombatZonesAndGroupActionZonesWithAreaLogs);
        run("runtime rear engaged members use combat zone intent instead of objective advance", RuntimeRearEngagedMembersUseCombatZoneIntentInsteadOfObjectiveAdvance);
        run("runtime combat-zone outsiders path to latest combat zone before slot search", RuntimeCombatZoneOutsidersPathToLatestCombatZoneBeforeSlotSearch);
        run("runtime combat-zone join movement uses action bounds instead of center", RuntimeCombatZoneJoinMovementUsesActionBoundsInsteadOfCenter);
        run("runtime combat-zone join movement keeps centered goal near map edge", RuntimeCombatZoneJoinMovementKeepsCenteredGoalNearMapEdge);
        run("runtime members inside actor-local combat zone do not march to selected group zone", RuntimeMembersInsideActorLocalCombatZoneDoNotMarchToSelectedGroupZone);
        run("runtime player combat-zone overlap promotes group action zone", RuntimePlayerCombatZoneOverlapPromotesGroupActionZone);
        run("runtime combat zone keeps member footprints and hot-area padding under cap pressure", RuntimeCombatZoneKeepsMemberFootprintsAndHotAreaPaddingUnderCapPressure);
    }

    private static void RuntimeBuildsGlobalCombatZonesAndGroupActionZonesWithAreaLogs()
    {
        BattleStartSnapshot snapshot = BuildCombatZoneSnapshot("battle_global_combat_zone_logs");
        string previousLog = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        _ = controller.AdvanceNextTick();

        BattleCombatZoneSnapshot zone = controller.State.CombatZones.Values.Single();
        BattleGroupActionZoneSnapshot enemyActionZone = controller.State.GroupActionZones["enemy_group"];
        BattleGroupActionZoneSnapshot playerActionZone = controller.State.GroupActionZones["player_group"];
        string log = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        string newLog = log.Length >= previousLog.Length ? log[previousLog.Length..] : log;

        AssertTrue(string.IsNullOrWhiteSpace(zone.OwnerBattleGroupId), "combat zone must be global and unowned");
        AssertTrue(zone.MinCellX <= 2 && zone.MaxCellX >= 9, $"combat zone should include front contact and rear join space: bounds=({zone.MinCellX},{zone.MinCellY})-({zone.MaxCellX},{zone.MaxCellY})");
        AssertEqual(BattleGroupActionZoneKind.CombatJoin, enemyActionZone.Kind, "enemy group action zone kind");
        AssertEqual(BattleGroupActionZoneKind.CombatJoin, playerActionZone.Kind, "player group action zone kind");
        AssertEqual(zone.CombatZoneId, enemyActionZone.TargetCombatZoneId, "enemy action zone should target the global combat zone");
        AssertEqual(zone.CombatZoneId, playerActionZone.TargetCombatZoneId, "player action zone should target the global combat zone");

        AssertTrue(newLog.Contains("BattleAreaSnapshot battle=battle_global_combat_zone_logs", StringComparison.Ordinal), "area snapshot log should be emitted");
        AssertTrue(newLog.Contains("reason=combat_zone_rebuilt", StringComparison.Ordinal), "combat-zone rebuild reason should be logged");
        AssertTrue(newLog.Contains("BattleCombatZoneSnapshot", StringComparison.Ordinal), "combat-zone bounds should be logged");
        AssertTrue(newLog.Contains("BattleDeploymentZoneSnapshot", StringComparison.Ordinal), "deployment/objective zones should be logged");
        AssertTrue(newLog.Contains("BattleGroupActionZoneSnapshot", StringComparison.Ordinal), "group action zones should be logged");
        AssertTrue(newLog.Contains("BattleUnitPositionSnapshot", StringComparison.Ordinal), "unit positions should be logged");
        AssertTrue(newLog.Contains("actor=enemy_rear:1", StringComparison.Ordinal), "rear unit should be visible in the area snapshot");
        AssertTrue(newLog.Contains("bounds=(", StringComparison.Ordinal) && newLog.Contains(")-(", StringComparison.Ordinal), "logs should use from-to bounds");
    }

    private static void RuntimeRearEngagedMembersUseCombatZoneIntentInsteadOfObjectiveAdvance()
    {
        BattleStartSnapshot snapshot = BuildCombatZoneSnapshot("battle_rear_uses_combat_zone_intent");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor enemyRear = controller.State.Actors.Single(item => item.ActorId == "enemy_rear:1");

        BattleEvent? objectiveMove = tick.Events.FirstOrDefault(item =>
            item.ActorId == "enemy_rear:1" &&
            item.Kind == BattleEventKind.MovementStarted &&
            item.TargetId == "player_deployment" &&
            item.ReasonCode == "plan_objective_advance");
        BattleEvent? combatMove = tick.Events.FirstOrDefault(item =>
            item.ActorId == "enemy_rear:1" &&
            item.Kind == BattleEventKind.MovementStarted &&
            item.TargetId != "player_deployment");

        AssertTrue(objectiveMove == null, "rear member in an engaged combat zone must not continue ordinary objective advance");
        AssertTrue(
            combatMove != null ||
            enemyRear.PlanState == BattleGroupPlanRuntimeState.TargetLocked ||
            enemyRear.PlanState == BattleGroupPlanRuntimeState.MovingToAttackSlot ||
            enemyRear.LastAdvanceFailureReason == "reject_no_reachable_slot" ||
            enemyRear.LastAdvanceFailureReason == BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot,
            $"rear member should receive combat-zone intent or named blocked-entry diagnostics: plan={enemyRear.PlanState} failure={enemyRear.LastAdvanceFailureReason}");
    }

    private static void RuntimeCombatZoneKeepsMemberFootprintsAndHotAreaPaddingUnderCapPressure()
    {
        BattleStartSnapshot snapshot = BuildCombatZonePaddingSnapshot("battle_combat_zone_padding");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        _ = controller.AdvanceNextTick();

        BattleCombatZoneSnapshot zone = controller.State.CombatZones.Values.Single();

        AssertTrue(zone.ActorIds.Count == 8, $"all clustered combat participants should remain in one zone: count={zone.ActorIds.Count}");
        AssertTrue(zone.MinCellX <= 30, $"combat zone should preserve west hot-area padding: minX={zone.MinCellX}");
        AssertTrue(zone.MinCellY <= 14, $"combat zone should preserve north hot-area padding: minY={zone.MinCellY}");
        AssertTrue(zone.MaxCellX >= 45, $"combat zone should preserve east hot-area padding around rear 2x2 units: maxX={zone.MaxCellX}");
        AssertTrue(zone.MaxCellY >= 25, $"combat zone should preserve south hot-area padding around rear 2x2 units: maxY={zone.MaxCellY}");
        AssertTrue(
            zone.ActorIds.Contains("enemy_rear_top:1") &&
            zone.ActorIds.Contains("enemy_rear_bottom:1"),
            "rear actors should be listed inside the same combat zone whose bounds cover their footprints");
    }

    private static void RuntimeCombatZoneOutsidersPathToLatestCombatZoneBeforeSlotSearch()
    {
        BattleStartSnapshot snapshot = BuildSpreadCombatZoneJoinSnapshot("battle_spread_join_paths_to_combat_zone");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupActionZoneSnapshot playerActionZone = controller.State.GroupActionZones["player_group"];
        BattleRuntimeActor playerRear = controller.State.Actors.Single(item => item.ActorId == "player_rear:1");
        BattleEvent? rearMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_rear:1");

        AssertEqual(BattleGroupActionZoneKind.CombatJoin, playerActionZone.Kind, "player group should be joining the fresh combat zone");
        AssertTrue(
            !IsInsideActionZone(playerRear, playerActionZone),
            "fixture requires the rear member to remain outside the current combat zone at the decision boundary");
        AssertTrue(rearMove != null, "combat-zone outsider should path toward the current combat zone instead of idling");
        AssertEqual(
            $"{playerActionZone.BattleGroupId}:combat_join:{playerActionZone.TargetCombatZoneId}",
            rearMove!.TargetId,
            "combat-zone outsider movement target should be the latest combat-zone join region, not an enemy actor or objective");
        AssertEqual("combat_zone_join_advance", rearMove.ReasonCode, "combat-zone outsider movement reason");
    }

    private static void RuntimeCombatZoneJoinMovementUsesActionBoundsInsteadOfCenter()
    {
        BattleStartSnapshot snapshot = BuildCombatZoneJoinBoundsSnapshot("battle_combat_join_uses_action_bounds");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupActionZoneSnapshot enemyActionZone = controller.State.GroupActionZones["enemy_group"];
        BattleRuntimeActor outsider = controller.State.Actors.Single(item => item.ActorId == "enemy_outsider:1");
        BattleEvent? outsiderMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_outsider:1");

        AssertEqual(BattleGroupActionZoneKind.CombatJoin, enemyActionZone.Kind, "enemy group should be joining the existing combat zone");
        AssertTrue(
            !IsInsideActionZone(outsider, enemyActionZone),
            "fixture requires the outsider to stand outside the logged action-zone bounds");
        AssertTrue(
            outsiderMove != null,
            $"outsider near the right edge should move into the logged action-zone bounds instead of reporting path failure: failure={outsider.LastAdvanceFailureReason}");
        AssertEqual("combat_zone_join_advance", outsiderMove!.ReasonCode, "combat-zone join movement reason");
        AssertTrue(
            outsiderMove.ToGridX < outsiderMove.FromGridX,
            $"outsider should step toward the action-zone min/max bounds, not treat the center as a top-left goal: from=({outsiderMove.FromGridX},{outsiderMove.FromGridY}) to=({outsiderMove.ToGridX},{outsiderMove.ToGridY}) bounds=({enemyActionZone.MinCellX},{enemyActionZone.MinCellY})-({enemyActionZone.MaxCellX},{enemyActionZone.MaxCellY})");
    }

    private static void RuntimeCombatZoneJoinMovementKeepsCenteredGoalNearMapEdge()
    {
        BattleGroupActionZoneSnapshot actionZone = new()
        {
            BattleGroupId = "player_group",
            Kind = BattleGroupActionZoneKind.CombatJoin,
            TargetCombatZoneId = "combat_zone_1",
            MinCellX = 42,
            MinCellY = 0,
            MaxCellX = 56,
            MaxCellY = 18,
            CenterCellX = 49,
            CenterCellY = 9,
            CenterCellHeight = 0
        };

        Type plannerType = typeof(BattleRuntimeSession).Assembly.GetType("Rpg.Runtime.Battle.BattleCombatJoinRegionPlanner")
            ?? throw new InvalidOperationException("BattleCombatJoinRegionPlanner type not found");
        System.Reflection.MethodInfo method = plannerType.GetMethod(
            "BuildMovementGoal",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildMovementGoal method not found");
        BattleRegionMovementGoal goal = (BattleRegionMovementGoal)method.Invoke(null, new object[] { actionZone })!;

        AssertEqual(49, goal.CenterCellX, "combat join goal should preserve the action-zone center x");
        AssertEqual(9, goal.CenterCellY, "combat join goal should preserve the action-zone center y");
        AssertEqual(15, goal.Width, "combat join goal width");
        AssertEqual(19, goal.Height, "combat join goal height");
    }

    private static void RuntimeMembersInsideActorLocalCombatZoneDoNotMarchToSelectedGroupZone()
    {
        BattleStartSnapshot snapshot = BuildSplitCombatZoneSnapshot("battle_split_actor_local_zone");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleCombatZoneSnapshot[] zones = controller.State.CombatZones.Values
            .OrderBy(item => item.CombatZoneId, StringComparer.Ordinal)
            .ToArray();
        BattleGroupActionZoneSnapshot playerActionZone = controller.State.GroupActionZones["player_group"];
        BattleCombatZoneSnapshot playerRightZone = zones.Single(zone => zone.ActorIds.Contains("player_right:1"));
        BattleRuntimeActor playerRight = controller.State.Actors.Single(item => item.ActorId == "player_right:1");
        BattleEvent? rightMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_right:1");
        string selectedJoinRegionId = $"{playerActionZone.BattleGroupId}:combat_join:{playerActionZone.TargetCombatZoneId}";

        AssertEqual(2, zones.Length, "fixture should produce two simultaneous global combat zones");
        AssertEqual(BattleGroupActionZoneKind.CombatJoin, playerActionZone.Kind, "player group should be engaged through one selected action zone");
        AssertTrue(
            !string.Equals(playerRightZone.CombatZoneId, playerActionZone.TargetCombatZoneId, StringComparison.Ordinal),
            "fixture requires the right-side actor to stand in a non-selected combat zone");
        AssertTrue(
            IsInsideCombatZone(playerRight, playerRightZone),
            "right-side actor should be inside its actor-local combat-zone bounds");
        AssertTrue(
            rightMove == null || !string.Equals(rightMove.TargetId, selectedJoinRegionId, StringComparison.Ordinal),
            $"actor already inside a combat zone must not march to the selected group action zone: target={rightMove?.TargetId} selected={selectedJoinRegionId}");
        AssertTrue(
            string.Equals(playerRight.TargetActorId, "enemy_right:1", StringComparison.Ordinal) ||
            string.Equals(rightMove?.TargetId, "enemy_right:1", StringComparison.Ordinal),
            $"actor inside a non-selected combat zone should make local combat decisions against that zone's enemy: target={playerRight.TargetActorId} moveTarget={rightMove?.TargetId}");
    }

    private static void RuntimePlayerCombatZoneOverlapPromotesGroupActionZone()
    {
        BattleStartSnapshot snapshot = BuildCombatZoneOverlapEngagementSnapshot("battle_overlap_promotes_group_action");
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor playerFlank = controller.State.Actors.Single(item => item.ActorId == "player_flank:1");
        BattleRuntimeActor enemyBoss = controller.State.Actors.Single(item => item.ActorId == "enemy_boss:1");
        playerFlank.TargetActorId = enemyBoss.ActorId;
        playerFlank.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleCombatZoneSnapshot zone = controller.State.CombatZones.Values.Single();
        BattleGroupTacticalState playerState = controller.State.TacticalStates["player_group"];
        BattleGroupActionZoneSnapshot playerActionZone = controller.State.GroupActionZones["player_group"];
        BattleEvent? engagement = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "player_group");

        AssertTrue(zone.HasCloseHostileContact, "fixture should contain a real local fight as the active combat zone anchor");
        AssertTrue(zone.ActorIds.Contains(playerFlank.ActorId), "target-linked player should be listed in the active combat zone");
        AssertTrue(zone.ActorIds.Contains(enemyBoss.ActorId), "target-linked hostile should be listed in the active combat zone");
        AssertEqual(BattleGroupEngagementState.Engaged, playerState.EngagementState, "combat-zone overlap should enter player-scoped engagement");
        AssertEqual(BattleGroupActionZoneKind.CombatJoin, playerActionZone.Kind, "combat-zone overlap should publish combat-join intent");
        AssertEqual(zone.CombatZoneId, playerActionZone.TargetCombatZoneId, "player action zone should target the overlapping combat zone");
        AssertEqual(BattleGroupTacticalReasonCode.EngagementEnterCombatZoneOverlap, engagement?.ReasonCode, "engagement reason");
    }

    private static BattleStartSnapshot BuildCombatZoneSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 1,
                    Width = 2,
                    Height = 2
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 11,
                    CellY = 1,
                    Width = 2,
                    Height = 2
                }
            },
            BattleGroups =
            {
                BuildGroup("player_group", "player", "player_front", 2, 1, "enemy_deployment"),
                BuildGroup("player_group", "player", "player_rear", 0, 1, "enemy_deployment"),
                BuildGroup("enemy_group", "enemy", "enemy_front", 6, 1, "player_deployment", BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_group", "enemy", "enemy_rear", 9, 1, "player_deployment", BattleGroupTacticalMode.EnemyOffense)
            }
        };

        for (int x = 0; x <= 12; x++)
        {
            for (int y = 0; y <= 3; y++)
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

    private static BattleStartSnapshot BuildCombatZonePaddingSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 16,
                    Width = 2,
                    Height = 8
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 51,
                    CellY = 15,
                    Width = 20,
                    Height = 12
                }
            },
            BattleGroups =
            {
                BuildGroup("player_group", "player", "player_top", 33, 17, "enemy_deployment", footprintWidth: 2, footprintHeight: 1),
                BuildGroup("player_group", "player", "player_front_top", 33, 18, "enemy_deployment", footprintWidth: 2, footprintHeight: 1),
                BuildGroup("player_group", "player", "player_front_mid", 33, 20, "enemy_deployment", footprintWidth: 2, footprintHeight: 1),
                BuildGroup("player_group", "player", "player_front_bottom", 33, 21, "enemy_deployment", footprintWidth: 2, footprintHeight: 1),
                BuildGroup("enemy_group", "enemy", "enemy_front_top", 37, 19, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2),
                BuildGroup("enemy_group", "enemy", "enemy_front_bottom", 37, 21, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2),
                BuildGroup("enemy_group", "enemy", "enemy_rear_top", 41, 19, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2),
                BuildGroup("enemy_group", "enemy", "enemy_rear_bottom", 41, 21, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2)
            }
        };

        for (int x = 0; x <= 60; x++)
        {
            for (int y = 10; y <= 30; y++)
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

    private static BattleStartSnapshot BuildSpreadCombatZoneJoinSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 8,
                    Width = 2,
                    Height = 3
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 18,
                    CellY = 4,
                    Width = 2,
                    Height = 3
                }
            },
            BattleGroups =
            {
                BuildGroup("player_group", "player", "player_front", 5, 5, "enemy_deployment"),
                BuildGroup("player_group", "player", "player_rear", 0, 10, "enemy_deployment"),
                BuildGroup("enemy_group", "enemy", "enemy_front", 8, 5, "player_deployment", BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_group", "enemy", "enemy_rear", 18, 10, "player_deployment", BattleGroupTacticalMode.EnemyOffense)
            }
        };

        for (int x = 0; x <= 20; x++)
        {
            for (int y = 0; y <= 12; y++)
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

    private static BattleStartSnapshot BuildCombatZoneJoinBoundsSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 16,
                    Width = 2,
                    Height = 4
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 56,
                    CellY = 2,
                    Width = 12,
                    Height = 8
                }
            },
            BattleGroups =
            {
                BuildGroup("player_front_top_group", "player", "player_front_top", 33, 14, "enemy_deployment", footprintWidth: 2, footprintHeight: 1, runtimeCommanderGroupId: "player_group"),
                BuildGroup("player_front_mid_group", "player", "player_front_mid", 33, 16, "enemy_deployment", footprintWidth: 2, footprintHeight: 1, runtimeCommanderGroupId: "player_group"),
                BuildGroup("player_front_bottom_group", "player", "player_front_bottom", 33, 19, "enemy_deployment", footprintWidth: 2, footprintHeight: 1, runtimeCommanderGroupId: "player_group"),
                BuildGroup("enemy_front_top_group", "enemy", "enemy_front_top", 35, 18, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_group"),
                BuildGroup("enemy_front_bottom_group", "enemy", "enemy_front_bottom", 33, 20, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_group"),
                BuildGroup("enemy_outsider_group", "enemy", "enemy_outsider", 43, 18, "player_deployment", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_group")
            }
        };

        for (int x = 20; x <= 50; x++)
        {
            for (int y = 8; y <= 30; y++)
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

    private static BattleStartSnapshot BuildSplitCombatZoneSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 9,
                    Width = 2,
                    Height = 4
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 40,
                    CellY = 9,
                    Width = 2,
                    Height = 4
                }
            },
            BattleGroups =
            {
                BuildGroup("player_group", "player", "player_left_top", 10, 10, "enemy_deployment"),
                BuildGroup("player_group", "player", "player_left_bottom", 10, 12, "enemy_deployment"),
                BuildGroup("player_group", "player", "player_right", 30, 10, "enemy_deployment"),
                BuildGroup("enemy_group", "enemy", "enemy_left_top", 13, 10, "player_deployment", BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_group", "enemy", "enemy_left_bottom", 13, 12, "player_deployment", BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_group", "enemy", "enemy_right", 33, 10, "player_deployment", BattleGroupTacticalMode.EnemyOffense)
            }
        };

        for (int x = 0; x <= 42; x++)
        {
            for (int y = 6; y <= 16; y++)
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

    private static BattleStartSnapshot BuildCombatZoneOverlapEngagementSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 0,
                    CellY = 0,
                    Width = 2,
                    Height = 2
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "enemy",
                    CellX = 20,
                    CellY = 0,
                    Width = 2,
                    Height = 2
                }
            },
            BattleGroups =
            {
                BuildGroup("player_group", "player", "player_flank", 0, 0, "enemy_deployment"),
                BuildGroup("player_anchor_group", "player", "player_anchor", 18, 0, "enemy_deployment"),
                BuildGroup("enemy_group", "enemy", "enemy_boss", 20, 0, "player_deployment", BattleGroupTacticalMode.EnemyOffense)
            }
        };

        for (int x = 0; x <= 20; x++)
        {
            snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = x,
                Y = 0,
                Height = 0,
                MoveCost = 1
            });
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string commanderGroupId,
        string factionId,
        string sourceForceId,
        int x,
        int y,
        string objectiveZoneId,
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded,
        int footprintWidth = 1,
        int footprintHeight = 1,
        string? runtimeCommanderGroupId = null)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = commanderGroupId,
            RuntimeCommanderGroupId = runtimeCommanderGroupId ?? commanderGroupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = 120,
            MaxHitPoints = 120,
            AttackDamage = 10,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = x,
            CellY = y,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight,
            TacticalMode = tacticalMode,
            Plan = new BattleGroupPlanSnapshot
            {
                ObjectiveZoneId = objectiveZoneId,
                EngagementRule = BattleEngagementRule.AttackFirst
            }
        };
    }

    private static bool IsInsideActionZone(
        BattleRuntimeActor actor,
        BattleGroupActionZoneSnapshot actionZone)
    {
        int width = Math.Max(1, actor.FootprintWidth);
        int height = Math.Max(1, actor.FootprintHeight);
        for (int y = actor.GridY; y < actor.GridY + height; y++)
        {
            for (int x = actor.GridX; x < actor.GridX + width; x++)
            {
                if (x >= actionZone.MinCellX &&
                    x <= actionZone.MaxCellX &&
                    y >= actionZone.MinCellY &&
                    y <= actionZone.MaxCellY &&
                    actor.GridHeight == actionZone.CenterCellHeight)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideCombatZone(
        BattleRuntimeActor actor,
        BattleCombatZoneSnapshot combatZone)
    {
        int width = Math.Max(1, actor.FootprintWidth);
        int height = Math.Max(1, actor.FootprintHeight);
        for (int y = actor.GridY; y < actor.GridY + height; y++)
        {
            for (int x = actor.GridX; x < actor.GridX + width; x++)
            {
                if (x >= combatZone.MinCellX &&
                    x <= combatZone.MaxCellX &&
                    y >= combatZone.MinCellY &&
                    y <= combatZone.MaxCellY &&
                    actor.GridHeight == combatZone.CenterCellHeight)
                {
                    return true;
                }
            }
        }

        return false;
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
