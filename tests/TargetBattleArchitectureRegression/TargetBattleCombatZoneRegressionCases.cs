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

    private static BattleGroupSnapshot BuildGroup(
        string commanderGroupId,
        string factionId,
        string sourceForceId,
        int x,
        int y,
        string objectiveZoneId,
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded,
        int footprintWidth = 1,
        int footprintHeight = 1)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = commanderGroupId,
            RuntimeCommanderGroupId = commanderGroupId,
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
