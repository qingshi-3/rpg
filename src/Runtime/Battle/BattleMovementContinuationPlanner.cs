using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleMovementContinuationPlanner
{
    internal static IReadOnlyList<BattleRuntimeTickContext> BuildContinuationContexts(
        IReadOnlyCollection<string> movementCompletedActorIds,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        if (movementCompletedActorIds == null ||
            movementCompletedActorIds.Count == 0 ||
            tickStartFacts == null)
        {
            return System.Array.Empty<BattleRuntimeTickContext>();
        }

        List<BattleRuntimeTickContext> contexts = new();
        foreach (string actorId in movementCompletedActorIds.OrderBy(item => item, System.StringComparer.Ordinal))
        {
            if (!tickStartFacts.TryGetValue(actorId ?? "", out BattleRuntimeTickStartActorFact actorFact) ||
                !TryBuildContinuationContext(
                    actorFact,
                    tickStartFacts,
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters,
                    battleId,
                    currentTimeSeconds,
                    tick,
                    navigationFailureDiagnostics,
                    tacticalStateStore,
                    out BattleRuntimeTickContext context))
            {
                continue;
            }

            context.AllowStaleTargetRetarget = false;
            // Continuation is only valid for the same stored movement intent.
            // If its next selected step loses reservation, do not reinterpret the
            // chain through an alternate step in the same tick.
            context.AllowReservationFallback = false;
            contexts.Add(context);
        }

        return contexts;
    }

    internal static void ClearEndedMovementChains(
        IEnumerable<BattleRuntimeActor> actors,
        IReadOnlyCollection<string> movementCompletedActorIds)
    {
        if (actors == null || movementCompletedActorIds == null || movementCompletedActorIds.Count == 0)
        {
            return;
        }

        HashSet<string> completed = new(movementCompletedActorIds, System.StringComparer.Ordinal);
        foreach (BattleRuntimeActor actor in actors)
        {
            if (actor == null ||
                !completed.Contains(actor.ActorId ?? "") ||
                actor.Phase == BattleRuntimeActorPhase.Moving && actor.HasMovementTarget)
            {
                continue;
            }

            BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(actor);
        }
    }

    private static bool TryBuildContinuationContext(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore,
        out BattleRuntimeTickContext context)
    {
        context = null;
        BattleRuntimeActor actor = actorFact.Actor;
        if (actor == null ||
            actor.HitPoints <= 0 ||
            actor.Phase != BattleRuntimeActorPhase.AnchoredDecision ||
            !actor.HasMovementIntentSnapshot ||
            !CommandStillMatches(actor, actorFact) ||
            ReachedObjectiveBoundary(actorFact))
        {
            return false;
        }

        return actor.MovementIntentKind switch
        {
            BattleRuntimeAiActionKind.AdvanceTowardObjective => TryBuildObjectiveContinuation(
                actorFact,
                facts,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                tick,
                BattleRuntimeAiActionRequest.AdvanceTowardObjective(actor.ActorId),
                out context),
            BattleRuntimeAiActionKind.ReturnToObjective => TryBuildObjectiveContinuation(
                actorFact,
                facts,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                tick,
                BattleRuntimeAiActionRequest.ReturnToObjective(
                    actor.ActorId,
                    actor.MovementIntentReasonCode,
                    actor.MovementIntentLocalCombatSituationId),
                out context),
            BattleRuntimeAiActionKind.AdvanceTowardRegion => TryBuildRegionContinuation(
                actorFact,
                facts,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                tick,
                tacticalStateStore,
                out context),
            BattleRuntimeAiActionKind.AdvanceTowardTarget or
                BattleRuntimeAiActionKind.JoinLocalCombat or
                BattleRuntimeAiActionKind.HoldSupport => TryBuildTargetContinuation(
                    actorFact,
                    facts,
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters,
                    currentTimeSeconds,
                    tacticalStateStore,
                    out context),
            _ => false
        };
    }

    private static bool TryBuildObjectiveContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick,
        BattleRuntimeAiActionRequest request,
        out BattleRuntimeTickContext context)
    {
        context = null;
        BattleRuntimeActor actor = actorFact.Actor;
        if (actor?.HasObjectiveAnchor != true ||
            !string.Equals(actor.ObjectiveZoneId ?? "", actor.MovementIntentObjectiveZoneId ?? "", System.StringComparison.Ordinal) ||
            HasLocallyPerceivedHostile(actorFact, facts) ||
            BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact))
        {
            return false;
        }

        BattleRuntimeTickContext candidate = BattleObjectiveAdvancePlanner.BuildObjectiveAdvanceContext(
            request,
            actorFact,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            battleId,
            tick);
        if (!IsUsableMoveContext(candidate))
        {
            return false;
        }

        context = candidate;
        return true;
    }

    private static bool TryBuildRegionContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick,
        BattleGroupTacticalStateStore tacticalStateStore,
        out BattleRuntimeTickContext context)
    {
        context = null;
        BattleRuntimeActor actor = actorFact.Actor;
        BattleRegionMovementGoal goal = BattleLocalCombatRegionResolver.ResolveRegionMovementGoal(actorFact, tacticalStateStore);
        if (goal == null ||
            !string.Equals(goal.RegionId ?? "", actor?.MovementIntentRegionId ?? "", System.StringComparison.Ordinal) ||
            !ReasonStillMatches(actor, goal.ReasonCode) ||
            HasLocallyPerceivedHostile(actorFact, facts) ||
            IsRegionReached(actorFact, goal))
        {
            return false;
        }

        BattleRuntimeTickContext candidate = BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext(
            BattleRuntimeAiActionRequest.AdvanceTowardRegion(actor.ActorId, goal),
            actorFact,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            battleId,
            tick);
        if (!IsUsableMoveContext(candidate))
        {
            return false;
        }

        context = candidate;
        return true;
    }

    private static bool TryBuildTargetContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        double currentTimeSeconds,
        BattleGroupTacticalStateStore tacticalStateStore,
        out BattleRuntimeTickContext context)
    {
        context = null;
        BattleRuntimeActor actor = actorFact.Actor;
        string targetActorId = actor?.MovementIntentTargetActorId ?? "";
        if (string.IsNullOrWhiteSpace(targetActorId) ||
            !string.Equals(actor.TargetActorId ?? "", targetActorId, System.StringComparison.Ordinal) ||
            facts == null ||
            !facts.TryGetValue(targetActorId, out BattleRuntimeTickStartActorFact targetFact) ||
            targetFact.HitPoints <= 0 ||
            BattleRuntimeTickResolver.SameFaction(actor, targetFact.Actor))
        {
            return false;
        }

        int attackRange = System.Math.Max(1, actor.AttackRange);
        int movementGap = BattleActorFootprint.GetGap(actor, actorFact.Anchor, targetFact.Actor, targetFact.Anchor);
        int attackGap = BattleRuntimeTickResolver.GetOrthogonalAttackGap(actor, actorFact.Anchor, targetFact.Actor, targetFact.Anchor);
        if (attackGap <= attackRange && actor.MovementIntentKind != BattleRuntimeAiActionKind.HoldSupport)
        {
            return false;
        }

        BattleTacticalRegionSnapshot localCombatRegion = BattleLocalCombatRegionResolver.ResolveEngagedLocalCombatRegion(
            actorFact,
            tacticalStateStore);
        LocalCombatSituation localCombatSituation = BuildMatchingLocalCombatSituation(
            actorFact,
            targetFact,
            facts,
            navigationGraph,
            occupancy,
            currentTimeSeconds,
            localCombatRegion);
        if (RequiresLocalCombatSituation(actor) && localCombatSituation == null)
        {
            return false;
        }

        BattleRuntimeAiActionRequest request = CreateTargetRequest(actor);
        BattleRuntimeActor tickStartActor = BattleRuntimeTickResolver.BuildTickStartProjection(actorFact);
        BattleRuntimeActor tickStartTarget = BattleRuntimeTickResolver.BuildTickStartProjection(targetFact);
        bool preferSupport = actor.MovementIntentKind == BattleRuntimeAiActionKind.HoldSupport ||
                             BattleTargetSelectionService.IsTargetEngagedBySameFactionActor(facts, actorFact, targetFact);
        bool preferSupportSlots = actor.MovementIntentKind == BattleRuntimeAiActionKind.HoldSupport ||
                                  preferSupport &&
                                  movementGap > attackRange + 1 &&
                                  !BattleTargetSelectionService.HasReachableAttackSlot(
                                      tickStartActor,
                                      tickStartTarget,
                                      actorFact.Anchor,
                                      navigationGraph,
                                      occupancy,
                                      flowFields,
                                      performanceCounters,
                                      localCombatRegion);
        bool avoidOpeningNewAxisGapNearEngagedTarget = preferSupport && movementGap == attackRange + 1;
        BattleCombatSlotIntent? combatSlotIntent = null;
        IReadOnlyList<BattleGridCoord> moveOptions;
        if (actor.HasMovementIntentCombatSlot)
        {
            if (BattleCombatSlotIntentResolver.TryResolveStoredIntent(
                    actor,
                    tickStartTarget,
                    navigationGraph,
                    occupancy,
                    localCombatRegion,
                    out BattleCombatSlotIntent storedSlotIntent))
            {
                moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot(
                    tickStartActor,
                    storedSlotIntent.Anchor,
                    storedSlotIntent.Kind,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    performanceCounters,
                    localCombatRegion);
                combatSlotIntent = moveOptions.Count > 0 ? storedSlotIntent : null;
            }
            else
            {
                moveOptions = System.Array.Empty<BattleGridCoord>();
            }

            if (moveOptions.Count == 0 &&
                BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                    tickStartActor,
                    tickStartTarget,
                    actorFact.Anchor,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    preferSupportSlots,
                    performanceCounters,
                    localCombatRegion,
                    out BattleCombatSlotIntent replacementSlotIntent,
                    out IReadOnlyList<BattleGridCoord> replacementMoveOptions))
            {
                combatSlotIntent = replacementSlotIntent;
                moveOptions = replacementMoveOptions;
            }
        }
        else
        {
            if (localCombatSituation != null &&
                BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                    tickStartActor,
                    tickStartTarget,
                    actorFact.Anchor,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    preferSupportSlots,
                    performanceCounters,
                    localCombatRegion,
                    out BattleCombatSlotIntent selectedSlotIntent,
                    out IReadOnlyList<BattleGridCoord> selectedMoveOptions))
            {
                combatSlotIntent = selectedSlotIntent;
                moveOptions = selectedMoveOptions;
            }
            else if (localCombatSituation == null)
            {
                moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget(
                    tickStartActor,
                    tickStartTarget,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    flowFields,
                    preferSupportSlots,
                    avoidOpeningNewAxisGapNearEngagedTarget,
                    performanceCounters,
                    localCombatRegion);
            }
            else
            {
                moveOptions = System.Array.Empty<BattleGridCoord>();
            }
        }
        if (moveOptions.Count == 0)
        {
            return false;
        }

        context = BattleRuntimeTickResolver.CreateContext(
            request,
            actorFact,
            targetFact,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions,
            localCombatSituation: localCombatSituation,
            movementReasonCode: actor.MovementIntentReasonCode,
            combatSlotIntent: combatSlotIntent);
        return true;
    }

    private static LocalCombatSituation BuildMatchingLocalCombatSituation(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact targetFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        double currentTimeSeconds,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        BattleRuntimeActor actor = actorFact.Actor;
        if (actor == null ||
            string.IsNullOrWhiteSpace(actor.MovementIntentLocalCombatSituationId) &&
            actor.MovementIntentKind != BattleRuntimeAiActionKind.JoinLocalCombat &&
            actor.MovementIntentKind != BattleRuntimeAiActionKind.HoldSupport)
        {
            return null;
        }

        LocalCombatSituation situation = LocalCombatSituationBuilder.Build(
            actor,
            targetFact.Actor,
            facts.Values.Select(item => item.Actor).ToArray(),
            navigationGraph,
            occupancy,
            currentTimeSeconds,
            localCombatRegion);
        if (situation == null ||
            !string.Equals(situation.TargetActorId ?? "", actor.MovementIntentTargetActorId ?? "", System.StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(actor.MovementIntentLocalCombatSituationId) &&
            !string.Equals(situation.SituationId ?? "", actor.MovementIntentLocalCombatSituationId, System.StringComparison.Ordinal))
        {
            return null;
        }

        return situation;
    }

    private static BattleRuntimeAiActionRequest CreateTargetRequest(BattleRuntimeActor actor)
    {
        return actor.MovementIntentKind switch
        {
            BattleRuntimeAiActionKind.JoinLocalCombat => BattleRuntimeAiActionRequest.JoinLocalCombat(
                actor.ActorId,
                actor.MovementIntentTargetActorId,
                actor.MovementIntentReasonCode,
                actor.MovementIntentLocalCombatSituationId),
            BattleRuntimeAiActionKind.HoldSupport => BattleRuntimeAiActionRequest.HoldSupport(
                actor.ActorId,
                actor.MovementIntentTargetActorId,
                actor.MovementIntentReasonCode,
                actor.MovementIntentLocalCombatSituationId),
            _ => BattleRuntimeAiActionRequest.AdvanceTowardTarget(actor.ActorId, actor.MovementIntentTargetActorId)
        };
    }

    private static bool RequiresLocalCombatSituation(BattleRuntimeActor actor)
    {
        return actor?.MovementIntentKind == BattleRuntimeAiActionKind.JoinLocalCombat ||
               actor?.MovementIntentKind == BattleRuntimeAiActionKind.HoldSupport ||
               !string.IsNullOrWhiteSpace(actor?.MovementIntentLocalCombatSituationId);
    }

    private static bool CommandStillMatches(BattleRuntimeActor actor, BattleRuntimeTickStartActorFact actorFact)
    {
        string currentCommand = actor?.CommandId ?? actorFact.CommandId ?? "";
        return string.Equals(currentCommand, actor?.MovementIntentCommandId ?? "", System.StringComparison.Ordinal);
    }

    private static bool ReachedObjectiveBoundary(BattleRuntimeTickStartActorFact actorFact)
    {
        return actorFact.Actor?.HasObjectiveAnchor == true &&
               BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact);
    }

    private static bool ReasonStillMatches(BattleRuntimeActor actor, string currentReasonCode)
    {
        return string.IsNullOrWhiteSpace(actor?.MovementIntentReasonCode) ||
               string.Equals(actor.MovementIntentReasonCode, currentReasonCode ?? "", System.StringComparison.Ordinal);
    }

    private static bool HasLocallyPerceivedHostile(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts)
    {
        if (actorFact.Actor == null || facts == null)
        {
            return false;
        }

        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor == null ||
                candidate.Actor.HitPoints <= 0 ||
                string.Equals(candidate.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", System.StringComparison.Ordinal) ||
                BattleRuntimeTickResolver.SameFaction(actorFact.Actor, candidate.Actor))
            {
                continue;
            }

            int gap = BattleActorFootprint.GetGap(
                actorFact.Actor,
                actorFact.Anchor,
                candidate.Actor,
                candidate.Anchor);
            int heightGap = System.Math.Abs(actorFact.Anchor.Height - candidate.Anchor.Height);
            if (gap + heightGap <= BattlePerceptionPolicy.DefaultLocalPerceptionRange)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRegionReached(BattleRuntimeTickStartActorFact actorFact, BattleRegionMovementGoal goal)
    {
        if (goal == null)
        {
            return false;
        }

        BattleRuntimeActor region = new()
        {
            GridX = goal.CenterCellX,
            GridY = goal.CenterCellY,
            GridHeight = goal.CenterCellHeight,
            FootprintWidth = System.Math.Max(1, goal.Width),
            FootprintHeight = System.Math.Max(1, goal.Height)
        };
        BattleGridCoord regionAnchor = new(goal.CenterCellX, goal.CenterCellY, goal.CenterCellHeight);
        return BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, region, regionAnchor) <= 1;
    }

    private static bool IsUsableMoveContext(BattleRuntimeTickContext context)
    {
        return context?.Proposal?.HasMoveTo == true &&
               string.IsNullOrWhiteSpace(context.Proposal.FailureReason);
    }
}
