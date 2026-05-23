using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleMovementIntentRegressionCases
{
    public static void RuntimeCanSwitchAssaultTargetForFasterAttackOpportunity()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());

        _ = new BattleRuntimeSession(executor).RunMinimal(BuildReroutePastSecondaryTargetSnapshot());

        string[] playerTargets = executor.SeenFacts
            .Where(item => item.ActorId == "force_player:1" && item.HasTarget)
            .Take(4)
            .Select(item => item.TargetActorId)
            .ToArray();

        AssertTrue(playerTargets.Length >= 4, "player actor should keep receiving combat decisions while rerouting");
        AssertTrue(
            playerTargets.All(item => item == "enemy_z:1"),
            $"assault movement should switch to the faster attack opportunity while rerouting: actual=[{string.Join(",", playerTargets)}]");
    }

    public static void RuntimeSupportUnitDoesNotMoveAwayFromEngagedTargetForFarFlank()
    {
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(BuildEngagedTargetFarFlankSnapshot());

        BattleEvent? backlineMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "backline:1" &&
            item.Kind == BattleEventKind.MovementCompleted);

        AssertTrue(backlineMove != null, "backline should move toward a support position instead of idling");
        AssertTrue(
            backlineMove!.ToGridX == 2 && backlineMove.ToGridY == 1,
            $"backline should take the nearer orthogonal support step when an ally already engages the target: actual=({backlineMove.ToGridX},{backlineMove.ToGridY})");
    }

    public static void RuntimeAssaultTargetSelectionPrefersFastestAttackOpportunity()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(BuildAdjacentOpportunityBeatsRetainedTargetSnapshot());
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "force_player:1");
        player.TargetActorId = "enemy_far:1";

        BattleRuntimeAdvanceResult advance = controller.AdvanceNextTick();

        BattleEvent? firstDamage = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.ActorId == "force_player:1");
        AssertTrue(firstDamage != null, "player should attack the immediately available enemy instead of walking toward retained distant target");
        AssertTrue(
            firstDamage!.TargetId == "enemy_near:1",
            $"default assault target should be the fastest attack opportunity: actual={firstDamage.TargetId}");
    }

    public static void RuntimeTargetChoiceUsesReachableFootprintAttackSlots()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());

        _ = new BattleRuntimeSession(executor).RunMinimal(BuildBlockedNearLargeTargetSnapshot());

        string[] firstTargets = executor.SeenFacts
            .Where(item => item.ActorId == "force_player:1" && item.HasTarget)
            .Take(3)
            .Select(item => item.TargetActorId)
            .ToArray();

        AssertTrue(firstTargets.Length > 0, "player actor should receive target decisions");
        AssertTrue(
            firstTargets.All(item => item == "enemy_reachable:1"),
            $"ordinary assault should choose the enemy with the reachable footprint-valid attack slot: actual=[{string.Join(",", firstTargets)}]");
    }

    public static void RuntimeMoverRetargetsWhenTargetDiesBeforeMovementResolves()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildSameTickTargetDeathRetargetSnapshot())
            .AdvanceNextTick();

        BattleEvent? moverMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_mover:1");
        AssertTrue(moverMove != null, "mover should not spend its decision slice on a target killed earlier in the same tick");
        AssertTrue(
            moverMove!.TargetId == "enemy_live:1",
            $"mover should retarget the next live attack opportunity in the same tick: actual={moverMove.TargetId}");
    }

    private static BattleStartSnapshot BuildReroutePastSecondaryTargetSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_movement_intent_reroute",
            BattleId = "battle_movement_intent_reroute",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 0, 400),
                BuildGroup("group_enemy_a", "enemy", "enemy_a", 4, 0, 400, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_z", "enemy", "enemy_z", 0, 5, 400, initialCommandId: "HoldLine")
            }
        };

        AddSurface(snapshot, 0, 0);
        AddSurface(snapshot, 0, 1);
        AddSurface(snapshot, 0, 2);
        AddSurface(snapshot, 0, 3);
        AddSurface(snapshot, 0, 4);
        AddSurface(snapshot, 0, 5);
        AddSurface(snapshot, 1, 3);
        AddSurface(snapshot, 2, 3);
        AddSurface(snapshot, 3, 2);
        AddSurface(snapshot, 4, 1);
        AddSurface(snapshot, 4, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildEngagedTargetFarFlankSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_engaged_target_far_flank",
            BattleId = "battle_engaged_target_far_flank",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_frontline", "player", "frontline", 1, 0, 400),
                BuildGroup("group_backline", "player", "backline", 3, 2, 400),
                BuildGroup("group_enemy", "enemy", "enemy", 0, 0, 400, initialCommandId: "HoldLine")
            }
        };

        AddSurface(snapshot, 0, 0);
        AddSurface(snapshot, 1, 0);
        AddSurface(snapshot, 3, 2);
        AddSurface(snapshot, 3, 1);
        AddSurface(snapshot, 2, 1);
        AddSurface(snapshot, 2, 2);
        AddSurface(snapshot, 4, 1);
        AddSurface(snapshot, 3, 0);
        AddSurface(snapshot, 2, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildAdjacentOpportunityBeatsRetainedTargetSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_fastest_attack_opportunity",
            BattleId = "battle_fastest_attack_opportunity",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 0, 80),
                BuildGroup("group_enemy_far", "enemy", "enemy_far", 5, 0, 80, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_near", "enemy", "enemy_near", 0, 1, 80, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 5; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        AddSurface(snapshot, 0, 1);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildBlockedNearLargeTargetSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_blocked_near_large_target",
            BattleId = "battle_blocked_near_large_target",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 2, 200),
                BuildGroup("group_player_blocker", "player", "player_blocker", 0, 1, 200, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_blocked", "enemy", "enemy_blocked", 0, 0, 200, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_reachable", "enemy", "enemy_reachable", 0, 4, 200, initialCommandId: "HoldLine")
            }
        };

        for (int x = -1; x <= 1; x++)
        {
            for (int y = 0; y <= 4; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSameTickTargetDeathRetargetSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_same_tick_target_death_retarget",
            BattleId = "battle_same_tick_target_death_retarget",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_killer", "player", "player_killer", 2, 0, 80),
                BuildGroup("group_player_mover", "player", "player_mover", 0, 2, 80),
                BuildGroup("group_enemy_weak", "enemy", "enemy_weak", 3, 0, 1, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_live", "enemy", "enemy_live", 6, 2, 80, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 6; x++)
        {
            for (int y = 0; y <= 2; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "",
        int footprintWidth = 1,
        int footprintHeight = 1)
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
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight
        };
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

    private sealed class RecordingBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
    {
        private readonly IBattleRuntimeAiExecutor _inner;

        public RecordingBattleRuntimeAiExecutor(IBattleRuntimeAiExecutor inner)
        {
            _inner = inner;
        }

        public List<BattleRuntimeAiDecisionFacts> SeenFacts { get; } = new();

        public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
        {
            SeenFacts.Add(facts);
            return _inner.ChooseAction(facts);
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
