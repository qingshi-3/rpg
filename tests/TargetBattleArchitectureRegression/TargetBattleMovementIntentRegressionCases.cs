using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
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

    public static void RuntimeMoveFirstPlanAdvancesToObjectiveBeforeDistantEnemy()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildMoveFirstObjectiveSnapshot());
        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? plan = controller.EventStream.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupPlanAccepted &&
            item.ActorId == "force_player:1");
        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(plan != null, "runtime should emit the accepted battle-group plan before movement starts");
        AssertTrue(move != null, "move-first plan should advance toward the objective when only distant enemies are present");
        AssertTrue(
            move!.TargetId == "objective_gate" &&
            move.ReasonCode == "plan_objective_advance" &&
            move.ToGridX == 1 &&
            move.ToGridY == 0,
            $"move-first plan should step toward the objective instead of distant target: target={move.TargetId} reason={move.ReasonCode} to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeMovementStartedDoesNotPublishLookaheadCorrectionPath()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildMoveFirstObjectiveSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(move != null, "runtime should emit movement start for the move-first actor");
        System.Reflection.PropertyInfo? previewProperty = typeof(BattleEvent).GetProperty("MovementPreviewPath");
        object[] preview = ((System.Collections.IEnumerable?)previewProperty?.GetValue(move))
            ?.Cast<object>()
            .ToArray() ?? Array.Empty<object>();

        AssertTrue(
            preview.Length == 0,
            $"movement start should not publish future lookahead correction anchors because they can desync visuals from runtime authority: count={preview.Length}");
        AssertTrue(
            move!.ToGridX == 1 &&
            move.ToGridY == 0,
            $"movement event should still carry only the committed next runtime step: to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeMoveFirstPlanAdvancesAcrossLargeAuthoredTopology()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildLargeObjectiveTopologySnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(move != null, "move-first plan should find a first objective step on the full battle-site topology");
        AssertTrue(
            move!.TargetId == "enemy_deployment" &&
            move.ReasonCode == "plan_objective_advance",
            $"large topology objective movement should use the plan target: target={move.TargetId} reason={move.ReasonCode}");
    }

    public static void RuntimeObjectiveMovementUsesBoundedLocalObstacleAvoidance()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildObjectiveLocalObstacleSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");
        string eventSummary = string.Join(
            ";",
            tick.Events.Select(item => $"{item.Kind}:{item.ActorId}:{item.ReasonCode}:{item.TargetId}:({item.ToGridX},{item.ToGridY})"));

        AssertTrue(move != null, $"objective movement should not stop when a nearby authored obstacle blocks only the direct greedy step; events={eventSummary}");
        AssertTrue(
            move!.TargetId == "objective_gate" &&
            move.ReasonCode == "plan_objective_advance" &&
            move.ToGridX == 0 &&
            move.ToGridY == 0,
            $"bounded local obstacle avoidance should choose the first step around the nearby obstacle without publishing a route: target={move.TargetId} reason={move.ReasonCode} to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeObjectiveMovementFollowsStaticWallUntilRejoin()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveLongWallSnapshot());
        List<(int X, int Y)> startedMoves = new();

        for (int i = 0; i < 24 && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
            foreach (BattleEvent move in tick.Events.Where(item =>
                         item.Kind == BattleEventKind.MovementStarted &&
                         item.ActorId == "force_player:1"))
            {
                startedMoves.Add((move.ToGridX, move.ToGridY));
            }
        }

        string summary = string.Join(";", startedMoves.Select(item => $"({item.X},{item.Y})"));
        AssertTrue(startedMoves.Count >= 6, $"local steering should keep moving along the static wall instead of reporting no path: moves={summary}");
        AssertTrue(
            startedMoves[0] == (0, -1) &&
            startedMoves[1] == (0, -2) &&
            startedMoves[2] == (0, -3),
            $"local steering should keep the same obstacle-follow side before rejoining: moves={summary}");
        AssertTrue(
            startedMoves.Any(item => item.X > 0),
            $"local steering should rejoin objective progress after reaching the wall opening: moves={summary}");
    }

    public static void RuntimeObjectiveMovementStopsObstacleFollowWhenBudgetExpires()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveDeadWallSnapshot());
        List<(int X, int Y)> startedMoves = new();

        for (int i = 0; i < 32 && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
            foreach (BattleEvent move in tick.Events.Where(item =>
                         item.Kind == BattleEventKind.MovementStarted &&
                         item.ActorId == "force_player:1"))
            {
                startedMoves.Add((move.ToGridX, move.ToGridY));
            }
        }

        string summary = string.Join(";", startedMoves.Select(item => $"({item.X},{item.Y})"));
        AssertTrue(
            startedMoves.Count <= 21,
            $"local steering should not silently renew the same obstacle-follow budget against a dead wall: moves={summary}");
        AssertTrue(
            startedMoves.Count == 0 || startedMoves[^1].Y >= -21,
            $"local steering should stop or degrade after the bounded follow budget is exhausted: moves={summary}");
    }

    public static void RuntimePlanScopedMovementDoesNotScanFarAttackSlots()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildMoveFirstObjectiveSnapshot());

        _ = controller.AdvanceNextTick();

        AssertTrue(
            counters.CombatSlotScanCount == 0 &&
            counters.OpenAttackFlowFieldBuildCount == 0,
            $"objective-first movement should not scan far enemy attack slots: slotScans={counters.CombatSlotScanCount} openAttackBuilds={counters.OpenAttackFlowFieldBuildCount}");
    }

    public static void RuntimeEnemyMoveFirstPlanDoesNotScanFarAttackSlots()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildEnemyMoveFirstObjectiveSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy:1");
        AssertTrue(move != null, "enemy move-first plan should advance toward player deployment objective");
        AssertTrue(
            move!.TargetId == "player_deployment" &&
            move.ReasonCode == "plan_objective_advance",
            $"enemy plan should move by objective before sensing contact: target={move.TargetId} reason={move.ReasonCode}");
        AssertTrue(
            counters.CombatSlotScanCount == 0 &&
            counters.OpenAttackFlowFieldBuildCount == 0,
            $"enemy objective-first movement should not scan far player attack slots: slotScans={counters.CombatSlotScanCount} openAttackBuilds={counters.OpenAttackFlowFieldBuildCount}");
    }

    public static void RuntimeEnemyAttackFirstPlanSensesLocalPlayerBeforeObjective()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildEnemyAttackFirstObjectiveSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy:1");
        AssertTrue(move != null, "enemy attack-first plan should act when the player enters local perception");
        AssertTrue(
            move!.TargetId == "force_player:1" &&
            move.ReasonCode == "auto_advance",
            $"enemy attack-first plan should pursue the locally sensed player instead of ignoring contact: target={move.TargetId} reason={move.ReasonCode}");
    }

    public static void RuntimeObjectiveZonePlanResolvesAnchorFromSnapshotZone()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveZoneResolvedPlanSnapshot());
        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? plan = controller.EventStream.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupPlanAccepted &&
            item.ActorId == "force_player:1");
        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(plan != null, "runtime should accept objective-zone-only plan facts");
        AssertTrue(move != null, "objective-zone-only plan should still advance after runtime resolves the anchor");
        AssertTrue(
            plan!.TargetId == "objective_gate" &&
            move!.TargetId == "objective_gate" &&
            move.ToGridX == 1 &&
            move.ToGridY == 0,
            $"runtime should resolve the movement anchor from snapshot ObjectiveZones: plan={plan.TargetId} move={move.TargetId} to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeMoveFirstPlanSeeksEnemyAfterObjectiveReached()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildObjectiveReachedThenSeekSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(move != null, "move-first plan should not keep advancing to an already reached objective");
        AssertTrue(
            move!.TargetId == "enemy:1" &&
            move.ReasonCode == "auto_advance" &&
            move.ToGridX == 4 &&
            move.ToGridY == 0,
            $"after objective arrival, move-first plan should resume enemy pursuit: target={move.TargetId} reason={move.ReasonCode} to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeRetainedLocalTargetUsesGreedyStepWithoutTargetFlowField()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildRetainedLocalTargetGreedyStepSnapshot());
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "force_player:1");
        player.TargetActorId = "enemy:1";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");
        AssertTrue(move != null, "retained local target pursuit should still move toward the sensed enemy");
        AssertTrue(
            move!.TargetId == "enemy:1" &&
            move.ToGridX == 1 &&
            move.ToGridY == 1,
            $"retained local target should use the best legal neighboring step: target={move.TargetId} to=({move.ToGridX},{move.ToGridY})");
        AssertTrue(
            counters.FlowFieldBuildCount == 0 &&
            counters.OpenAttackFlowFieldBuildCount == 0,
            $"retained local target pursuit should not build target/open-attack flow fields: flowBuilds={counters.FlowFieldBuildCount} openAttackBuilds={counters.OpenAttackFlowFieldBuildCount}");
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

    internal static BattleStartSnapshot BuildSameTickTargetDeathRetargetSnapshot()
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

    private static BattleStartSnapshot BuildMoveFirstObjectiveSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_move_first_objective",
            BattleId = "battle_move_first_objective",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 3,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 7, 0, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildObjectiveLocalObstacleSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_objective_local_obstacle",
            BattleId = "battle_objective_local_obstacle",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    1,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 4,
                        ObjectiveCellY = 1,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 8, 4, 160, initialCommandId: "HoldLine")
            }
        };

        AddSurface(snapshot, 0, 1);
        for (int x = 0; x <= 4; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        AddSurface(snapshot, 3, 1);
        AddSurface(snapshot, 4, 1);
        AddSurface(snapshot, 8, 4);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildObjectiveLongWallSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_objective_long_wall",
            BattleId = "battle_objective_long_wall",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 6,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 10, 10, 160, initialCommandId: "HoldLine")
            }
        };

        for (int y = -8; y <= 0; y++)
        {
            AddSurface(snapshot, 0, y);
        }

        for (int x = 0; x <= 6; x++)
        {
            AddSurface(snapshot, x, -8);
        }

        for (int x = 2; x <= 6; x++)
        {
            for (int y = -8; y <= 0; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddSurface(snapshot, 10, 10);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildObjectiveDeadWallSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_objective_dead_wall",
            BattleId = "battle_objective_dead_wall",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 100,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 120, 120, 160, initialCommandId: "HoldLine")
            }
        };

        for (int y = -40; y <= 0; y++)
        {
            AddSurface(snapshot, 0, y);
        }

        AddSurface(snapshot, 120, 120);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildLargeObjectiveTopologySnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_large_objective_topology",
            BattleId = "battle_large_objective_topology",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    10,
                    18,
                    160,
                    footprintWidth: 2,
                    footprintHeight: 1,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "enemy_deployment",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 51,
                        ObjectiveCellY = 15,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 20,
                        ObjectiveHeight = 12
                    }),
                BuildGroup(
                    "group_enemy",
                    "enemy",
                    "enemy",
                    70,
                    20,
                    160,
                    initialCommandId: "HoldLine",
                    footprintWidth: 2,
                    footprintHeight: 2)
            }
        };

        for (int y = 0; y < 27; y++)
        {
            for (int x = 0; x < 79; x++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildEnemyMoveFirstObjectiveSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_enemy_move_first_objective",
            BattleId = "battle_enemy_move_first_objective",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 0, 160, initialCommandId: "HoldLine"),
                BuildGroup(
                    "group_enemy",
                    "enemy",
                    "enemy",
                    7,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_enemy",
                        ObjectiveZoneId = "player_deployment",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 3,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    })
            }
        };

        for (int x = 0; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildEnemyAttackFirstObjectiveSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_enemy_attack_first_objective",
            BattleId = "battle_enemy_attack_first_objective",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 4, 0, 160, initialCommandId: "HoldLine"),
                BuildGroup(
                    "group_enemy",
                    "enemy",
                    "enemy",
                    7,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_enemy",
                        ObjectiveZoneId = "player_deployment",
                        EngagementRule = BattleEngagementRule.AttackFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 0,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    })
            }
        };

        for (int x = 0; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildObjectiveZoneResolvedPlanSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_objective_zone_resolved_plan",
            BattleId = "battle_objective_zone_resolved_plan",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "objective_gate",
                    DisplayName = "正面入口",
                    CellX = 3,
                    CellY = 0,
                    Width = 1,
                    Height = 1
                }
            },
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 7, 0, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildObjectiveReachedThenSeekSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_objective_reached_then_seek",
            BattleId = "battle_objective_reached_then_seek",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    3,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 3,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 7, 0, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 3; x <= 7; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRetainedLocalTargetGreedyStepSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_retained_local_target_greedy_step",
            BattleId = "battle_retained_local_target_greedy_step",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 0, 1, 160),
                BuildGroup("group_enemy", "enemy", "enemy", 4, 1, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 4; x++)
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
        int footprintHeight = 1,
        BattleGroupPlanSnapshot plan = null)
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
            FootprintHeight = footprintHeight,
            Plan = plan ?? new BattleGroupPlanSnapshot()
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
