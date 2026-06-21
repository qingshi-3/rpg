using System;
using System.Collections.Generic;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleDecisionOutcomeApplier
{
    internal static void Apply(
        List<BattleRuntimeTickContext> contexts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        RecordAdvanceFailureCallback recordAdvanceFailure,
        Action<BattleRuntimeActor> resetAdvanceFailureState)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(recordAdvanceFailure);
        ArgumentNullException.ThrowIfNull(resetAdvanceFailureState);

        foreach (BattleRuntimeTickContext context in contexts)
        {
            if (context.Request.Kind == BattleRuntimeAiActionKind.Hold)
            {
                context.ActorFact.Actor.TargetActorId = "";
                if (string.Equals(context.Request.FailureReason, LocalCombatDecisionReason.RejectOutsideLeash, StringComparison.Ordinal) ||
                    string.Equals(context.Request.FailureReason, BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, StringComparison.Ordinal))
                {
                    recordAdvanceFailure(context.ActorFact.Actor, context.Request.FailureReason);
                }
                else
                {
                    resetAdvanceFailureState(context.ActorFact.Actor);
                }

                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Succeeded(context.Request, "held");
                continue;
            }

            if (context.TargetFact == null &&
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardObjective &&
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardRegion &&
                context.Request.Kind != BattleRuntimeAiActionKind.ReturnToObjective)
            {
                context.ActorFact.Actor.TargetActorId = "";
                resetAdvanceFailureState(context.ActorFact.Actor);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (context.TargetFact != null)
            {
                bool targetChanged = !string.Equals(
                    context.ActorFact.Actor.TargetActorId,
                    context.TargetFact.Value.Actor.ActorId,
                    StringComparison.Ordinal);
                context.ActorFact.Actor.TargetActorId = context.TargetFact.Value.Actor.ActorId;
                if (targetChanged)
                {
                    resetAdvanceFailureState(context.ActorFact.Actor);
                    BattlePlanStateEmitter.SetPlanState(
                        stream,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        context.ActorFact.Actor,
                        BattleGroupPlanRuntimeState.TargetLocked,
                        "target_locked");
                }
            }
            else if (context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                     context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                     context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective)
            {
                context.ActorFact.Actor.TargetActorId = "";
                BattlePlanStateEmitter.SetPlanState(
                    stream,
                    battleId,
                    tick,
                    currentTimeSeconds,
                    context.ActorFact.Actor,
                    BattleGroupPlanRuntimeState.AdvancingToObjective,
                    context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                        ? LocalCombatDecisionReason.ReturnObjectiveThreatClear
                        : context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion
                            ? context.Request.ReasonCode
                            : "objective_advance");
            }

            if (!string.IsNullOrWhiteSpace(context.Proposal.FailureReason))
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, context.Proposal.FailureReason);
                BattleRuntimeActorStateMachine.MarkHolding(
                    context.ActorFact.Actor,
                    currentTimeSeconds,
                    ShouldPreserveMovementSteeringForFailure(context));
                if (context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
                    context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                    context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                    context.Request.Kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
                    context.Request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                    context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective)
                {
                    recordAdvanceFailure(context.ActorFact.Actor, context.Proposal.FailureReason);
                }

                continue;
            }

            if (context.Request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge)
            {
                resetAdvanceFailureState(context.ActorFact.Actor);
                BattleRuntimeActorStateMachine.MarkWaitingForCharge(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Succeeded(context.Request, "attack_charge_wait");
            }
            else if (context.Request.Kind != BattleRuntimeAiActionKind.AttackTarget &&
                     context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget &&
                     context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardObjective &&
                     context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardRegion &&
                     context.Request.Kind != BattleRuntimeAiActionKind.JoinLocalCombat &&
                     context.Request.Kind != BattleRuntimeAiActionKind.HoldSupport &&
                     context.Request.Kind != BattleRuntimeAiActionKind.ReturnToObjective)
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "unsupported_action");
            }
        }
    }

    private static bool ShouldPreserveMovementSteeringForFailure(BattleRuntimeTickContext context)
    {
        BattleRuntimeActor actor = context?.ActorFact.Actor;
        return actor != null &&
               (context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective) &&
               actor.MovementSteeringMode == BattleLocalSteeringMode.FollowObstacle &&
               actor.MovementSteeringBudgetRemaining <= 0;
    }
}
