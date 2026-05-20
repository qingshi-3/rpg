using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleMultiUnitNavigationRegressionCases
{
    public static void RuntimeManyAlliesConvergeOnSingleHoldlineEnemyWithoutOverlap()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_allies_single_enemy");
        AddGroup(snapshot, "player_top", "player", "player_top", 0, -1, 35);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 0, 0, 35);
        AddGroup(snapshot, "player_bottom", "player", "player_bottom", 0, 1, 35);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 45, initialCommandId: "HoldLine");

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many allies vs single holdline enemy should resolve instead of stalling");
        AssertActorsMoved(result, "player_top:1", "player_mid:1", "player_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("player_", StringComparison.Ordinal), expectedDirection: 1);
    }

    public static void RuntimeManyEnemiesConvergeOnSingleHoldlineDefenderWithoutOverlap()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_enemies_single_defender");
        AddGroup(snapshot, "player_anchor", "player", "player_anchor", 0, 0, 45, initialCommandId: "HoldLine");
        AddGroup(snapshot, "enemy_top", "enemy", "enemy_top", 6, -1, 35);
        AddGroup(snapshot, "enemy_mid", "enemy", "enemy_mid", 6, 0, 35);
        AddGroup(snapshot, "enemy_bottom", "enemy", "enemy_bottom", 6, 1, 35);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many enemies vs single holdline defender should resolve instead of stalling");
        AssertActorsMoved(result, "enemy_top:1", "enemy_mid:1", "enemy_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("enemy_", StringComparison.Ordinal), expectedDirection: -1);
    }

    public static void RuntimeManyVsManyOpenFieldClosesWithoutIllegalPositions()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_vs_many_open_field");
        AddGroup(snapshot, "player_top", "player", "player_top", 0, -1, 35);
        AddGroup(snapshot, "player_bottom", "player", "player_bottom", 0, 1, 35);
        AddGroup(snapshot, "enemy_top", "enemy", "enemy_top", 6, -1, 35);
        AddGroup(snapshot, "enemy_bottom", "enemy", "enemy_bottom", 6, 1, 35);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many vs many open-field battle should resolve instead of stalling");
        AssertActorsMoved(result, "player_top:1", "player_bottom:1", "enemy_top:1", "enemy_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("player_", StringComparison.Ordinal), expectedDirection: 1);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("enemy_", StringComparison.Ordinal), expectedDirection: -1);
    }

    public static void RuntimeFourVersusFourBattleDoesNotTimeoutWhileBothSidesLive()
    {
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(BuildFourVersusFourOpenFieldSnapshot());

        AssertCompletedWithoutRuntimeException(result, "4v4 open-field battle should resolve through combat instead of the runtime tick cap");
        AssertTrue(
            result.EventStream.Events.Any(item => item.Kind == BattleEventKind.DamageApplied),
            "4v4 battle should produce combat damage before completion");
    }

    public static void RuntimeSameLaneCrowdAdvancesAsChainInOneTick()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneSnapshot("battle_same_lane_chain");
        AddGroup(snapshot, "player_rear", "player", "player_rear", 0, 0, 90);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 1, 0, 90);
        AddGroup(snapshot, "player_front", "player", "player_front", 2, 0, 90);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 120, initialCommandId: "HoldLine");

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        AssertMove(tick.Events, "player_front:1", 2, 0, 3, 0);
        AssertMove(tick.Events, "player_mid:1", 1, 0, 2, 0);
        AssertMove(tick.Events, "player_rear:1", 0, 0, 1, 0);
    }

    public static void RuntimeSupportQueueAdvancesChainBehindEngagedFrontline()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneSnapshot("battle_support_chain");
        AddGroup(snapshot, "player_rear", "player", "player_rear", 2, 0, 90);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 3, 0, 90);
        AddGroup(snapshot, "player_front", "player", "player_front", 5, 0, 90);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 120, initialCommandId: "HoldLine");

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        AssertTrue(
            tick.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_front:1" &&
                item.TargetId == "enemy_anchor:1"),
            "engaged frontline should attack while support units advance behind it");
        AssertMove(tick.Events, "player_mid:1", 3, 0, 4, 0);
        AssertMove(tick.Events, "player_rear:1", 2, 0, 3, 0);
    }

    public static void RuntimeSupportUnitContinuesIntoDiagonalAttackRangeAgainstEngagedTarget()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_support_joins_engaged_target");
        AddGroup(snapshot, "player_front", "player", "player_front", 2, 0, 120);
        AddGroup(snapshot, "player_support", "player", "player_support", 5, -1, 120);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 3, 0, 220, initialCommandId: "HoldLine");

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? supportMove = result.EventStream.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == "player_support:1");
        AssertTrue(supportMove != null, "support unit should not stop outside attack range just because the target is engaged");
        AssertEqual(4, supportMove!.ToGridX, "support unit should close to diagonal attack x");
        AssertEqual(-1, supportMove.ToGridY, "support unit should close to diagonal attack y");
        AssertTrue(
            result.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_support:1" &&
                item.TargetId == "enemy_anchor:1"),
            "support unit should attack the already engaged target after reaching 8-direction range");
    }

    private static BattleStartSnapshot BuildOpenFieldSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 6; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSingleLaneSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildFourVersusFourOpenFieldSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_four_vs_four_open_field",
            BattleId = "battle_four_vs_four_open_field",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 12; x++)
        {
            for (int y = 0; y <= 5; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            AddGroup(snapshot, $"group_player_{i}", "player", $"player_{i}", 0, i, 160);
            AddGroup(snapshot, $"group_enemy_{i}", "enemy", $"enemy_{i}", 10, i, 160);
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

    private static void AddGroup(
        BattleStartSnapshot snapshot,
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "")
    {
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
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
            AttackDamage = 5,
            AttackRange = 1,
            AttackSpeed = 1.0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId
        });
    }

    private static void AssertCompletedWithoutRuntimeException(BattleRuntimeSessionResult result, string message)
    {
        AssertTrue(result.Outcome.IsComplete, $"{message}: outcome should be complete");
        AssertTrue(
            result.Outcome.TerminationReason != BattleTerminationReason.RuntimeException,
            $"{message}: termination={result.Outcome.TerminationReason}");
    }

    private static void AssertActorsMoved(BattleRuntimeSessionResult result, params string[] actorIds)
    {
        foreach (string actorId in actorIds)
        {
            AssertTrue(
                result.EventStream.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == actorId),
                $"actor should produce at least one runtime movement event: {actorId}");
        }
    }

    private static void AssertAllMovementDestinationsAreAuthored(BattleRuntimeSessionResult result, BattleStartSnapshot snapshot)
    {
        var authored = snapshot.LocationContext.NavigationSurfaces
            .Select(item => (item.X, item.Y, item.Height))
            .ToHashSet();
        foreach (BattleEvent movement in result.EventStream.Events.Where(item => item.Kind == BattleEventKind.MovementCompleted))
        {
            AssertTrue(
                authored.Contains((movement.ToGridX, movement.ToGridY, movement.ToGridHeight)),
                $"movement destination must stay on authored walkable surfaces: actor={movement.ActorId} to=({movement.ToGridX},{movement.ToGridY},{movement.ToGridHeight})");
        }
    }

    private static void AssertNoDuplicateMovementDestinationsPerTick(BattleRuntimeSessionResult result)
    {
        string[] duplicates = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.MovementCompleted)
            .GroupBy(item => $"{item.RuntimeTick}:{item.ToGridX},{item.ToGridY},{item.ToGridHeight}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        AssertEqual(0, duplicates.Length, $"same-tick movement reservations should prevent duplicate destination cells: {string.Join(",", duplicates)}");
    }

    private static void AssertNoDuplicateLivingCorpsCells(BattleRuntimeSessionResult result)
    {
        string[] duplicates = result.FinalState.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .GroupBy(item => $"{item.GridX},{item.GridY},{item.GridHeight}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        AssertEqual(0, duplicates.Length, $"living corps should not end stacked on the same cell: {string.Join(",", duplicates)}");
    }

    private static void AssertAllMovesProgressX(BattleRuntimeSessionResult result, Func<string, bool> actorFilter, int expectedDirection)
    {
        foreach (BattleEvent movement in result.EventStream.Events.Where(item => item.Kind == BattleEventKind.MovementCompleted && actorFilter(item.ActorId)))
        {
            int deltaX = movement.ToGridX - movement.FromGridX;
            AssertTrue(
                Math.Sign(deltaX) == Math.Sign(expectedDirection) || deltaX == 0,
                $"movement should not step away from the opposing side: actor={movement.ActorId} fromX={movement.FromGridX} toX={movement.ToGridX}");
        }
    }

    private static void AssertMove(
        IReadOnlyList<BattleEvent> events,
        string actorId,
        int fromX,
        int fromY,
        int toX,
        int toY)
    {
        BattleEvent? movement = events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == actorId);
        AssertTrue(movement != null, $"actor should move in this tick: {actorId}");
        AssertEqual(fromX, movement!.FromGridX, $"{actorId} movement from x");
        AssertEqual(fromY, movement.FromGridY, $"{actorId} movement from y");
        AssertEqual(toX, movement.ToGridX, $"{actorId} movement to x");
        AssertEqual(toY, movement.ToGridY, $"{actorId} movement to y");
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
