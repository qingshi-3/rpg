using System.Collections.Generic;
using Rpg.Runtime.Battle.AI;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private BattleRuntimeAiActionRequest BuildCommandScopedAiActionRequest(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation,
        BattleRegionMovementGoal regionMovementGoal = null)
    {
        if (localCombatSituation?.InsideLeash == false)
        {
            RecordAdvanceFailure(actorFact.Actor, LocalCombatDecisionReason.RejectOutsideLeash);
            return BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, LocalCombatDecisionReason.RejectOutsideLeash);
        }

        BattleRuntimeAiDecisionFacts decisionFacts = BuildAiDecisionFacts(actorFact, targetFact, localCombatSituation);
        if (localCombatSituation != null)
        {
            BattleRuntimeAiActionRequest localRequest = _aiExecutor.ChooseAction(decisionFacts);
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
            !IsObjectiveReached(actorFact) &&
            (targetFact == null ||
             GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
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
            !IsObjectiveReached(actorFact))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
        }

        if (IsHoldLineCommand(actorFact.CommandId) &&
            (targetFact == null || GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
        {
            return BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "hold_line_out_of_range");
        }

        return _aiExecutor.ChooseAction(decisionFacts) ??
               BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "missing_ai_request");
    }

    private static BattleRuntimeAiDecisionFacts BuildAiDecisionFacts(
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
            DistanceToTarget = targetFact == null ? int.MaxValue : GetOrthogonalAttackGap(actorFact, targetFact.Value),
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

    private static BattleTacticalRegionSnapshot ResolveEngagedLocalCombatRegion(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        if (tacticalStateStore == null || string.IsNullOrWhiteSpace(actorFact.Actor.BattleGroupId))
        {
            return null;
        }

        try
        {
            BattleGroupTacticalState tacticalState = tacticalStateStore.GetRequiredSnapshot(actorFact.Actor.BattleGroupId);
            return tacticalState.EngagementState == BattleGroupEngagementState.Engaged
                ? tacticalState.LocalCombatRegion
                : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> FilterFactsToLocalCombatRegion(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        Dictionary<string, BattleRuntimeTickStartActorFact> filtered = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeTickStartActorFact fact in facts?.Values ?? System.Array.Empty<BattleRuntimeTickStartActorFact>())
        {
            if (string.Equals(fact.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", System.StringComparison.Ordinal) ||
                SameFaction(fact.Actor, actorFact.Actor) ||
                IsInsideLocalCombatRegion(fact, localCombatRegion))
            {
                filtered[fact.Actor.ActorId ?? ""] = fact;
            }
        }

        return filtered;
    }

    private static bool IsInsideLocalCombatRegion(
        BattleRuntimeTickStartActorFact fact,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (localCombatRegion == null ||
            fact.Actor.GridHeight != localCombatRegion.CenterCellHeight)
        {
            return false;
        }

        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        return fact.Actor.GridX >= minX &&
               fact.Actor.GridX < minX + width &&
               fact.Actor.GridY >= minY &&
               fact.Actor.GridY < minY + height;
    }

    private static BattleRegionMovementGoal ResolveRegionMovementGoal(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        if (tacticalStateStore == null || string.IsNullOrWhiteSpace(actorFact.Actor.BattleGroupId))
        {
            return null;
        }

        try
        {
            BattleGroupTacticalState tacticalState = tacticalStateStore.GetRequiredSnapshot(actorFact.Actor.BattleGroupId);
            return EnemyBattleGroupRegionPolicy.ResolveMovementGoal(tacticalState);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

}
