using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private static bool IsBlockedLocalCombatHold(string failureReason)
    {
        return string.Equals(failureReason ?? "", BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, System.StringComparison.Ordinal) ||
               string.Equals(failureReason ?? "", LocalCombatDecisionReason.RejectNoReachableSlot, System.StringComparison.Ordinal) ||
               string.Equals(failureReason ?? "", LocalCombatDecisionReason.HoldSupportAttackSlotsFull, System.StringComparison.Ordinal);
    }

    private static bool TryBuildAlternateCombatZoneJoinContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact failedTarget,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> targetFacts,
        IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> targetCandidates,
        BattleTacticalRegionSnapshot storedLocalCombatRegion,
        BattleTacticalRegionSnapshot combatJoinRegion,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
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
                SameFaction(actorFact.Actor, alternateTarget.Actor))
            {
                continue;
            }

            BattleTacticalRegionSnapshot scopedLocalCombatRegion = BattleCombatJoinRegionPlanner.SelectLocalCombatScope(
                actorFact,
                alternateTarget,
                storedLocalCombatRegion,
                combatJoinRegion);
            LocalCombatSituation alternateSituation = LocalCombatSituationBuilder.Build(
                actorFact.Actor,
                alternateTarget.Actor,
                targetFacts.Values.Select(item => item.Actor).ToArray(),
                navigationGraph,
                occupancy,
                currentTimeSeconds,
                scopedLocalCombatRegion);

            BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(actorFact);
            BattleRuntimeActor tickStartTarget = BattleTickStartProjectionBuilder.Build(alternateTarget);
            int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
            int movementGap = BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, alternateTarget.Actor, alternateTarget.Anchor);
            bool preferSupport = BattleTargetSelectionService.IsTargetEngagedBySameFactionActor(targetFacts, actorFact, alternateTarget);
            bool preferSupportSlots = preferSupport &&
                                      movementGap > attackRange + 1 &&
                                      !BattleTargetSelectionService.HasReachableAttackSlot(
                                          tickStartActor,
                                          tickStartTarget,
                                          actorFact.Anchor,
                                          navigationGraph,
                                          occupancy,
                                          flowFields,
                                          performanceCounters,
                                          scopedLocalCombatRegion);
            BattleRuntimeAiActionRequest request = null;
            BattleCombatSlotIntent? selectedIntent = null;
            IReadOnlyList<BattleGridCoord> moveOptions = System.Array.Empty<BattleGridCoord>();
            if (alternateSituation != null &&
                BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                    tickStartActor,
                    tickStartTarget,
                    actorFact.Anchor,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    flowFields,
                    preferSupportSlots,
                    performanceCounters,
                    scopedLocalCombatRegion,
                    out BattleCombatSlotIntent selectedSlotIntent,
                    out IReadOnlyList<BattleGridCoord> slotMoveOptions) &&
                slotMoveOptions.Count > 0)
            {
                selectedIntent = selectedSlotIntent;
                moveOptions = slotMoveOptions;
                string joinReason = alternateSituation.BlocksObjectiveRoute
                    ? LocalCombatDecisionReason.JoinBlocksObjectiveRoute
                    : LocalCombatDecisionReason.JoinRecentDamage;
                request = selectedSlotIntent.Kind == BattleCombatSlotKind.Support
                    ? BattleRuntimeAiActionRequest.HoldSupport(
                        actorFact.Actor.ActorId,
                        alternateTarget.Actor.ActorId,
                        LocalCombatDecisionReason.HoldSupportAttackSlotsFull,
                        alternateSituation.SituationId)
                    : BattleRuntimeAiActionRequest.JoinLocalCombat(
                        actorFact.Actor.ActorId,
                        alternateTarget.Actor.ActorId,
                        joinReason,
                        alternateSituation.SituationId);
            }

            if (moveOptions.Count == 0)
            {
                moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget(
                    tickStartActor,
                    tickStartTarget,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    flowFields,
                    preferSupportSlots,
                    avoidOpeningNewAxisGapNearEngagedTarget: false,
                    performanceCounters,
                    scopedLocalCombatRegion);
                request = moveOptions.Count == 0
                    ? null
                    : BattleRuntimeAiActionRequest.AdvanceTowardTarget(
                        actorFact.Actor.ActorId,
                        alternateTarget.Actor.ActorId);
            }

            if (request == null || moveOptions.Count == 0)
            {
                continue;
            }

            // This is still zone-scoped: a retained target that cannot accept
            // ingress yields to another executable candidate in the same action zone.
            context = CreateContext(
                request,
                actorFact,
                alternateTarget,
                hasMoveTo: true,
                moveTo: moveOptions[0],
                failureReason: "",
                moveOptions: moveOptions,
                localCombatSituation: alternateSituation,
                movementReasonCode: request.ReasonCode,
                combatSlotIntent: selectedIntent);
            return true;
        }

        return false;
    }

    private static int NormalizeCandidateScore(int value)
    {
        return value < 0 ? int.MaxValue : value;
    }
}
