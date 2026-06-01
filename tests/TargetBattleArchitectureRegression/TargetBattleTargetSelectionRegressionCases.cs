using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleTargetSelectionRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("td002 fastest assault scoring locks lowest travel cost target", FastestAssaultScoringLocksLowestTravelCostTarget);
        run("td002 plan scoped selection ignores far global attack slot target", PlanScopedSelectionIgnoresFarGlobalAttackSlotTarget);
        run("td002 move first route blocking selection prefers blocker over closer lure", MoveFirstRouteBlockingSelectionPrefersBlockerOverCloserLure);
        run("td002 retained target stickiness keeps live retained target over closer lure", RetainedTargetStickinessKeepsLiveRetainedTargetOverCloserLure);
    }

    public static void FastestAssaultScoringLocksLowestTravelCostTarget()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildFastestAssaultSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleEvent move = FirstMovement(tick.Events, "force_player:1");
        BattleRuntimeActor player = Actor(controller, "force_player:1");

        AssertEqual("enemy_z:1", player.TargetActorId, "fastest assault target lock");
        AssertEqual("enemy_z:1", move.TargetId, "fastest assault movement target");
        // Stopwatch timing is wall-clock/non-deterministic; selecting enemy_z proves the fastest-assault branch.
    }

    public static void PlanScopedSelectionIgnoresFarGlobalAttackSlotTarget()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildPlanScopedSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor player = Actor(controller, "force_player:1");
        BattleEvent move = FirstMovement(tick.Events, "force_player:1");

        AssertEqual("enemy_local:1", player.TargetActorId, "plan-scoped target lock");
        AssertEqual("enemy_local:1", move.TargetId, "plan-scoped movement target");
        AssertEqual(0L, counters.TargetScoringElapsedTicks, "plan-scoped objective movement must not enter fastest-assault scoring");
    }

    public static void MoveFirstRouteBlockingSelectionPrefersBlockerOverCloserLure()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildRouteBlockingSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleEvent move = FirstMovement(tick.Events, "support:1");
        BattleRuntimeActor support = Actor(controller, "support:1");

        AssertEqual("enemy_blocker:1", support.TargetActorId, "route-blocking target lock");
        AssertEqual("enemy_blocker:1", move.TargetId, "route-blocking movement target");
        AssertEqual("join_blocks_objective_route", move.ReasonCode, "route-blocking movement reason");
    }

    public static void RetainedTargetStickinessKeepsLiveRetainedTargetOverCloserLure()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildRetainedTargetSnapshot());
        BattleRuntimeActor player = Actor(controller, "force_player:1");
        player.TargetActorId = "enemy_retained:1";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleEvent move = FirstMovement(tick.Events, "force_player:1");

        AssertEqual("enemy_retained:1", player.TargetActorId, "retained target lock");
        AssertEqual("enemy_retained:1", move.TargetId, "retained target movement target");
    }

    private static BattleStartSnapshot BuildFastestAssaultSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_fastest_assault",
            BattleId = "battle_td002_fastest_assault",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 0, 400),
                BuildGroup("group_enemy_a", "enemy", "enemy_a", 4, 0, 400, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_z", "enemy", "enemy_z", 0, 5, 400, initialCommandId: "HoldLine")
            }
        };

        // enemy_a is closer by grid gap, while enemy_z has the cheaper reachable attack slot.
        for (int y = 0; y <= 5; y++)
        {
            AddSurface(snapshot, 0, y);
        }

        AddSurface(snapshot, 1, 3);
        AddSurface(snapshot, 2, 3);
        AddSurface(snapshot, 3, 2);
        AddSurface(snapshot, 4, 1);
        AddSurface(snapshot, 4, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildPlanScopedSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_plan_scoped",
            BattleId = "battle_td002_plan_scoped",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    400,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_keep",
                        EngagementRule = BattleEngagementRule.AttackFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 8,
                        ObjectiveCellY = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy_local", "enemy", "enemy_local", 4, 0, 400, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_fast", "enemy", "enemy_fast", 0, 5, 400, initialCommandId: "HoldLine")
            }
        };

        // enemy_local is inside planned perception; enemy_fast would win a global travel-cost scan.
        for (int y = 0; y <= 5; y++)
        {
            AddSurface(snapshot, 0, y);
        }

        AddSurface(snapshot, 1, 0, moveCost: 8);
        AddSurface(snapshot, 2, 0, moveCost: 8);
        AddSurface(snapshot, 3, 0, moveCost: 8);
        AddSurface(snapshot, 4, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRouteBlockingSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_route_blocking",
            BattleId = "battle_td002_route_blocking",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_front", "player", "front", 3, 0, 160),
                BuildGroup(
                    "group_support",
                    "player",
                    "support",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_support",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 8,
                        ObjectiveCellY = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy_blocker", "enemy", "enemy_blocker", 4, 0, 160, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_lure", "enemy", "enemy_lure", 0, 3, 160, initialCommandId: "HoldLine")
            }
        };

        // enemy_blocker is on the objective corridor; enemy_lure is closer but off-route.
        for (int x = 0; x <= 8; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        // The side lane gives support an alternate path if the blocker is selected.
        for (int x = 0; x <= 4; x++)
        {
            AddSurface(snapshot, x, 1);
        }

        AddSurface(snapshot, 0, 2);
        AddSurface(snapshot, 0, 3);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRetainedTargetSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_retained_target",
            BattleId = "battle_td002_retained_target",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 0, 160),
                BuildGroup("group_enemy_retained", "enemy", "enemy_retained", 4, 0, 160, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_lure", "enemy", "enemy_lure", 0, 3, 160, initialCommandId: "HoldLine")
            }
        };

        // The live retained target is farther than enemy_lure but should remain sticky.
        for (int x = 0; x <= 4; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        AddSurface(snapshot, 0, 1);
        AddSurface(snapshot, 0, 2);
        AddSurface(snapshot, 0, 3);
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
        BattleGroupPlanSnapshot? plan = null)
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
            AttackRange = 1,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            Plan = plan ?? new BattleGroupPlanSnapshot()
        };
    }

    private static void AddSurface(BattleStartSnapshot snapshot, int x, int y, int moveCost = 1)
    {
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
        {
            X = x,
            Y = y,
            Height = 0,
            MoveCost = moveCost
        });
    }

    private static BattleRuntimeActor Actor(BattleRuntimeSessionController controller, string actorId)
    {
        return controller.State.Actors.Single(item => item.ActorId == actorId);
    }

    private static BattleEvent FirstMovement(IReadOnlyList<BattleEvent> events, string actorId)
    {
        BattleEvent? move = events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == actorId);
        AssertTrue(move != null, $"expected movement from {actorId}");
        return move!;
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
