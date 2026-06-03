using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleContinuousStepHandoffRegressionCases
{
    private const double FixedTickSeconds = 0.04;
    private const double MoveStepSeconds = 0.16;
    private const string PlayerActorId = "force_player:1";

    public static void Register(Action<string, Action> run)
    {
        run("runtime continuous step handoff emits next segment same tick", RuntimeContinuousStepHandoffEmitsNextSegmentSameTick);
        run("runtime continuous step handoff stops when objective reached", RuntimeContinuousStepHandoffStopsWhenObjectiveReached);
        run("runtime continuous step handoff stops when local enemy is perceived", RuntimeContinuousStepHandoffStopsWhenLocalEnemyIsPerceived);
        run("runtime continuous step handoff stops when command changes", RuntimeContinuousStepHandoffStopsWhenCommandChanges);
        run("runtime continuous step handoff respects reservation rejection", RuntimeContinuousStepHandoffRespectsReservationRejection);
        run("runtime continuous step handoff does not retarget after scoped target dies", RuntimeContinuousStepHandoffDoesNotRetargetAfterScopedTargetDies);
        run("runtime continuous step handoff does not start after same-tick mover defeat", RuntimeContinuousStepHandoffDoesNotStartAfterSameTickMoverDefeat);
        run("runtime state transition diagnostics are logged", RuntimeStateTransitionDiagnosticsAreLogged);
    }

    internal static void RuntimeContinuousStepHandoffEmitsNextSegmentSameTick()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveLaneSnapshot("battle_continuous_step_handoff", objectiveX: 5, enemyX: 10));

        List<FixedTickSlice> slices = AdvanceFixedTicks(controller, maxTicks: 24);
        BattleEvent[] starts = slices
            .SelectMany(item => item.Events)
            .Where(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == PlayerActorId)
            .Take(3)
            .ToArray();

        AssertTrue(starts.Length >= 3, "continuous objective lane should produce at least three player movement starts");
        for (int i = 1; i < starts.Length; i++)
        {
            double delta = starts[i].RuntimeTimeSeconds - starts[i - 1].RuntimeTimeSeconds;
            AssertFloatEqual(
                MoveStepSeconds,
                delta,
                0.0001,
                $"same actor MovementStarted events should be separated by one move-step duration, not by an extra fixed tick: times=[{FormatTimes(starts)}]");
        }

        AssertTrue(
            TryFindSameTickContinuationPair(slices, PlayerActorId, out string pairDescription),
            $"a completed segment should hand off to the next segment in the same fixed tick: observed={pairDescription}");
    }

    internal static void RuntimeContinuousStepHandoffStopsWhenObjectiveReached()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveLaneSnapshot("battle_continuous_step_objective_stop", objectiveX: 1, enemyX: 6));

        FixedTickSlice boundary = AdvanceUntilMovementCompleted(controller, PlayerActorId, maxTicks: 12);

        BattleEvent completed = boundary.Events.Single(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == PlayerActorId);
        AssertEqual(1, completed.ToGridX, "player should complete the objective cell");
        AssertTrue(
            boundary.Events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != PlayerActorId),
            "reaching the objective region must stop same-tick continuation instead of starting a forward segment");
    }

    internal static void RuntimeContinuousStepHandoffStopsWhenLocalEnemyIsPerceived()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveLaneSnapshot("battle_continuous_step_perception_stop", objectiveX: 5, enemyX: 5));

        _ = controller.AdvanceFixedTick(FixedTickSeconds);
        FixedTickSlice boundary = AdvanceUntilMovementCompleted(controller, PlayerActorId, maxTicks: 12);

        AssertTrue(
            boundary.Events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != PlayerActorId),
            "a completed objective mover that has local enemy contact must stop same-tick continuation so the next anchored decision can respond");
    }

    internal static void RuntimeContinuousStepHandoffStopsWhenCommandChanges()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildObjectiveLaneSnapshot("battle_continuous_step_command_changed", objectiveX: 5, enemyX: 10));

        BattleRuntimeAdvanceResult firstAdvance = controller.AdvanceFixedTick(FixedTickSeconds);
        AssertTrue(
            firstAdvance.Events.Any(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == PlayerActorId),
            "the command-change scenario should start with an in-progress movement segment");

        BattleRuntimeActor mover = controller.State.Actors.Single(item => item.ActorId == PlayerActorId);
        mover.CommandId = "HoldLine";

        FixedTickSlice boundary = AdvanceUntilMovementCompleted(controller, PlayerActorId, maxTicks: 12);
        AssertTrue(
            boundary.Events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != PlayerActorId),
            "changing command identity during a segment must return the mover to anchored decision instead of same-tick continuation");
    }

    internal static void RuntimeContinuousStepHandoffRespectsReservationRejection()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionController controller = new BattleRuntimeSession(performanceCounters: counters)
            .Begin(BuildContinuationReservationSnapshot());
        BattleRuntimeActor mover = controller.State.Actors.Single(item => item.ActorId == PlayerActorId);
        ConfigureCompletedObjectiveSegment(mover, fromX: 0, fromY: 0, toX: 1, toY: 0);

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(FixedTickSeconds);
        BattleEvent[] events = advance.Events.ToArray();

        AssertTrue(
            events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == PlayerActorId),
            "reservation scenario should begin from a completed mover boundary");
        AssertTrue(
            events.Any(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId == "player_contender:1" &&
                item.ToGridX == 2 &&
                item.ToGridY == 0),
            "the normal decision mover should reserve the shared next cell first");
        AssertTrue(
            events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != PlayerActorId),
            "same-tick continuation must not bypass reservation rejection");
        AssertTrue(counters.ReservationRejectedLastAdvance > 0, "continuation reservation rejection should use existing reservation diagnostics");
        AssertTrue(counters.HoldDueReservationCount > 0, "continuation reservation rejection should use the existing hold-due-reservation counter");
    }

    internal static void RuntimeContinuousStepHandoffDoesNotRetargetAfterScopedTargetDies()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTargetDeathDuringBoundarySnapshot());
        BattleRuntimeActor mover = controller.State.Actors.Single(item => item.ActorId == "player_mover:1");
        BattleRuntimeActor killer = controller.State.Actors.Single(item => item.ActorId == "player_killer:1");

        mover.TargetActorId = "enemy_weak:1";
        killer.Phase = BattleRuntimeActorPhase.AttackRecovery;
        killer.MotionState = BattleRuntimeActorMotionState.Attacking;
        killer.ActionReadyAtSeconds = MoveStepSeconds;

        _ = controller.AdvanceFixedTick(FixedTickSeconds);
        FixedTickSlice boundary = AdvanceUntilMovementCompleted(controller, "player_mover:1", maxTicks: 12);

        AssertTrue(
            boundary.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_killer:1" &&
                item.TargetId == "enemy_weak:1"),
            "the scoped movement target should die in the mover boundary tick");
        AssertTrue(
            boundary.Events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != "player_mover:1"),
            "same-intent continuation must not retarget a target-scoped mover after its stored target dies");
    }

    internal static void RuntimeContinuousStepHandoffDoesNotStartAfterSameTickMoverDefeat()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildMoverDefeatedDuringBoundarySnapshot());

        _ = controller.AdvanceFixedTick(FixedTickSeconds);
        FixedTickSlice boundary = AdvanceUntilMovementCompleted(controller, PlayerActorId, maxTicks: 12);

        AssertTrue(
            boundary.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "enemy_killer:1" &&
                item.TargetId == PlayerActorId),
            "enemy ranged attack should defeat the mover in the same tick as the committed boundary");
        AssertEqual(0, controller.State.Actors.Single(item => item.ActorId == PlayerActorId).HitPoints, "mover should be defeated by same-tick damage");
        AssertTrue(
            boundary.Events.All(item => item.Kind != BattleEventKind.MovementStarted || item.ActorId != PlayerActorId),
            "a mover defeated by same-tick damage must not emit a continuation MovementStarted event");
    }

    internal static void RuntimeStateTransitionDiagnosticsAreLogged()
    {
        string previousLog = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        _ = new BattleRuntimeSession()
            .RunMinimal(BuildObjectiveLaneSnapshot("battle_state_transition_diagnostics", objectiveX: 5, enemyX: 5));
        string log = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        string newLog = log.Length >= previousLog.Length
            ? log[previousLog.Length..]
            : log;

        AssertTrue(
            newLog.Contains("BattleRuntimeStateTransition battle=battle_state_transition_diagnostics", StringComparison.Ordinal) &&
            newLog.Contains("state=AdvancingToObjective", StringComparison.Ordinal),
            "state transition diagnostics should log objective movement state");
        AssertTrue(
            newLog.Contains("state=MovingToAttackSlot", StringComparison.Ordinal) ||
            newLog.Contains("state=Attacking", StringComparison.Ordinal),
            "state transition diagnostics should log contact response state");
    }

    private static BattleStartSnapshot BuildObjectiveLaneSnapshot(string battleId, int objectiveX, int enemyX)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
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
                    plan: BuildMoveFirstPlan("objective_lane", objectiveX, 0)),
                BuildGroup("group_enemy", "enemy", "enemy_anchor", enemyX, 0, 160, initialCommandId: "HoldLine")
            }
        };

        AddLineSurfaces(snapshot, 0, enemyX, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTargetDeathDuringBoundarySnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_continuous_step_target_death",
            BattleId = "battle_continuous_step_target_death",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_mover", "player", "player_mover", 0, 0, 120),
                BuildGroup("group_player_killer", "player", "player_killer", 4, 0, 120, attackDamage: 6),
                BuildGroup("group_enemy_weak", "enemy", "enemy_weak", 5, 0, 6, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy_live", "enemy", "enemy_live", 7, 2, 120, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 7, 0, 2);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildMoverDefeatedDuringBoundarySnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_continuous_step_mover_defeat",
            BattleId = "battle_continuous_step_mover_defeat",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    6,
                    attackRange: 1,
                    plan: BuildMoveFirstPlan("objective_far", 6, 0)),
                BuildGroup("group_enemy_killer", "enemy", "enemy_killer", 4, 0, 120, initialCommandId: "HoldLine", attackRange: 3, attackDamage: 6),
                BuildGroup("group_enemy_anchor", "enemy", "enemy_anchor", 8, 0, 120, initialCommandId: "HoldLine")
            }
        };

        AddLineSurfaces(snapshot, 0, 8, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildContinuationReservationSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_continuous_step_reservation_rejected",
            BattleId = "battle_continuous_step_reservation_rejected",
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
                    plan: BuildMoveFirstPlan("reservation_lane", 5, 0)),
                BuildGroup(
                    "group_player_contender",
                    "player",
                    "player_contender",
                    1,
                    -1,
                    160,
                    plan: BuildMoveFirstPlan("reservation_lane", 5, 0)),
                BuildGroup("group_enemy", "enemy", "enemy_anchor", 8, 0, 160, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 8, -1, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static void ConfigureCompletedObjectiveSegment(BattleRuntimeActor actor, int fromX, int fromY, int toX, int toY)
    {
        actor.CommandId = "Assault";
        actor.GridX = fromX;
        actor.GridY = fromY;
        actor.GridHeight = 0;
        actor.Phase = BattleRuntimeActorPhase.Moving;
        actor.MotionState = BattleRuntimeActorMotionState.Moving;
        actor.HasReservedGridCell = true;
        actor.ReservedGridX = toX;
        actor.ReservedGridY = toY;
        actor.ReservedGridHeight = 0;
        actor.HasMovementTarget = true;
        actor.MovementFromGridX = fromX;
        actor.MovementFromGridY = fromY;
        actor.MovementFromGridHeight = 0;
        actor.MovementToGridX = toX;
        actor.MovementToGridY = toY;
        actor.MovementToGridHeight = 0;
        actor.MovementStartedAtSeconds = -MoveStepSeconds;
        actor.MovementDurationSeconds = MoveStepSeconds;
        actor.ActionReadyAtSeconds = 0;
        actor.MovementProgress = 1;
        // The test seeds a Runtime-owned segment snapshot so the resolver can
        // prove same-intent continuation still respects reservation authority.
        actor.HasMovementIntentSnapshot = true;
        actor.MovementIntentKind = BattleRuntimeAiActionKind.AdvanceTowardObjective;
        actor.MovementIntentObjectiveZoneId = actor.ObjectiveZoneId ?? "";
        actor.MovementIntentCommandId = actor.CommandId ?? "";
        actor.MovementIntentSegmentDurationSeconds = MoveStepSeconds;
    }

    private static BattleGroupPlanSnapshot BuildMoveFirstPlan(string objectiveZoneId, int x, int y)
    {
        return new BattleGroupPlanSnapshot
        {
            ObjectiveZoneId = objectiveZoneId,
            EngagementRule = BattleEngagementRule.MoveFirst,
            HasObjectiveAnchor = true,
            ObjectiveCellX = x,
            ObjectiveCellY = y,
            ObjectiveCellHeight = 0,
            ObjectiveWidth = 1,
            ObjectiveHeight = 1
        };
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "",
        int attackRange = 1,
        int attackDamage = 1,
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
            AttackDamage = attackDamage,
            AttackRange = attackRange,
            AttackSpeed = 1.0,
            MoveStepSeconds = MoveStepSeconds,
            AttackActionSeconds = 1.2,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            Plan = plan ?? new BattleGroupPlanSnapshot()
        };
    }

    private static void AddLineSurfaces(BattleStartSnapshot snapshot, int minX, int maxX, int y)
    {
        for (int x = minX; x <= maxX; x++)
        {
            AddSurface(snapshot, x, y);
        }
    }

    private static void AddRectSurfaces(BattleStartSnapshot snapshot, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }
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

    private static List<FixedTickSlice> AdvanceFixedTicks(BattleRuntimeSessionController controller, int maxTicks)
    {
        List<FixedTickSlice> slices = new();
        for (int i = 0; i < maxTicks && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(FixedTickSeconds);
            slices.Add(new FixedTickSlice(i, advance.Events.ToArray()));
        }

        return slices;
    }

    private static FixedTickSlice AdvanceUntilMovementCompleted(BattleRuntimeSessionController controller, string actorId, int maxTicks)
    {
        for (int i = 0; i < maxTicks && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(FixedTickSeconds);
            BattleEvent[] events = advance.Events.ToArray();
            if (events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == actorId))
            {
                return new FixedTickSlice(i, events);
            }
        }

        throw new Exception($"expected movement boundary for actor={actorId}");
    }

    private static bool TryFindSameTickContinuationPair(
        IReadOnlyList<FixedTickSlice> slices,
        string actorId,
        out string observed)
    {
        List<string> summaries = new();
        foreach (FixedTickSlice slice in slices)
        {
            BattleEvent[] actorEvents = slice.Events
                .Where(item =>
                    item.ActorId == actorId &&
                    item.Kind is BattleEventKind.MovementCompleted or BattleEventKind.MovementStarted)
                .ToArray();
            if (actorEvents.Length == 0)
            {
                continue;
            }

            summaries.Add($"tick{actorEvents[0].RuntimeTick}=[{string.Join(";", actorEvents.Select(FormatMovement))}]");
            for (int completeIndex = 0; completeIndex < actorEvents.Length; completeIndex++)
            {
                BattleEvent completed = actorEvents[completeIndex];
                if (completed.Kind != BattleEventKind.MovementCompleted)
                {
                    continue;
                }

                for (int startIndex = completeIndex + 1; startIndex < actorEvents.Length; startIndex++)
                {
                    BattleEvent started = actorEvents[startIndex];
                    if (started.Kind == BattleEventKind.MovementStarted &&
                        started.FromGridX == completed.ToGridX &&
                        started.FromGridY == completed.ToGridY &&
                        started.FromGridHeight == completed.ToGridHeight)
                    {
                        observed = FormatMovement(started);
                        return true;
                    }
                }
            }
        }

        observed = string.Join(" ", summaries);
        return false;
    }

    private static string FormatTimes(IEnumerable<BattleEvent> events)
    {
        return string.Join(",", events.Select(item => item.RuntimeTimeSeconds.ToString("0.00")));
    }

    private static string FormatMovement(BattleEvent item)
    {
        return $"{item.Kind}@{item.RuntimeTimeSeconds:0.00}:{item.FromGridX},{item.FromGridY}->{item.ToGridX},{item.ToGridY}";
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertFloatEqual(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"{message}: expected={expected} actual={actual} tolerance={tolerance}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private sealed record FixedTickSlice(int SequenceIndex, IReadOnlyList<BattleEvent> Events);
}
