using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Builds the actor decision context from tick-start facts. It may ask AI and
// actor-local movement controllers for proposals, but it must not commit world state.
internal static class BattleRuntimeDecisionContextBuilder
{
    internal static BattleRuntimeTickContext Build(
        BattleRuntimeActor actor,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        IBattleRuntimeAiExecutor aiExecutor)
    {
        if (actor == null ||
            string.IsNullOrWhiteSpace(actor.ActorId) ||
            facts == null ||
            !facts.TryGetValue(actor.ActorId, out BattleRuntimeTickStartActorFact actorFact))
        {
            throw new System.InvalidOperationException($"missing runtime tick-start fact: actorId={actor?.ActorId ?? ""}");
        }

        System.ArgumentNullException.ThrowIfNull(aiExecutor);
        BattleMovementController movementController = new(actorFact.Actor);
        BattleMovementProposalWorldInputs movementWorldInputs = new(
            navigationGraph,
            occupancy,
            performanceCounters,
            battleId,
            tick);
        BattleRegionMovementGoal regionMovementGoal = BattleLocalCombatRegionResolver.ResolveRegionMovementGoal(actorFact, tacticalStateStore);
        BattleGroupActionZoneSnapshot combatJoinActionZone = BattleGroupActionZoneResolver.ResolveActorCombatJoinActionZone(
            actorFact,
            groupActionZones,
            combatZones);
        BattleTacticalRegionSnapshot localCombatRegion = BattleLocalCombatRegionResolver.ResolveEngagedLocalCombatRegion(actorFact, tacticalStateStore);
        BattleTacticalRegionSnapshot combatJoinRegion = BattleGroupActionZoneBuilder.ToLocalCombatRegion(combatJoinActionZone);
        if (movementController.TryBuildOutsiderAdvanceContext(
                actorFact,
                combatJoinActionZone,
                movementWorldInputs,
                out BattleRuntimeTickContext combatJoinContext))
        {
            return combatJoinContext;
        }

        BattleTacticalRegionSnapshot scopedLocalCombatRegion = localCombatRegion ?? combatJoinRegion;
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> targetFacts = combatJoinActionZone != null
            ? BattleGroupActionZoneResolver.FilterFactsToActionZone(facts, actorFact, combatJoinActionZone)
            : localCombatRegion == null
                ? facts
                : BattleLocalCombatRegionResolver.FilterFactsToLocalCombatRegion(facts, actorFact, localCombatRegion);
        bool useCommandScopedTargets = regionMovementGoal == null || actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst;
        BattleTargetSelectionService.BattleTargetCandidateSet targetCandidateSet = combatJoinActionZone != null
            ? BattleTargetSelectionService.BuildCombatZoneScopedTargetCandidates(
                targetFacts, actorFact, navigationGraph, occupancy, performanceCounters, combatJoinRegion)
            : useCommandScopedTargets
            ? BattleTargetSelectionService.BuildTargetCandidatesForCommand(
                targetFacts,
                actorFact,
                navigationGraph,
                occupancy,
                performanceCounters)
            : BattleTargetSelectionService.BuildRegionScopedTargetCandidates(targetFacts, actorFact);
        // Target choice remains the behavior-tree boundary; this builder only scopes candidate facts.
        BattleRuntimeAiDecisionFacts targetSelectionFacts = BattleAiActionRequestBuilder.BuildDecisionFacts(
            actorFact,
            null,
            null,
            targetCandidateSet.Candidates,
            targetCandidateSet.SelectionPolicy);
        BattleRuntimeAiActionRequest targetSelectionRequest = aiExecutor.ChooseAction(targetSelectionFacts);
        BattleRuntimeTickStartActorFact? selectedTarget = ResolveRequestedTarget(
            facts,
            actorFact,
            null,
            targetSelectionRequest);
        scopedLocalCombatRegion = BattleCombatJoinRegionPlanner.SelectLocalCombatScope(actorFact, selectedTarget, localCombatRegion, combatJoinRegion);
        LocalCombatSituation localCombatSituation = selectedTarget == null
            ? null
            : LocalCombatSituationBuilder.Build(
                actorFact.Actor,
                selectedTarget.Value.Actor,
                facts.Values.Select(item => item.Actor).ToArray(),
                navigationGraph,
                occupancy,
                currentTimeSeconds,
                scopedLocalCombatRegion);
        BattleRuntimeAiActionRequest request = BattleAiActionRequestBuilder.BuildCommandScopedRequest(
            actorFact,
            selectedTarget,
            localCombatSituation,
            regionMovementGoal,
            aiExecutor,
            targetCandidateSet.Candidates,
            targetCandidateSet.SelectionPolicy);
        BattleRuntimeTickStartActorFact? requestedTarget = ResolveRequestedTarget(facts, actorFact, selectedTarget, request);

        if (!string.Equals(request.ActorId, actor.ActorId, System.StringComparison.Ordinal))
        {
            return BattleRuntimeTickContextFactory.Create(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: false,
                moveTo: default,
                failureReason: "invalid_actor",
                localCombatSituation: localCombatSituation,
                regionMovementGoal: regionMovementGoal);
        }

        if (request.Kind == BattleRuntimeAiActionKind.Hold)
        {
            if (combatJoinActionZone != null &&
                localCombatSituation != null &&
                requestedTarget != null &&
                BattleCombatZoneJoinRetargeting.IsBlockedLocalCombatHold(request.FailureReason) &&
                string.Equals(requestedTarget.Value.Actor.ActorId ?? "", actorFact.TargetActorId ?? "", System.StringComparison.Ordinal) &&
                BattleCombatZoneJoinRetargeting.TryBuildAlternateCombatZoneJoinContext(
                    actorFact,
                    requestedTarget.Value,
                    targetFacts,
                    targetCandidateSet.Candidates,
                    localCombatRegion,
                    combatJoinRegion,
                    movementController,
                    movementWorldInputs,
                    currentTimeSeconds,
                    out BattleRuntimeTickContext alternateCombatJoinContext))
            {
                return alternateCombatJoinContext;
            }

            if (movementController.TryBuildPressureAdvanceContext(
                    actorFact,
                    requestedTarget,
                    localCombatSituation,
                    movementWorldInputs,
                    out BattleRuntimeTickContext pressureHoldContext))
            {
                return pressureHoldContext;
            }

            return BattleRuntimeTickContextFactory.Create(request, actorFact, requestedTarget, false, default, request.FailureReason, localCombatSituation: localCombatSituation, regionMovementGoal: regionMovementGoal);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return movementController.BuildMovementProposalContext(
                new BattleMovementProposalBuildRequest(request, actorFact, requestedTarget, localCombatSituation, regionMovementGoal),
                movementWorldInputs);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective)
        {
            return movementController.BuildMovementProposalContext(
                new BattleMovementProposalBuildRequest(request, actorFact, requestedTarget, localCombatSituation, regionMovementGoal),
                movementWorldInputs);
        }

        if (requestedTarget == null)
        {
            return BattleRuntimeTickContextFactory.Create(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: false,
                moveTo: default,
                failureReason: "invalid_target",
                localCombatSituation: localCombatSituation,
                regionMovementGoal: regionMovementGoal);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
            request.Kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
            request.Kind == BattleRuntimeAiActionKind.HoldSupport)
        {
            bool targetEngagedBySameFactionActor = BattleTargetSelectionService.IsTargetEngagedBySameFactionActor(
                facts,
                actorFact,
                requestedTarget.Value);
            BattleTargetMovementProposalResult targetMovementProposal = movementController.BuildTargetMovementProposalContext(
                new BattleTargetMovementProposalBuildRequest(
                    request,
                    actorFact,
                    requestedTarget.Value,
                    targetEngagedBySameFactionActor,
                    localCombatSituation,
                    scopedLocalCombatRegion,
                    new BattleMovementReservationMap(),
                    UseStoredCombatSlotIntent: false,
                    MovementReasonCode: request.ReasonCode),
                movementWorldInputs);
            if (targetMovementProposal.Context != null)
            {
                return targetMovementProposal.Context;
            }

            if (combatJoinActionZone != null &&
                localCombatSituation != null &&
                string.Equals(requestedTarget.Value.Actor.ActorId ?? "", actorFact.TargetActorId ?? "", System.StringComparison.Ordinal) &&
                BattleCombatZoneJoinRetargeting.TryBuildAlternateCombatZoneJoinContext(
                    actorFact,
                    requestedTarget.Value,
                    targetFacts,
                    targetCandidateSet.Candidates,
                    localCombatRegion,
                    combatJoinRegion,
                    movementController,
                    movementWorldInputs,
                    currentTimeSeconds,
                    out BattleRuntimeTickContext alternateCombatJoinContext))
            {
                return alternateCombatJoinContext;
            }

            if (movementController.TryBuildPressureAdvanceContext(
                    actorFact,
                    requestedTarget,
                    localCombatSituation,
                    movementWorldInputs,
                    out BattleRuntimeTickContext pressureAdvanceContext))
            {
                return pressureAdvanceContext;
            }

            // Runtime validation names blocked local-combat ingress, but
            // target/support selection stays with commander/local-combat request construction.
            string failureReason = string.IsNullOrWhiteSpace(targetMovementProposal.FailureReason)
                ? "path_not_found"
                : targetMovementProposal.FailureReason;
            BattleRuntimeAdvanceDiagnostics.LogAdvanceFailureDiagnostic(
                battleId,
                tick,
                actorFact,
                requestedTarget.Value,
                navigationGraph,
                failureReason,
                default,
                navigationFailureDiagnostics);
            return BattleRuntimeTickContextFactory.Create(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: false,
                moveTo: default,
                failureReason: failureReason,
                localCombatSituation: localCombatSituation);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AttackTarget ||
            request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge)
        {
            return BattleRuntimeTickContextFactory.Create(request, actorFact, requestedTarget, false, default, "", localCombatSituation: localCombatSituation);
        }

        return BattleRuntimeTickContextFactory.Create(
            request,
            actorFact,
            requestedTarget,
            hasMoveTo: false,
            moveTo: default,
            failureReason: "unsupported_action",
            localCombatSituation: localCombatSituation);
    }

    private static BattleRuntimeTickStartActorFact? ResolveRequestedTarget(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? fallbackTarget,
        BattleRuntimeAiActionRequest request)
    {
        if (request == null ||
            request.Kind == BattleRuntimeAiActionKind.Hold ||
            request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
            request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
            request.Kind == BattleRuntimeAiActionKind.ReturnToObjective ||
            request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge && fallbackTarget == null)
        {
            return request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                   request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                   request?.Kind == BattleRuntimeAiActionKind.ReturnToObjective
                ? null
                : fallbackTarget;
        }

        if (string.IsNullOrWhiteSpace(request.TargetActorId))
        {
            return fallbackTarget;
        }

        if (!facts.TryGetValue(request.TargetActorId, out BattleRuntimeTickStartActorFact requestedTarget) ||
            requestedTarget.HitPoints <= 0 ||
            BattleRuntimeIdentityRules.SameFaction(actorFact.Actor, requestedTarget.Actor))
        {
            return fallbackTarget;
        }

        return requestedTarget;
    }
}
