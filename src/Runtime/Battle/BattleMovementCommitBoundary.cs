using System;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Accepted movement commits cross the world reservation barrier here. Candidate
// selection and reservation stay outside; this boundary owns the resulting
// actor state mutation and event emission for the accepted step.
internal static class BattleMovementCommitBoundary
{
    internal static void ApplyAcceptedMove(
        BattleRuntimeTickContext context,
        BattleGridCoord from,
        BattleGridCoord selectedMove,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattlePerformanceCounters performanceCounters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stream);
        BattleRuntimeActor actor = context.ActorFact.Actor;
        ArgumentNullException.ThrowIfNull(actor);

        actor.HasReservedGridCell = true;
        actor.ReservedGridX = selectedMove.X;
        actor.ReservedGridY = selectedMove.Y;
        actor.ReservedGridHeight = selectedMove.Height;

        LogCombatSlotIntentIfChanged(battleId, tick, currentTimeSeconds, context, selectedMove);
        BattleRuntimeActorStateMachine.MarkMovementCommitted(
            actor,
            selectedMove,
            currentTimeSeconds,
            BattleMovementIntentCommit.FromContext(context));
        BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState(actor);
        context.Result = BattleRuntimeAiActionResult.Succeeded(context.Request, "advanced");
        stream.Add(BattleRuntimeEventFactory.CreateMovementEvent(
            BattleEventKind.MovementStarted,
            battleId,
            tick,
            currentTimeSeconds,
            actor,
            ResolveMovementEventTargetId(context),
            from,
            selectedMove,
            !string.IsNullOrWhiteSpace(context.Proposal.MovementReasonCode)
                ? context.Proposal.MovementReasonCode
                : context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardBeacon
                    ? "destination_beacon_advance"
                    : context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                        ? "plan_objective_advance"
                        : context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion
                            ? context.Request.ReasonCode
                            : context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                                ? LocalCombatDecisionReason.ReturnObjectiveThreatClear
                                : "auto_advance"));
        performanceCounters?.RecordMovementEvent(currentTimeSeconds);
    }

    private static string ResolveMovementEventTargetId(BattleRuntimeTickContext context)
    {
        if (context?.Request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return context.Request.RegionMovementGoal?.RegionId ?? "";
        }

        if (context?.Request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardBeacon)
        {
            return context.ActorFact.Actor.ActiveDestinationBeaconId ?? "";
        }

        return context?.TargetFact?.Actor.ActorId ?? context?.ActorFact.Actor.ObjectiveZoneId ?? "";
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

        // Slot assignment can update often in crowded fights, so it stays
        // trace-only while transition events remain the default diagnostics.
        GameLog.Trace(
            nameof(BattleMovementCommitBoundary),
            $"BattleRuntimeCombatSlotIntent battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} actor={actor.ActorId ?? ""} target={context.TargetFact?.Actor.ActorId ?? ""} situation={context.Proposal.LocalCombatSituationId ?? ""} kind={context.Proposal.CombatSlotKind} slot={slot.X},{slot.Y},{slot.Height} next={selectedMove.X},{selectedMove.Y},{selectedMove.Height} reason={context.Proposal.MovementReasonCode ?? ""}");
    }
}
