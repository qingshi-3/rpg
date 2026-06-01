using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// The tick resolver owns advance-failure state; request construction only
// invokes this explicit boundary to preserve the existing outside-leash write.
internal delegate void RecordAdvanceFailureCallback(BattleRuntimeActor actor, string reasonCode);

internal static class BattleAiActionRequestBuilder
{
    internal static BattleRuntimeAiActionRequest BuildCommandScopedRequest(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation,
        BattleRegionMovementGoal regionMovementGoal,
        IBattleRuntimeAiExecutor aiExecutor,
        RecordAdvanceFailureCallback recordAdvanceFailure)
    {
        if (localCombatSituation?.InsideLeash == false)
        {
            recordAdvanceFailure(actorFact.Actor, LocalCombatDecisionReason.RejectOutsideLeash);
            return BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, LocalCombatDecisionReason.RejectOutsideLeash);
        }

        BattleRuntimeAiDecisionFacts decisionFacts = BuildDecisionFacts(actorFact, targetFact, localCombatSituation);
        if (localCombatSituation != null)
        {
            BattleRuntimeAiActionRequest localRequest = aiExecutor.ChooseAction(decisionFacts);
            if (localRequest?.Kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
                localRequest?.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                localRequest?.Kind == BattleRuntimeAiActionKind.ReturnToObjective ||
                (localRequest?.Kind == BattleRuntimeAiActionKind.Hold &&
                 string.Equals(
                     localRequest.FailureReason,
                     BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot,
                     System.StringComparison.Ordinal)))
            {
                return localRequest;
            }
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst &&
            actorFact.Actor.HasObjectiveAnchor &&
            !BattleRuntimeTickResolver.IsObjectiveReached(actorFact) &&
            (targetFact == null ||
             BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
        }

        if (targetFact == null &&
            regionMovementGoal != null)
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardRegion(actorFact.Actor.ActorId, regionMovementGoal);
        }

        if (targetFact == null &&
            actorFact.Actor.HasObjectiveAnchor &&
            !BattleRuntimeTickResolver.IsObjectiveReached(actorFact))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
        }

        if (BattleRuntimeTickResolver.IsHoldLineCommand(actorFact.CommandId) &&
            (targetFact == null || BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
        {
            return BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "hold_line_out_of_range");
        }

        return aiExecutor.ChooseAction(decisionFacts) ??
               BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "missing_ai_request");
    }

    internal static BattleRuntimeAiDecisionFacts BuildDecisionFacts(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation = null)
    {
        string joinReason = localCombatSituation?.BlocksObjectiveRoute == true
            ? LocalCombatDecisionReason.JoinBlocksObjectiveRoute
            : LocalCombatDecisionReason.JoinRecentDamage;
        return new BattleRuntimeAiDecisionFacts
        {
            ActorId = actorFact.Actor.ActorId ?? "",
            TargetActorId = targetFact?.Actor.ActorId ?? "",
            HasTarget = targetFact != null,
            DistanceToTarget = targetFact == null ? int.MaxValue : BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact, targetFact.Value),
            AttackRange = System.Math.Max(1, actorFact.Actor.AttackRange),
            CanAttackNow = actorFact.AttackCharge >= 1.0,
            HasLocalCombatSituation = localCombatSituation != null,
            LocalCombatSituationId = localCombatSituation?.SituationId ?? "",
            LocalCombatOwnerBattleGroupId = localCombatSituation?.OwnerBattleGroupId ?? "",
            LocalCombatRegionId = localCombatSituation?.RegionId ?? "",
            LocalCombatCenterCellX = localCombatSituation?.Center.X ?? 0,
            LocalCombatCenterCellY = localCombatSituation?.Center.Y ?? 0,
            LocalCombatCenterCellHeight = localCombatSituation?.Center.Height ?? 0,
            LocalCombatWidth = System.Math.Max(1, localCombatSituation?.Width ?? 1),
            LocalCombatHeight = System.Math.Max(1, localCombatSituation?.Height ?? 1),
            LocalCombatVersion = localCombatSituation?.Version ?? 0,
            LocalCombatRegionReasonCode = localCombatSituation?.ReasonCode ?? "",
            LocalCombatTargetActorId = localCombatSituation?.TargetActorId ?? "",
            LocalCombatHasReachableAttackSlot = localCombatSituation?.HasReachableAttackSlot == true,
            LocalCombatHasReachableSupportSlot = localCombatSituation?.HasReachableSupportSlot == true,
            LocalCombatBlocksObjectiveRoute = localCombatSituation?.BlocksObjectiveRoute == true,
            LocalCombatInsideLeash = localCombatSituation?.InsideLeash != false,
            LocalCombatJoinReasonCode = joinReason,
            LocalCombatSupportReasonCode = LocalCombatDecisionReason.HoldSupportAttackSlotsFull,
            LocalCombatRejectReasonCode = LocalCombatDecisionReason.RejectOutsideLeash
        };
    }
}
