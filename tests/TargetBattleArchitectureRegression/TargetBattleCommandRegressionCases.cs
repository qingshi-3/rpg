using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleCommandRegressionCases
{
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
