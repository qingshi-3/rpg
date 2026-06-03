using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// The tick resolver owns stale-target context rebuilds; movement owns detecting
// the stale target and invoking this narrow retarget boundary.
internal delegate bool TryRetargetStaleAdvanceContextCallback(
    BattleRuntimeTickContext context,
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
    BattleDynamicOccupancy occupancy,
    BattleNavigationGraph navigationGraph,
    string battleId,
    int tick,
    double currentTimeSeconds,
    HashSet<string> navigationFailureDiagnostics,
    BattlePerformanceCounters performanceCounters);

internal static class BattleMovementCommitResolver
{
    private sealed class MoveCandidate
    {
        public BattleRuntimeTickContext Context { get; init; }
        public BattleGridCoord From { get; init; }
        public BattleGridCoord To { get; init; }
        public IReadOnlyList<BattleGridCoord> OrderedMoves { get; init; } = new List<BattleGridCoord>();
    }

    internal static int Resolve(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters,
        TryRetargetStaleAdvanceContextCallback retargetStaleAdvanceContext)
    {
        int movementEvents = 0;
        List<MoveCandidate> moveCandidates = new();
        foreach (BattleRuntimeTickContext context in contexts
                     .Where(item =>
                         (item.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
                          item.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                          item.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                          item.Request.Kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
                          item.Request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                          item.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective) &&
                          item.Result == null))
        {
            bool isObjectiveAdvance = context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                                      context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                                      context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective;
            if (context.TargetFact == null && !isObjectiveAdvance)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (!context.Proposal.HasMoveTo)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "advance_failed");
                BattleRuntimeTickResolver.RecordAdvanceFailure(context.ActorFact.Actor, context.Proposal.FailureReason);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                continue;
            }

            if (context.ActorFact.Actor.HitPoints <= 0)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "actor_defeated_before_move");
                continue;
            }

            if (!isObjectiveAdvance && context.TargetFact.Value.Actor.HitPoints <= 0)
            {
                if (!context.AllowStaleTargetRetarget ||
                    !retargetStaleAdvanceContext(
                        context,
                        tickStartFacts,
                        occupancy,
                        navigationGraph,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        navigationFailureDiagnostics,
                        performanceCounters))
                {
                    context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_defeated_before_move");
                    continue;
                }
            }

            IReadOnlyList<BattleGridCoord> orderedMoves = context.AllowReservationFallback && context.Proposal.MoveOptions?.Count > 0
                ? context.Proposal.MoveOptions
                : new[] { context.Proposal.MoveTo };

            moveCandidates.Add(new MoveCandidate
            {
                Context = context,
                From = context.ActorFact.Anchor,
                To = context.Proposal.MoveTo,
                OrderedMoves = orderedMoves
            });
        }

        BattleMovementReservationMap reservations = new();
        foreach (MoveCandidate candidate in moveCandidates
                     .OrderBy(item => BattleActorFootprint.GetGap(
                         item.Context.ActorFact.Actor,
                         item.Context.ActorFact.Anchor,
                         item.Context.TargetFact?.Actor ?? item.Context.ActorFact.Actor,
                         item.Context.TargetFact?.Anchor ?? BattleObjectiveAdvancePlanner.GetObjectiveAnchor(item.Context.ActorFact.Actor)))
                     .ThenBy(item => item.From.Height)
                     .ThenBy(item => item.From.Y)
                     .ThenBy(item => item.From.X)
                     .ThenBy(item => item.Context.ActorFact.Actor.BattleGroupId, System.StringComparer.Ordinal))
        {
            bool reserved = false;
            BattleGridCoord selectedMove = candidate.To;
            foreach (BattleGridCoord move in candidate.OrderedMoves)
            {
                if (!reservations.TryReserveMove(candidate.Context.ActorFact.Actor, candidate.From, move, occupancy))
                {
                    performanceCounters?.RecordReservationRejected();
                    continue;
                }

                selectedMove = move;
                reserved = true;
                break;
            }

            if (!reserved)
            {
                candidate.Context.Result = BattleRuntimeAiActionResult.Failed(candidate.Context.Request, "reservation_rejected");
                BattleRuntimeTickResolver.RecordAdvanceFailure(candidate.Context.ActorFact.Actor, "reservation_rejected");
                performanceCounters?.RecordHoldDueReservation();
                BattleRuntimeActorStateMachine.MarkHolding(candidate.Context.ActorFact.Actor, currentTimeSeconds);
                BattleRuntimeAdvanceDiagnostics.LogAdvanceFailureDiagnostic(
                    battleId,
                    tick,
                    candidate.Context.ActorFact,
                    candidate.Context.TargetFact,
                    navigationGraph,
                    "reservation_rejected",
                    candidate.To,
                    navigationFailureDiagnostics);
                continue;
            }

            candidate.Context.ActorFact.Actor.HasReservedGridCell = true;
            candidate.Context.ActorFact.Actor.ReservedGridX = selectedMove.X;
            candidate.Context.ActorFact.Actor.ReservedGridY = selectedMove.Y;
            candidate.Context.ActorFact.Actor.ReservedGridHeight = selectedMove.Height;
            BattleGroupPlanRuntimeState planState =
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                    ? BattleGroupPlanRuntimeState.AdvancingToObjective
                    : BattleGroupPlanRuntimeState.MovingToAttackSlot;
            string transitionReason = candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                ? "objective_advance"
                : candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion
                    ? candidate.Context.Request.ReasonCode
                : candidate.Context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                    ? LocalCombatDecisionReason.ReturnObjectiveThreatClear
                : "moving_to_attack_slot";
            BattlePlanStateEmitter.SetPlanState(
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                candidate.Context.ActorFact.Actor,
                planState,
                transitionReason,
                logWhenUnchanged: true,
                actionCode: "movement_started",
                from: candidate.From,
                to: selectedMove);
            LogCombatSlotIntentIfChanged(battleId, tick, currentTimeSeconds, candidate.Context, selectedMove);
            BattleRuntimeActorStateMachine.MarkMovementCommitted(
                candidate.Context.ActorFact.Actor,
                selectedMove,
                currentTimeSeconds,
                BattleMovementIntentCommit.FromContext(candidate.Context));
            BattleRuntimeTickResolver.ResetAdvanceFailureState(candidate.Context.ActorFact.Actor);
            candidate.Context.Result = BattleRuntimeAiActionResult.Succeeded(candidate.Context.Request, "advanced");
            stream.Add(BattleRuntimeEventFactory.CreateMovementEvent(
                BattleEventKind.MovementStarted,
                battleId,
                tick,
                currentTimeSeconds,
                candidate.Context.ActorFact.Actor,
                BattleObjectiveAdvancePlanner.ResolveMovementEventTargetId(candidate.Context),
                candidate.From,
                selectedMove,
                !string.IsNullOrWhiteSpace(candidate.Context.Proposal.MovementReasonCode)
                    ? candidate.Context.Proposal.MovementReasonCode
                    : candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                    ? "plan_objective_advance"
                    : candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion
                        ? candidate.Context.Request.ReasonCode
                    : candidate.Context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                        ? LocalCombatDecisionReason.ReturnObjectiveThreatClear
                    : "auto_advance"));
            movementEvents++;
            performanceCounters?.RecordMovementEvent(currentTimeSeconds);
        }

        return movementEvents;
    }

    private static void LogCombatSlotIntentIfChanged(
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeTickContext context,
        BattleGridCoord selectedMove)
    {
        if (context?.Proposal?.HasCombatSlotIntent != true)
        {
            return;
        }

        BattleRuntimeActor actor = context.ActorFact.Actor;
        BattleGridCoord slot = context.Proposal.CombatSlotAnchor;
        bool unchanged = actor.HasMovementIntentCombatSlot &&
                         actor.MovementIntentCombatSlotX == slot.X &&
                         actor.MovementIntentCombatSlotY == slot.Y &&
                         actor.MovementIntentCombatSlotHeight == slot.Height &&
                         actor.MovementIntentCombatSlotKind == context.Proposal.CombatSlotKind;
        if (unchanged)
        {
            return;
        }

        // Slot assignment is the stable target for combat-entry movement; Runtime
        // still validates the selected next step and may reassign if the slot expires.
        GameLog.Info(
            nameof(BattleMovementCommitResolver),
            $"BattleRuntimeCombatSlotIntent battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} actor={actor.ActorId ?? ""} target={context.TargetFact?.Actor.ActorId ?? ""} situation={context.Proposal.LocalCombatSituationId ?? ""} kind={context.Proposal.CombatSlotKind} slot={slot.X},{slot.Y},{slot.Height} next={selectedMove.X},{selectedMove.Y},{selectedMove.Height} reason={context.Proposal.MovementReasonCode ?? ""}");
    }
}
