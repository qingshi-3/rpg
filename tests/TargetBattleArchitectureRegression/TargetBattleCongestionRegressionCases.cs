using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleCongestionRegressionCases
{
    public static void RuntimeBacklineAdvancesBehindBlockedFrontline()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneBlockedFrontlineSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? backlineMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_backline:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(backlineMove != null, "backline unit should still advance when the only attack anchor is occupied by a frontline ally");
        AssertEqual(1, backlineMove!.ToGridX, "backline first support step x");
        AssertEqual(0, backlineMove.ToGridY, "backline first support step y");
    }

    public static void RuntimeFutureOccupancyDoesNotForceImmediateDetour()
    {
        BattleStartSnapshot snapshot = BuildFutureBlockedDirectRouteSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? backlineMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_backline:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(backlineMove != null, "backline unit should still move toward the direct route when the blocker is not in the next cell");
        AssertEqual(1, backlineMove!.ToGridX, "future occupancy should not force a first-step detour x");
        AssertEqual(0, backlineMove.ToGridY, "future occupancy should not force a first-step detour y");
    }

    public static void RuntimeProjectedOccupancyAllowsDirectFirstStepThenReplans()
    {
        BattleStartSnapshot snapshot = BuildSoftCongestionRouteChoiceSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? backlineMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_backline:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(backlineMove != null, "backline unit should still move while future occupancy is only a soft route cost");
        AssertEqual(1, backlineMove!.ToGridX, "projected occupancy should allow the direct first step when the blocker may move");
        AssertEqual(0, backlineMove.ToGridY, "projected occupancy should not force an immediate lower-lane detour");

        BattleEvent[] invalidOverlapMoves = result.EventStream.Events
            .Where(item =>
                item.ActorId == "a_backline:1" &&
                item.Kind == BattleEventKind.MovementCompleted &&
                item.ToGridX == 3 &&
                item.ToGridY == 0)
            .ToArray();
        AssertEqual(0, invalidOverlapMoves.Length, "backline should not move onto the frontline actor's occupied combat cell");
    }

    public static void RuntimeTriesAlternateSameTickReservationCandidate()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildSameTickAlternateReservationSnapshot())
            .AdvanceNextTick();

        BattleEvent[] playerMoves = tick.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId.StartsWith("player_", StringComparison.Ordinal))
            .ToArray();
        AssertEqual(2, playerMoves.Length, "both ready movers should move when one can take a valid alternate next step");
        AssertTrue(
            playerMoves.Select(item => $"{item.ToGridX},{item.ToGridY},{item.ToGridHeight}").Distinct().Count() == 2,
            "alternate same-tick reservation should keep movers from choosing the same destination");
    }

    private static BattleStartSnapshot BuildSingleLaneBlockedFrontlineSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_single_lane_blocked_frontline",
            BattleId = "battle_single_lane_blocked_frontline",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_backline", "player", "a_backline", 0, 0, 80),
                BuildGroup("group_frontline", "player", "m_frontline", 3, 0, 80),
                BuildGroup("group_enemy", "enemy", "z_enemy", 4, 0, 80)
            }
        };

        for (int x = 0; x <= 4; x++)
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

    private static BattleStartSnapshot BuildFutureBlockedDirectRouteSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_future_blocked_direct_route",
            BattleId = "battle_future_blocked_direct_route",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_backline", "player", "a_backline", 0, 0, 80),
                BuildGroup("group_blocker", "player", "m_blocker", 2, 0, 80),
                BuildGroup("group_enemy", "enemy", "z_enemy", 5, 0, 80)
            }
        };

        AddSurface(snapshot, 0, 0);
        AddSurface(snapshot, 1, 0);
        AddSurface(snapshot, 2, 0);
        AddSurface(snapshot, 3, 0);
        AddSurface(snapshot, 4, 0);
        AddSurface(snapshot, 5, 0);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSoftCongestionRouteChoiceSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_soft_congestion_route_choice",
            BattleId = "battle_soft_congestion_route_choice",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_backline", "player", "a_backline", 0, 0, 80),
                BuildGroup("group_blocker", "player", "m_blocker", 2, 0, 80),
                BuildGroup("group_enemy", "enemy", "z_enemy", 5, 0, 80)
            }
        };

        AddSurface(snapshot, 0, 0);
        AddSurface(snapshot, 1, 0);
        AddSurface(snapshot, 2, 0);
        AddSurface(snapshot, 3, 0);
        AddSurface(snapshot, 4, 0);
        AddSurface(snapshot, 5, 0);
        AddSurface(snapshot, 0, 1);
        AddSurface(snapshot, 1, 1);
        AddSurface(snapshot, 2, 1);
        AddSurface(snapshot, 3, 1);
        AddSurface(snapshot, 4, 1);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    internal static BattleStartSnapshot BuildSameTickAlternateReservationSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_same_tick_alternate_reservation",
            BattleId = "battle_same_tick_alternate_reservation",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_top", "player", "player_top", 0, 0, 80),
                BuildGroup("group_player_bottom", "player", "player_bottom", 0, 1, 80),
                BuildGroup("group_enemy", "enemy", "enemy", 3, 0, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 3; x++)
        {
            AddSurface(snapshot, x, 0);
            AddSurface(snapshot, x, 1);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
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

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "")
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
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId
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
