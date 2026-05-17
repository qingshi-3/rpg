using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

internal static class TargetBattleFootprintRegressionCases
{
    public static void RuntimeCopiesSnapshotFootprintToCorpsActors()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_footprint_copy",
            playerFootprintWidth: 2,
            playerFootprintHeight: 3);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleRuntimeActor playerCorps = result.FinalState.Actors.Single(item => item.ActorId == "force_player:1");
        AssertEqual(2, playerCorps.FootprintWidth, "runtime corps footprint width");
        AssertEqual(3, playerCorps.FootprintHeight, "runtime corps footprint height");
    }

    public static void RuntimeFootprintRangeUsesRectangleEdges()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_footprint_attack",
            enemyCellX: 2,
            enemyCellY: 1,
            playerFootprintWidth: 2,
            playerFootprintHeight: 2);
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] combatEvents = result.EventStream.Events
            .Where(item => item.Kind is BattleEventKind.MovementCompleted or BattleEventKind.DamageApplied)
            .ToArray();

        AssertTrue(combatEvents.Length > 0, "footprint adjacency should produce combat events");
        AssertEqual(BattleEventKind.DamageApplied, combatEvents[0].Kind, "footprint edge adjacency should attack before moving");
    }

    public static void RuntimeFootprintOccupancyBlocksCoveredCells()
    {
        BattleStartSnapshot snapshot = BuildFootprintBlockSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent tickZeroLargeMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_large:1" &&
            item.Kind == BattleEventKind.MovementCompleted &&
            item.EventId.Contains(":tick_0:", StringComparison.Ordinal));
        AssertTrue(
            tickZeroLargeMove == null ||
            tickZeroLargeMove.ToGridX != 1 ||
            tickZeroLargeMove.ToGridY != 0,
            "2x2 actor must not reserve an anchor whose covered cells overlap another actor");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int enemyCellX = 6,
        int enemyCellY = 0,
        int playerFootprintWidth = 1,
        int playerFootprintHeight = 1,
        int enemyFootprintWidth = 1,
        int enemyFootprintHeight = 1)
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
                    CorpsStrength = 80,
                    SourceLocationId = "city_player",
                    CellX = 0,
                    CellY = 0,
                    FootprintWidth = playerFootprintWidth,
                    FootprintHeight = playerFootprintHeight
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
                    CorpsStrength = 80,
                    SourceLocationId = "site_1",
                    CellX = enemyCellX,
                    CellY = enemyCellY,
                    FootprintWidth = enemyFootprintWidth,
                    FootprintHeight = enemyFootprintHeight
                }
            }
        };
    }

    private static BattleStartSnapshot BuildFootprintBlockSnapshot()
    {
        return new BattleStartSnapshot
        {
            SnapshotId = "snapshot_battle_footprint_block",
            BattleId = "battle_footprint_block",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_large",
                    FactionId = "player",
                    SourceForceId = "a_large",
                    HeroId = "hero_large",
                    HeroDefinitionId = "hero_def_large",
                    CorpsId = "corps_large",
                    CorpsDefinitionId = "large_corps",
                    CorpsStrength = 80,
                    SourceLocationId = "city_1",
                    CellX = 0,
                    CellY = 0,
                    FootprintWidth = 2,
                    FootprintHeight = 2
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_blocker",
                    FactionId = "player",
                    SourceForceId = "z_blocker",
                    HeroId = "hero_blocker",
                    HeroDefinitionId = "hero_def_blocker",
                    CorpsId = "corps_blocker",
                    CorpsDefinitionId = "blocker_corps",
                    CorpsStrength = 80,
                    SourceLocationId = "city_1",
                    CellX = 2,
                    CellY = 1
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy",
                    FactionId = "enemy",
                    SourceForceId = "z_enemy",
                    HeroId = "hero_enemy",
                    HeroDefinitionId = "hero_def_enemy",
                    CorpsId = "corps_enemy",
                    CorpsDefinitionId = "enemy_corps",
                    CorpsStrength = 80,
                    SourceLocationId = "site_1",
                    CellX = 4,
                    CellY = 0
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
