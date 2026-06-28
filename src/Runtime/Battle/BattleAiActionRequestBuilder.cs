using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleAiActionRequestBuilder
{
    internal static BattleRuntimeAiActionRequest BuildCommandScopedRequest(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation,
        BattleRegionMovementGoal regionMovementGoal,
        IBattleRuntimeAiExecutor aiExecutor,
        IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> targetCandidates = null,
        string targetSelectionPolicy = "")
    {
        BattleRuntimeAiDecisionFacts decisionFacts = BuildDecisionFacts(
            actorFact,
            targetFact,
            localCombatSituation,
            targetCandidates,
            targetSelectionPolicy);
        if (localCombatSituation != null)
        {
            BattleRuntimeAiActionRequest localRequest = aiExecutor.ChooseAction(decisionFacts);
            if (localRequest?.Kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
                localRequest?.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                localRequest?.Kind == BattleRuntimeAiActionKind.ReturnToObjective ||
                localRequest?.Kind == BattleRuntimeAiActionKind.AttackTarget ||
                localRequest?.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge ||
                (localRequest?.Kind == BattleRuntimeAiActionKind.Hold &&
                 string.Equals(
                     localRequest.FailureReason,
                     BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot,
                     System.StringComparison.Ordinal)))
            {
                return localRequest;
            }
        }

        bool shouldAdvanceInsideCommandScope =
            targetFact == null ||
            actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst &&
            BattleCombatGeometry.GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange);
        bool regionReached = BattleLocalCombatRegionResolver.IsRegionReached(actorFact, regionMovementGoal);
        if (shouldAdvanceInsideCommandScope &&
            regionMovementGoal != null &&
            !regionReached)
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardRegion(actorFact.Actor.ActorId, regionMovementGoal);
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst &&
            actorFact.Actor.HasObjectiveAnchor &&
            !BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact) &&
            shouldAdvanceInsideCommandScope)
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
        }

        if (targetFact == null &&
            actorFact.Actor.HasObjectiveAnchor &&
            !BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
        }

        if (BattleRuntimeIdentityRules.IsHoldLineCommand(actorFact.CommandId) &&
            (targetFact == null || BattleCombatGeometry.GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
        {
            return BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "hold_line_out_of_range");
        }

        return aiExecutor.ChooseAction(decisionFacts) ??
               BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "missing_ai_request");
    }

    internal static BattleRuntimeAiDecisionFacts BuildDecisionFacts(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation = null,
        IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> targetCandidates = null,
        string targetSelectionPolicy = "")
    {
        string joinReason = localCombatSituation?.BlocksObjectiveRoute == true
            ? LocalCombatDecisionReason.JoinBlocksObjectiveRoute
            : LocalCombatDecisionReason.JoinRecentDamage;
        BattleRuntimeAiDecisionFacts facts = new()
        {
            ActorId = actorFact.Actor.ActorId ?? "",
            TargetActorId = targetFact?.Actor.ActorId ?? "",
            HasTarget = targetFact != null,
            TargetSelectionPolicy = string.IsNullOrWhiteSpace(targetSelectionPolicy)
                ? BattleRuntimeAiTargetSelectionPolicy.Default
                : targetSelectionPolicy,
            DistanceToTarget = targetFact == null ? int.MaxValue : BattleCombatGeometry.GetOrthogonalAttackGap(actorFact, targetFact.Value),
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
        if (targetCandidates != null)
        {
            foreach (BattleRuntimeAiTargetCandidateFacts candidate in targetCandidates)
            {
                if (candidate != null)
                {
                    facts.TargetCandidates.Add(candidate);
                }
            }
        }

        return facts;
    }
}
