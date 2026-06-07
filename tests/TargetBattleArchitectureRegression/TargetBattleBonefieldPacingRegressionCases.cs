using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleBonefieldPacingRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime bonefield large joiner keeps stable slot intent instead of pacing", RuntimeBonefieldLargeJoinerKeepsStableSlotIntentInsteadOfPacing);
        run("runtime combat-zone join ignores stale retained target priority", RuntimeCombatZoneJoinIgnoresStaleRetainedTargetPriority);
        run("runtime bonefield blocked retained target does not strand large joiner", RuntimeBonefieldBlockedRetainedTargetDoesNotStrandLargeJoiner);
    }

    public static void RuntimeBonefieldLargeJoinerKeepsStableSlotIntentInsteadOfPacing()
    {
        BattleStartSnapshot snapshot = TargetBattleMultiUnitNavigationRegressionCases.BuildBonefieldOscillationStateSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        const string joinerId = "bonefield:f6_draugarlord:2";
        const string targetId = "expedition:player_camp:1:army:f1_azuritelion:2";

        PlaceActor(controller, "bonefield:f6_draugarlord:1", 42, 14);
        PlaceActor(controller, joinerId, 41, 19);
        PlaceActor(controller, "bonefield:f6_draugarlord:3", 39, 17);
        PlaceActor(controller, "bonefield:f6_draugarlord:4", 41, 16);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:1", 38, 16);
        PlaceActor(controller, targetId, 40, 15);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:3", 46, 14);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_grandmasterzir:1", 40, 14);

        BattleRuntimeActor joiner = controller.State.Actors.Single(item => item.ActorId == joinerId);
        joiner.TargetActorId = targetId;
        joiner.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;

        for (int i = 0; i < 32 && !controller.IsComplete; i++)
        {
            _ = controller.AdvanceNextTick();
        }

        BattleEvent[] moves = controller.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId == joinerId)
            .ToArray();
        AssertTrue(moves.Length >= 3, $"fixture should produce enough local-combat movement to expose slot pacing: moves={DescribeMoves(moves)}");
        AssertNoImmediateReverseMoves(moves, "large local-combat joiner should not pace between adjacent anchors while attack/support slot policy settles");
        string[] repeatedDestinations = moves
            .GroupBy(item => $"{item.ToGridX},{item.ToGridY},{item.ToGridHeight}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        AssertEqual(0, repeatedDestinations.Length, $"large local-combat joiner should not cycle through the same anchors before joining: repeated={string.Join(",", repeatedDestinations)} moves={DescribeMoves(moves)}");
    }

    public static void RuntimeBonefieldBlockedRetainedTargetDoesNotStrandLargeJoiner()
    {
        BattleStartSnapshot snapshot = TargetBattleMultiUnitNavigationRegressionCases.BuildBonefieldOscillationStateSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        const string joinerId = "bonefield:f6_draugarlord:1";
        const string saturatedTargetId = "expedition:player_camp:1:army:f1_azuritelion:1";
        const string alternateTargetId = "expedition:player_camp:1:army:f1_grandmasterzir:1";

        PlaceActor(controller, joinerId, 42, 17);
        PlaceActor(controller, "bonefield:f6_draugarlord:2", 40, 16);
        PlaceActor(controller, "bonefield:f6_draugarlord:3", 38, 17);
        PlaceActor(controller, "bonefield:f6_draugarlord:4", 40, 14);
        PlaceActor(controller, saturatedTargetId, 38, 16);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:2", 38, 15);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:3", 38, 14);
        PlaceActor(controller, alternateTargetId, 44, 14);

        BattleRuntimeActor joiner = controller.State.Actors.Single(item => item.ActorId == joinerId);
        joiner.TargetActorId = saturatedTargetId;
        joiner.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;
        joiner.LastAdvanceFailureReason = "hold_support_attack_slots_full";
        joiner.ConsecutiveAdvanceFailures = 1;

        for (int i = 0; i < 80 && !controller.IsComplete; i++)
        {
            _ = controller.AdvanceNextTick();
        }

        BattleEvent[] joinerEvents = controller.EventStream.Events
            .Where(item =>
                item.ActorId == joinerId &&
                item.Kind is BattleEventKind.MovementStarted or BattleEventKind.DamageApplied)
            .ToArray();
        AssertTrue(
            joinerEvents.Any(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                (item.TargetId == alternateTargetId || item.TargetId == saturatedTargetId)),
            $"large joiner should not stay stranded when either an alternate local target or retained-target fallback is executable: events={DescribeMoves(joinerEvents)} lastFailure={joiner.LastAdvanceFailureReason} phase={joiner.Phase}");
        AssertTrue(
            joiner.LastAdvanceFailureReason != BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot,
            $"executable fallback movement must not leave stale no-slot failure on the actor: lastFailure={joiner.LastAdvanceFailureReason}");
    }

    public static void RuntimeCombatZoneJoinIgnoresStaleRetainedTargetPriority()
    {
        BattleStartSnapshot snapshot = TargetBattleMultiUnitNavigationRegressionCases.BuildBonefieldOscillationStateSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        const string joinerId = "bonefield:f6_draugarlord:1";
        const string retainedTargetId = "expedition:player_camp:1:army:f1_azuritelion:1";
        const string executableTargetId = "expedition:player_camp:1:army:f1_grandmasterzir:1";

        PlaceActor(controller, joinerId, 42, 17);
        PlaceActor(controller, "bonefield:f6_draugarlord:2", 40, 16);
        PlaceActor(controller, "bonefield:f6_draugarlord:3", 38, 17);
        PlaceActor(controller, "bonefield:f6_draugarlord:4", 40, 14);
        PlaceActor(controller, retainedTargetId, 38, 16);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:2", 38, 15);
        PlaceActor(controller, "expedition:player_camp:1:army:f1_azuritelion:3", 38, 14);
        PlaceActor(controller, executableTargetId, 44, 14);

        BattleRuntimeActor joiner = controller.State.Actors.Single(item => item.ActorId == joinerId);
        joiner.TargetActorId = retainedTargetId;
        joiner.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;
        joiner.LastAdvanceFailureReason = "";
        joiner.ConsecutiveAdvanceFailures = 0;

        int advancedTicks = 0;
        for (int i = 0; i < 8 && !controller.IsComplete; i++)
        {
            _ = controller.AdvanceNextTick();
            advancedTicks++;
            bool joinerActed = controller.EventStream.Events.Any(item =>
                                    item.ActorId == joinerId &&
                                    item.Kind is BattleEventKind.MovementStarted or BattleEventKind.DamageApplied) ||
                                !string.IsNullOrWhiteSpace(joiner.LastAdvanceFailureReason) ||
                                !string.Equals(joiner.TargetActorId, retainedTargetId, StringComparison.Ordinal);
            if (joinerActed)
            {
                break;
            }
        }

        BattleEvent[] joinerEvents = controller.EventStream.Events
            .Where(item =>
                item.ActorId == joinerId &&
                item.Kind is BattleEventKind.MovementStarted or BattleEventKind.DamageApplied)
            .ToArray();
        bool switchedToExecutableTarget = string.Equals(joiner.TargetActorId, executableTargetId, StringComparison.Ordinal);
        bool retainedTargetBecameExecutable = joinerEvents.Any(item => item.TargetId == retainedTargetId);
        AssertTrue(
            switchedToExecutableTarget || retainedTargetBecameExecutable,
            $"combat-zone join should either switch away from a stale retained target or make the retained target executable through fallback movement after ticks={advancedTicks} events={DescribeMoves(controller.EventStream.Events.Where(item => item.ActorId == joinerId).ToArray())} target={joiner.TargetActorId} lastFailure={joiner.LastAdvanceFailureReason} phase={joiner.Phase}");
        AssertEqual("", joiner.LastAdvanceFailureReason, $"zone-first combat join should not need a retained-target failure marker before selecting an executable target after ticks={advancedTicks}");
        AssertTrue(
            joinerEvents.Any(item => item.TargetId == executableTargetId || item.TargetId == retainedTargetId),
            $"in-zone unit should immediately act toward an executable combat-zone target before reporting retained-target failure: events={DescribeMoves(joinerEvents)} target={joiner.TargetActorId} lastFailure={joiner.LastAdvanceFailureReason} phase={joiner.Phase}");
    }

    private static void PlaceActor(BattleRuntimeSessionController controller, string actorId, int x, int y)
    {
        BattleRuntimeActor actor = controller.State.Actors.Single(item => item.ActorId == actorId);
        actor.GridX = x;
        actor.GridY = y;
        actor.GridHeight = 0;
        actor.Position = x;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        actor.HasReservedGridCell = false;
        actor.HasMovementTarget = false;
        actor.MovementProgress = 0;
        actor.ActionReadyAtSeconds = 0;
        actor.HasMovementIntentSnapshot = false;
        actor.MovementIntentTargetActorId = "";
        actor.MovementIntentLocalCombatSituationId = "";
        actor.HasMovementIntentCombatSlot = false;
    }

    private static void AssertNoImmediateReverseMoves(IReadOnlyList<BattleEvent> moves, string message)
    {
        for (int i = 1; i < moves.Count; i++)
        {
            BattleEvent previous = moves[i - 1];
            BattleEvent current = moves[i];
            bool reversed =
                previous.FromGridX == current.ToGridX &&
                previous.FromGridY == current.ToGridY &&
                previous.FromGridHeight == current.ToGridHeight &&
                previous.ToGridX == current.FromGridX &&
                previous.ToGridY == current.FromGridY &&
                previous.ToGridHeight == current.FromGridHeight;
            AssertTrue(!reversed, $"{message}: moves={DescribeMoves(moves)}");
        }
    }

    private static string DescribeMoves(IReadOnlyList<BattleEvent> moves)
    {
        return string.Join(
            ";",
            moves.Select(item =>
                $"t{item.RuntimeTick}:({item.FromGridX},{item.FromGridY},{item.FromGridHeight})->({item.ToGridX},{item.ToGridY},{item.ToGridHeight})"));
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
