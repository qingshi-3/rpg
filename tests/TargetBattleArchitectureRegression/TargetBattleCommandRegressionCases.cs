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

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().Probe(request);

        AssertTrue(result.Success, "probe should accept opposed player and enemy forces");
        BattleGroupSnapshot playerGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemyGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        AssertEqual("HoldLine", playerGroup.InitialCorpsCommandId, "player force should receive the selected initial corps command");
        AssertEqual("", enemyGroup.InitialCorpsCommandId, "enemy force should not inherit the player's initial corps command");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int playerStrength,
        int enemyStrength,
        int enemyCellX = 6,
        int enemyCellY = 0)
    {
        return new BattleStartSnapshot
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
