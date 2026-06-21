using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleCombatZoneJoinRetargeting
{
    internal static bool IsBlockedLocalCombatHold(string failureReason)
    {
        return string.Equals(failureReason ?? "", BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, System.StringComparison.Ordinal) ||
               string.Equals(failureReason ?? "", LocalCombatDecisionReason.RejectNoReachableSlot, System.StringComparison.Ordinal) ||
               string.Equals(failureReason ?? "", LocalCombatDecisionReason.HoldSupportAttackSlotsFull, System.StringComparison.Ordinal);
    }

    internal static bool TryBuildAlternateCombatZoneJoinContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact failedTarget,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> targetFacts,
        IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> targetCandidates,
        BattleTacticalRegionSnapshot storedLocalCombatRegion,
        BattleTacticalRegionSnapshot combatJoinRegion,
        BattleMovementController movementController,
        BattleMovementProposalWorldInputs movementWorldInputs,
        double currentTimeSeconds,
        out BattleRuntimeTickContext context)
    {
        context = null;
        if (actorFact.Actor == null ||
            targetFacts == null ||
            targetCandidates == null)
        {
            return false;
        }

        foreach (BattleRuntimeAiTargetCandidateFacts candidate in targetCandidates
                     .Where(item =>
                         item != null &&
                         !string.IsNullOrWhiteSpace(item.ActorId) &&
                         !string.Equals(item.ActorId, failedTarget.Actor.ActorId ?? "", System.StringComparison.Ordinal))
                     .OrderBy(item => NormalizeCandidateScore(item.TravelCost))
                     .ThenBy(item => NormalizeCandidateScore(item.SelectionTier))
                     .ThenBy(item => NormalizeCandidateScore(item.OrthogonalAttackGap))
                     .ThenBy(item => NormalizeCandidateScore(item.GridGap))
                     .ThenBy(item => item.ActorId, System.StringComparer.Ordinal))
        {
            if (!targetFacts.TryGetValue(candidate.ActorId, out BattleRuntimeTickStartActorFact alternateTarget) ||
                alternateTarget.HitPoints <= 0 ||
                BattleRuntimeIdentityRules.SameFaction(actorFact.Actor, alternateTarget.Actor))
            {
                continue;
            }

            if (!movementController.TryBuildAlternateCombatZoneJoinProposalContext(
                    actorFact,
                    alternateTarget,
                    targetFacts,
                    BattleTargetSelectionService.IsTargetEngagedBySameFactionActor(targetFacts, actorFact, alternateTarget),
                    storedLocalCombatRegion,
                    combatJoinRegion,
                    new BattleMovementReservationMap(),
                    movementWorldInputs,
                    currentTimeSeconds,
                    out context))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static int NormalizeCandidateScore(int value)
    {
        return value < 0 ? int.MaxValue : value;
    }
}
