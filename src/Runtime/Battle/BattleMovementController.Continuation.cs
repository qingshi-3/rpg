using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleMovementController
{
    internal static IReadOnlyList<BattleRuntimeTickContext> BuildContinuationContexts(
        IReadOnlyCollection<string> movementCompletedActorIds,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap candidateReservations,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones)
    {
        if (movementCompletedActorIds == null ||
            movementCompletedActorIds.Count == 0 ||
            tickStartFacts == null)
        {
            return System.Array.Empty<BattleRuntimeTickContext>();
        }

        if (candidateReservations == null)
        {
            throw new System.InvalidOperationException("movement continuation reservations missing");
        }

        List<BattleRuntimeTickContext> contexts = new();
        foreach (string actorId in movementCompletedActorIds.OrderBy(item => item, System.StringComparer.Ordinal))
        {
            if (!tickStartFacts.TryGetValue(actorId ?? "", out BattleRuntimeTickStartActorFact actorFact))
            {
                continue;
            }

            BattleMovementController movementController = new(actorFact.Actor);
            if (!movementController.BuildContinuationContext(
                    actorFact,
                    tickStartFacts,
                    navigationGraph,
                    occupancy,
                    candidateReservations,
                    performanceCounters,
                    battleId,
                    currentTimeSeconds,
                    tick,
                    navigationFailureDiagnostics,
                    tacticalStateStore,
                    groupActionZones,
                    combatZones,
                    out BattleRuntimeTickContext context))
            {
                continue;
            }

            contexts.Add(context);
        }

        return contexts;
    }

    internal bool BuildContinuationContext(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap candidateReservations,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        out BattleRuntimeTickContext context)
    {
        context = null;
        if (!ReferenceEquals(_actor, actorFact.Actor))
        {
            throw new System.InvalidOperationException($"movement context actor mismatch: actor={_actor?.ActorId ?? ""} contextActor={actorFact.Actor?.ActorId ?? ""}");
        }

        if (candidateReservations == null)
        {
            throw new System.InvalidOperationException($"movement continuation reservations missing: actor={_actor?.ActorId ?? ""}");
        }

        if (!TryBuildActorContinuationContext(
                actorFact,
                tickStartFacts,
                navigationGraph,
                occupancy,
                candidateReservations,
                performanceCounters,
                battleId,
                currentTimeSeconds,
                tick,
                navigationFailureDiagnostics,
                tacticalStateStore,
                groupActionZones,
                combatZones,
                out context))
        {
            return false;
        }

        context.AllowStaleTargetRetarget = false;
        // Continuation remains tied to the stored movement intent; if this
        // exact next step loses reservation, wait for the next actor decision.
        context.AllowReservationFallback = false;
        return true;
    }

    private bool TryBuildActorContinuationContext(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap candidateReservations,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
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

        BattleGroupActionZoneSnapshot combatJoinActionZone = BattleGroupActionZoneResolver.ResolveActorCombatJoinActionZone(
            actorFact,
            groupActionZones,
            combatZones);
        BattleMovementProposalWorldInputs movementWorldInputs = new(
            navigationGraph,
            occupancy,
            performanceCounters,
            battleId,
            tick);
        if (TryBuildOutsiderAdvanceContext(
                actorFact,
                combatJoinActionZone,
                movementWorldInputs,
                out BattleRuntimeTickContext joinContext))
        {
            if (!IsUsableMoveContext(joinContext))
            {
                return false;
            }

            context = joinContext;
            return true;
        }

        return actor.MovementIntentKind switch
        {
            BattleRuntimeAiActionKind.AdvanceTowardObjective => TryBuildObjectiveContinuation(
                actorFact,
                facts,
                navigationGraph,
                occupancy,
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
                    candidateReservations,
                    performanceCounters,
                    currentTimeSeconds,
                    battleId,
                    tick,
                    tacticalStateStore,
                    groupActionZones,
                    combatZones,
                    out context),
            _ => false
        };
    }

    private bool TryBuildObjectiveContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
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

        BattleRuntimeTickContext candidate = BuildMovementProposalContext(
            new BattleMovementProposalBuildRequest(
                request,
                actorFact,
                null,
                null,
                null),
            new BattleMovementProposalWorldInputs(
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick));
        if (!IsUsableMoveContext(candidate))
        {
            return false;
        }

        context = candidate;
        return true;
    }

    private bool TryBuildRegionContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
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

        BattleRuntimeAiActionRequest request = BattleRuntimeAiActionRequest.AdvanceTowardRegion(actor.ActorId, goal);
        BattleRuntimeTickContext candidate = BuildMovementProposalContext(
            new BattleMovementProposalBuildRequest(
                request,
                actorFact,
                null,
                null,
                goal),
            new BattleMovementProposalWorldInputs(
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick));
        if (!IsUsableMoveContext(candidate))
        {
            return false;
        }

        context = candidate;
        return true;
    }

    private bool TryBuildTargetContinuation(
        BattleRuntimeTickStartActorFact actorFact,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap candidateReservations,
        BattlePerformanceCounters performanceCounters,
        double currentTimeSeconds,
        string battleId,
        int tick,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
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
            BattleRuntimeIdentityRules.SameFaction(actor, targetFact.Actor))
        {
            return false;
        }

        int attackRange = System.Math.Max(1, actor.AttackRange);
        if (BattleTargetSelectionService.FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) != null)
        {
            // Movement continuation is not target acquisition. Once a movement
            // boundary lands in attack range of any enemy, the chain stops so
            // the next actor decision can attack or wait for charge instead of
            // walking past the frontline toward an older slot.
            return false;
        }

        int attackGap = BattleCombatGeometry.GetOrthogonalAttackGap(actor, actorFact.Anchor, targetFact.Actor, targetFact.Anchor);
        if (attackGap <= attackRange && actor.MovementIntentKind != BattleRuntimeAiActionKind.HoldSupport)
        {
            return false;
        }

        BattleTacticalRegionSnapshot localCombatRegion = BattleLocalCombatRegionResolver.ResolveEngagedLocalCombatRegion(
            actorFact,
            tacticalStateStore);
        BattleGroupActionZoneSnapshot combatJoinActionZone = BattleGroupActionZoneResolver.ResolveActorCombatJoinActionZone(
            actorFact,
            groupActionZones,
            combatZones);
        BattleTacticalRegionSnapshot combatJoinRegion = BattleGroupActionZoneBuilder.ToLocalCombatRegion(combatJoinActionZone);
        BattleTacticalRegionSnapshot scopedLocalCombatRegion = BattleCombatJoinRegionPlanner.SelectLocalCombatScope(
            actorFact,
            targetFact,
            localCombatRegion,
            combatJoinRegion);
        LocalCombatSituation localCombatSituation = BuildMatchingLocalCombatSituation(
            actorFact,
            targetFact,
            facts,
            navigationGraph,
            occupancy,
            currentTimeSeconds,
            scopedLocalCombatRegion);
        if (RequiresLocalCombatSituation(actor) && localCombatSituation == null)
        {
            return false;
        }

        BattleRuntimeAiActionRequest request = CreateTargetRequest(actor);
        bool targetEngagedBySameFactionActor = HasSameFactionActorEngagingTarget(facts, actorFact, targetFact);
        BattleTargetMovementProposalResult targetMovementProposal = BuildTargetMovementProposalContext(
            new BattleTargetMovementProposalBuildRequest(
                request,
                actorFact,
                targetFact,
                targetEngagedBySameFactionActor,
                localCombatSituation,
                scopedLocalCombatRegion,
                candidateReservations,
                UseStoredCombatSlotIntent: true,
                MovementReasonCode: actor.MovementIntentReasonCode),
            new BattleMovementProposalWorldInputs(
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick));
        if (targetMovementProposal.Context != null)
        {
            context = targetMovementProposal.Context;
            return true;
        }

        if (TryBuildPressureAdvanceContext(
                actorFact,
                targetFact,
                localCombatSituation,
                new BattleMovementProposalWorldInputs(
                    navigationGraph,
                    occupancy,
                    performanceCounters,
                    battleId,
                    tick),
                out BattleRuntimeTickContext pressureAdvanceContext))
        {
            context = pressureAdvanceContext;
            return true;
        }

        return false;
    }

    private static bool HasSameFactionActorEngagingTarget(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact targetFact)
    {
        if (facts == null || actorFact.Actor == null || targetFact.Actor == null)
        {
            return false;
        }

        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor == null ||
                string.Equals(candidate.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", System.StringComparison.Ordinal) ||
                candidate.HitPoints <= 0 ||
                !BattleRuntimeIdentityRules.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            if (BattleCombatGeometry.GetOrthogonalAttackGap(
                    candidate.Actor,
                    candidate.Anchor,
                    targetFact.Actor,
                    targetFact.Anchor) <= System.Math.Max(1, candidate.Actor.AttackRange))
            {
                return true;
            }
        }

        return false;
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
                candidate.HitPoints <= 0 ||
                string.Equals(candidate.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", System.StringComparison.Ordinal) ||
                BattleRuntimeIdentityRules.SameFaction(actorFact.Actor, candidate.Actor))
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

        int width = System.Math.Max(1, goal.Width);
        int height = System.Math.Max(1, goal.Height);
        BattleGridCoord regionAnchor = new(
            goal.CenterCellX - (width - 1) / 2,
            goal.CenterCellY - (height - 1) / 2,
            goal.CenterCellHeight);
        BattleRuntimeActor region = new()
        {
            GridX = regionAnchor.X,
            GridY = regionAnchor.Y,
            GridHeight = goal.CenterCellHeight,
            FootprintWidth = width,
            FootprintHeight = height
        };
        return BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, region, regionAnchor) <= 1;
    }

    private static bool IsUsableMoveContext(BattleRuntimeTickContext context)
    {
        return context?.Proposal?.HasMoveTo == true &&
               string.IsNullOrWhiteSpace(context.Proposal.FailureReason);
    }
}
