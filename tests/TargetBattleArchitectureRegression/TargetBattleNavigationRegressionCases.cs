using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleNavigationRegressionCases
{
    public static void RuntimeNavigationConsumesAuthoredSurfaceSnapshot()
    {
        BattleStartSnapshot snapshot = BuildLayeredSnapshot("battle_authored_surface_blocked");

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? playerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(
            playerMove == null ||
            playerMove.ToGridX != 1 ||
            playerMove.ToGridY != 0 ||
            playerMove.ToGridHeight != 0,
            "authored navigation should not let runtime walk onto a covered low surface that is absent from the snapshot");
    }

    public static void RuntimeNavigationChangesHeightOnlyThroughAuthoredConnections()
    {
        BattleStartSnapshot snapshot = BuildLayeredSnapshot("battle_authored_surface_connection");
        snapshot.LocationContext.NavigationConnections.Add(new BattleNavigationConnectionSnapshot
        {
            FromX = 0,
            FromY = 0,
            FromHeight = 0,
            ToX = 1,
            ToY = 0,
            ToHeight = 1,
            MoveCost = 1
        });

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? playerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(playerMove != null, "runtime should use an authored height connection when one exists");
        AssertEqual(0, playerMove!.FromGridHeight, "movement source height");
        AssertEqual(1, playerMove.ToGridX, "connected destination x");
        AssertEqual(0, playerMove.ToGridY, "connected destination y");
        AssertEqual(1, playerMove.ToGridHeight, "connected destination height");
    }

    private static BattleStartSnapshot BuildLayeredSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 0, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 2, 0, 1)
            }
        };

        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 1, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 0, Height = 1, MoveCost = 1 }
        });
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int cellHeight)
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
            CorpsStrength = 80,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            CellHeight = cellHeight
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
