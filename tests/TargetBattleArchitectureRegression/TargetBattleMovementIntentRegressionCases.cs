using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleMovementIntentRegressionCases
{
    public static void RuntimeKeepsAssaultTargetIntentWhileRerouting()
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
            playerTargets.All(item => item == "enemy_a:1"),
            $"assault movement should keep the acquired target while rerouting: actual=[{string.Join(",", playerTargets)}]");
    }

    public static void RuntimeSupportUnitDoesNotMoveAwayFromEngagedTargetForFarFlank()
    {
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(BuildEngagedTargetFarFlankSnapshot());

        BattleEvent? backlineMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "backline:1" &&
            item.Kind == BattleEventKind.MovementCompleted);

        AssertTrue(backlineMove != null, "backline should move toward a support position instead of idling");
        AssertTrue(
            backlineMove!.ToGridX == 2 && backlineMove.ToGridY == 2,
            $"backline should take the nearer support step when an ally already engages the target: actual=({backlineMove.ToGridX},{backlineMove.ToGridY})");
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
        AddSurface(snapshot, 2, 2);
        AddSurface(snapshot, 4, 1);
        AddSurface(snapshot, 3, 0);
        AddSurface(snapshot, 2, 0);
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
            MaxHitPoints = hitPoints,
            AttackDamage = 1,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId
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
