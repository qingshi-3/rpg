using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Actor-local movement owns phase time and intent cleanup. Reservation, topology,
// plan-state events, and movement-start commits remain world/commit authority.
internal sealed partial class BattleMovementController
{
    private readonly BattleRuntimeActor _actor;

    internal BattleMovementController(BattleRuntimeActor actor)
    {
        _actor = actor;
    }

    internal bool AdvanceMovementBoundary(
        double currentTimeSeconds,
        out BattleGridCoord movementFrom,
        out BattleGridCoord movementTo,
        out string boundaryReasonCode) =>
        BattleRuntimeActorStateMachine.AdvanceTimeBoundary(
            _actor,
            currentTimeSeconds,
            out movementFrom,
            out movementTo,
            out boundaryReasonCode);

    internal BattleRuntimeTickContext BuildMovementProposalContext(
        BattleMovementProposalBuildRequest request,
        BattleMovementProposalWorldInputs worldInputs)
    {
        ValidateMovementProposalRequest(request);

        return request.Request.Kind switch
        {
            BattleRuntimeAiActionKind.AdvanceTowardObjective or
                BattleRuntimeAiActionKind.ReturnToObjective => BattleObjectiveAdvancePlanner.BuildObjectiveAdvanceContext(
                    request.Request,
                    request.ActorFact,
                    worldInputs.NavigationGraph,
                    worldInputs.Occupancy,
                    worldInputs.PerformanceCounters,
                    worldInputs.BattleId,
                    worldInputs.Tick),
            BattleRuntimeAiActionKind.AdvanceTowardRegion => BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext(
                request.Request,
                request.ActorFact,
                worldInputs.NavigationGraph,
                worldInputs.Occupancy,
                worldInputs.PerformanceCounters,
                worldInputs.BattleId,
                worldInputs.Tick),
            _ => throw new System.InvalidOperationException($"unsupported movement proposal kind for actor movement controller: actor={_actor?.ActorId ?? ""} kind={request.Request.Kind}")
        };
    }

    private void ValidateMovementProposalRequest(BattleMovementProposalBuildRequest request)
    {
        if (_actor == null || string.IsNullOrWhiteSpace(_actor.ActorId))
        {
            throw new System.InvalidOperationException("movement proposal actor missing");
        }

        if (request.Request == null ||
            request.ActorFact.Actor == null)
        {
            throw new System.InvalidOperationException($"movement proposal request missing actor context: actor={_actor.ActorId}");
        }

        if (!ReferenceEquals(_actor, request.ActorFact.Actor))
        {
            throw new System.InvalidOperationException($"movement proposal actor mismatch: actor={_actor.ActorId} contextActor={request.ActorFact.Actor.ActorId ?? ""}");
        }

        if (!string.Equals(request.Request.ActorId ?? "", _actor.ActorId, System.StringComparison.Ordinal))
        {
            throw new System.InvalidOperationException($"movement proposal request actor mismatch: actor={_actor.ActorId} requestActor={request.Request.ActorId ?? ""}");
        }
    }

    internal bool TryBuildOutsiderAdvanceContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupActionZoneSnapshot actionZone,
        BattleMovementProposalWorldInputs worldInputs,
        out BattleRuntimeTickContext context)
    {
        if (!ReferenceEquals(_actor, actorFact.Actor))
        {
            throw new System.InvalidOperationException($"movement outsider context actor mismatch: actor={_actor?.ActorId ?? ""} contextActor={actorFact.Actor?.ActorId ?? ""}");
        }

        return BattleCombatJoinRegionPlanner.TryBuildOutsiderAdvanceContext(
            actorFact,
            actionZone,
            worldInputs.NavigationGraph,
            worldInputs.Occupancy,
            worldInputs.PerformanceCounters,
            worldInputs.BattleId,
            worldInputs.Tick,
            out context);
    }

    internal bool TryBuildPressureAdvanceContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation,
        BattleMovementProposalWorldInputs worldInputs,
        out BattleRuntimeTickContext context)
    {
        if (!ReferenceEquals(_actor, actorFact.Actor))
        {
            throw new System.InvalidOperationException($"movement pressure context actor mismatch: actor={_actor?.ActorId ?? ""} contextActor={actorFact.Actor?.ActorId ?? ""}");
        }

        return BattleCombatJoinRegionPlanner.TryBuildPressureAdvanceContext(
            actorFact,
            targetFact,
            localCombatSituation,
            worldInputs.NavigationGraph,
            worldInputs.Occupancy,
            worldInputs.PerformanceCounters,
            out context);
    }

    internal BattleTargetMovementProposalResult BuildTargetMovementProposalContext(
        BattleTargetMovementProposalBuildRequest request,
        BattleMovementProposalWorldInputs worldInputs)
    {
        ValidateTargetMovementProposalRequest(request);

        BattleRuntimeActor actor = request.ActorFact.Actor;
        BattleRuntimeTickStartActorFact targetFact = request.TargetFact;
        int attackRange = System.Math.Max(1, actor.AttackRange);
        int movementGap = BattleActorFootprint.GetGap(actor, request.ActorFact.Anchor, targetFact.Actor, targetFact.Anchor);
        int attackGap = BattleCombatGeometry.GetOrthogonalAttackGap(actor, request.ActorFact.Anchor, targetFact.Actor, targetFact.Anchor);
        if (attackGap <= attackRange && request.Request.Kind != BattleRuntimeAiActionKind.HoldSupport)
        {
            return new BattleTargetMovementProposalResult(
                BattleRuntimeTickContextFactory.Create(
                    request.Request,
                    request.ActorFact,
                    targetFact,
                    hasMoveTo: false,
                    moveTo: default,
                    failureReason: "target_already_in_range",
                    localCombatSituation: request.LocalCombatSituation),
                "");
        }

        BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(request.ActorFact);
        BattleRuntimeActor tickStartTarget = BattleTickStartProjectionBuilder.Build(targetFact);
        bool preferSupport = request.Request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                             request.TargetEngagedBySameFactionActor;
        bool preferSupportSlots = request.Request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                                  preferSupport &&
                                  movementGap > attackRange + 1 &&
                                  !BattleTargetSelectionService.HasReachableAttackSlot(
                                      tickStartActor,
                                      tickStartTarget,
                                      request.ActorFact.Anchor,
                                      worldInputs.NavigationGraph,
                                      worldInputs.Occupancy,
                                      worldInputs.PerformanceCounters,
                                      request.ScopedLocalCombatRegion);
        bool avoidOpeningNewAxisGapNearEngagedTarget = preferSupport && movementGap == attackRange + 1;
        BattleCombatSlotIntent? combatSlotIntent = null;
        IReadOnlyList<BattleGridCoord> moveOptions = System.Array.Empty<BattleGridCoord>();
        if (request.UseStoredCombatSlotIntent && actor.HasMovementIntentCombatSlot)
        {
            bool storedSlotBlocked = false;
            if (BattleCombatSlotIntentResolver.TryResolveStoredIntent(
                    actor,
                    tickStartTarget,
                    worldInputs.NavigationGraph,
                    worldInputs.Occupancy,
                    request.ScopedLocalCombatRegion,
                    out BattleCombatSlotIntent storedSlotIntent))
            {
                moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot(
                    tickStartActor,
                    storedSlotIntent.Anchor,
                    storedSlotIntent.Kind,
                    worldInputs.NavigationGraph,
                    worldInputs.Occupancy,
                    request.CandidateReservations,
                    worldInputs.PerformanceCounters,
                    request.ScopedLocalCombatRegion,
                    allowNonImprovingLocalAvoidance: false);
                combatSlotIntent = moveOptions.Count > 0 ? storedSlotIntent : null;
                storedSlotBlocked = moveOptions.Count == 0;
            }

            // A still-valid stored slot that cannot be improved locally is a
            // queue/hold signal. Do not reinterpret the same movement chain as
            // a fresh slot search, or crowded large units can orbit the fight.
            if (moveOptions.Count == 0 &&
                !storedSlotBlocked &&
                BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                    tickStartActor,
                    tickStartTarget,
                    request.ActorFact.Anchor,
                    worldInputs.NavigationGraph,
                    worldInputs.Occupancy,
                    request.CandidateReservations,
                    preferSupportSlots,
                    worldInputs.PerformanceCounters,
                    request.ScopedLocalCombatRegion,
                    out BattleCombatSlotIntent replacementSlotIntent,
                    out IReadOnlyList<BattleGridCoord> replacementMoveOptions))
            {
                combatSlotIntent = replacementSlotIntent;
                moveOptions = replacementMoveOptions;
            }
        }
        else
        {
            moveOptions = BuildFreshTargetMoveOptions(
                request,
                worldInputs,
                tickStartActor,
                tickStartTarget,
                preferSupportSlots,
                avoidOpeningNewAxisGapNearEngagedTarget,
                out combatSlotIntent);
        }

        if (moveOptions.Count == 0)
        {
            string failureReason = request.LocalCombatSituation != null
                ? request.LocalCombatSituation.HasReachableSupportSlot
                    ? LocalCombatDecisionReason.HoldSupportAttackSlotsFull
                    : LocalCombatDecisionReason.RejectNoReachableSlot
                : "path_not_found";
            return new BattleTargetMovementProposalResult(null, failureReason);
        }

        return new BattleTargetMovementProposalResult(
            BattleRuntimeTickContextFactory.Create(
                request.Request,
                request.ActorFact,
                targetFact,
                hasMoveTo: true,
                moveTo: moveOptions[0],
                failureReason: "",
                moveOptions: moveOptions,
                localCombatSituation: request.LocalCombatSituation,
                movementReasonCode: request.MovementReasonCode,
                combatSlotIntent: combatSlotIntent),
            "");
    }

    internal bool TryBuildAlternateCombatZoneJoinProposalContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact alternateTarget,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> targetFacts,
        bool targetEngagedBySameFactionActor,
        BattleTacticalRegionSnapshot storedLocalCombatRegion,
        BattleTacticalRegionSnapshot combatJoinRegion,
        BattleMovementReservationMap candidateReservations,
        BattleMovementProposalWorldInputs worldInputs,
        double currentTimeSeconds,
        out BattleRuntimeTickContext context)
    {
        context = null;
        if (!ReferenceEquals(_actor, actorFact.Actor))
        {
            throw new System.InvalidOperationException($"alternate combat-zone join proposal actor mismatch: actor={_actor?.ActorId ?? ""} contextActor={actorFact.Actor?.ActorId ?? ""}");
        }

        if (targetFacts == null)
        {
            return false;
        }

        if (candidateReservations == null)
        {
            throw new System.InvalidOperationException($"alternate combat-zone join proposal reservations missing: actor={_actor?.ActorId ?? ""}");
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
            worldInputs.NavigationGraph,
            worldInputs.Occupancy,
            currentTimeSeconds,
            scopedLocalCombatRegion);

        BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(actorFact);
        BattleRuntimeActor tickStartTarget = BattleTickStartProjectionBuilder.Build(alternateTarget);
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
        int movementGap = BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, alternateTarget.Actor, alternateTarget.Anchor);
        bool preferSupportSlots = targetEngagedBySameFactionActor &&
                                  movementGap > attackRange + 1 &&
                                  !BattleTargetSelectionService.HasReachableAttackSlot(
                                      tickStartActor,
                                      tickStartTarget,
                                      actorFact.Anchor,
                                      worldInputs.NavigationGraph,
                                      worldInputs.Occupancy,
                                      worldInputs.PerformanceCounters,
                                      scopedLocalCombatRegion);
        BattleRuntimeAiActionRequest request = null;
        BattleCombatSlotIntent? selectedIntent = null;
        IReadOnlyList<BattleGridCoord> moveOptions = System.Array.Empty<BattleGridCoord>();
        if (alternateSituation != null &&
            BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                tickStartActor,
                tickStartTarget,
                actorFact.Anchor,
                worldInputs.NavigationGraph,
                worldInputs.Occupancy,
                candidateReservations,
                preferSupportSlots,
                worldInputs.PerformanceCounters,
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
                worldInputs.NavigationGraph,
                worldInputs.Occupancy,
                candidateReservations,
                preferSupportSlots,
                avoidOpeningNewAxisGapNearEngagedTarget: false,
                worldInputs.PerformanceCounters,
                scopedLocalCombatRegion);
            request = moveOptions.Count == 0
                ? null
                : BattleRuntimeAiActionRequest.AdvanceTowardTarget(
                    actorFact.Actor.ActorId,
                    alternateTarget.Actor.ActorId);
        }

        if (request == null || moveOptions.Count == 0)
        {
            return false;
        }

        // This remains zone-scoped: a retained target that cannot accept ingress
        // yields to another executable candidate in the same action-zone boundary.
        context = BattleRuntimeTickContextFactory.Create(
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

    private static IReadOnlyList<BattleGridCoord> BuildFreshTargetMoveOptions(
        BattleTargetMovementProposalBuildRequest request,
        BattleMovementProposalWorldInputs worldInputs,
        BattleRuntimeActor tickStartActor,
        BattleRuntimeActor tickStartTarget,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        out BattleCombatSlotIntent? combatSlotIntent)
    {
        combatSlotIntent = null;
        if (request.LocalCombatSituation != null &&
            BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
                tickStartActor,
                tickStartTarget,
                request.ActorFact.Anchor,
                worldInputs.NavigationGraph,
                worldInputs.Occupancy,
                request.CandidateReservations,
                preferSupportSlots,
                worldInputs.PerformanceCounters,
                request.ScopedLocalCombatRegion,
                out BattleCombatSlotIntent selectedSlotIntent,
                out IReadOnlyList<BattleGridCoord> slotMoveOptions))
        {
            combatSlotIntent = selectedSlotIntent;
            return slotMoveOptions;
        }

        return request.LocalCombatSituation == null
            ? BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget(
                tickStartActor,
                tickStartTarget,
                worldInputs.NavigationGraph,
                worldInputs.Occupancy,
                request.CandidateReservations,
                preferSupportSlots,
                avoidOpeningNewAxisGapNearEngagedTarget,
                worldInputs.PerformanceCounters,
                request.ScopedLocalCombatRegion)
            : System.Array.Empty<BattleGridCoord>();
    }

    private void ValidateTargetMovementProposalRequest(BattleTargetMovementProposalBuildRequest request)
    {
        if (_actor == null || string.IsNullOrWhiteSpace(_actor.ActorId))
        {
            throw new System.InvalidOperationException("target movement proposal actor missing");
        }

        if (request.Request == null ||
            request.ActorFact.Actor == null ||
            request.TargetFact.Actor == null ||
            request.CandidateReservations == null)
        {
            throw new System.InvalidOperationException($"target movement proposal request missing context: actor={_actor.ActorId}");
        }

        if (!ReferenceEquals(_actor, request.ActorFact.Actor))
        {
            throw new System.InvalidOperationException($"target movement proposal actor mismatch: actor={_actor.ActorId} contextActor={request.ActorFact.Actor.ActorId ?? ""}");
        }

        if (!string.Equals(request.Request.ActorId ?? "", _actor.ActorId, System.StringComparison.Ordinal))
        {
            throw new System.InvalidOperationException($"target movement proposal request actor mismatch: actor={_actor.ActorId} requestActor={request.Request.ActorId ?? ""}");
        }

        if (!string.IsNullOrWhiteSpace(request.Request.TargetActorId) &&
            !string.Equals(request.Request.TargetActorId, request.TargetFact.Actor.ActorId ?? "", System.StringComparison.Ordinal))
        {
            throw new System.InvalidOperationException($"target movement proposal request target mismatch: actor={_actor.ActorId} requestTarget={request.Request.TargetActorId ?? ""} targetFact={request.TargetFact.Actor.ActorId ?? ""}");
        }

        if (request.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget &&
            request.Request.Kind != BattleRuntimeAiActionKind.JoinLocalCombat &&
            request.Request.Kind != BattleRuntimeAiActionKind.HoldSupport)
        {
            throw new System.InvalidOperationException($"unsupported target movement proposal kind for actor movement controller: actor={_actor.ActorId} kind={request.Request.Kind}");
        }
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
                !completed.Contains(actor.ActorId ?? ""))
            {
                continue;
            }

            new BattleMovementController(actor).ClearEndedMovementChain();
        }
    }

    internal void ClearEndedMovementChain()
    {
        BattleRuntimeActor actor = _actor;
        if (actor == null ||
            actor.Phase == BattleRuntimeActorPhase.Moving && actor.HasMovementTarget)
        {
            return;
        }

        bool preserveExhaustedSteering =
            actor.MovementSteeringMode == BattleLocalSteeringMode.FollowObstacle &&
            actor.MovementSteeringBudgetRemaining <= 0;
        BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(actor, clearSteering: !preserveExhaustedSteering);
    }
}

internal readonly record struct BattleMovementProposalBuildRequest(
    BattleRuntimeAiActionRequest Request,
    BattleRuntimeTickStartActorFact ActorFact,
    BattleRuntimeTickStartActorFact? TargetFact,
    LocalCombatSituation LocalCombatSituation,
    BattleRegionMovementGoal RegionMovementGoal);

internal readonly record struct BattleMovementProposalWorldInputs(
    BattleNavigationGraph NavigationGraph,
    BattleDynamicOccupancy Occupancy,
    BattlePerformanceCounters PerformanceCounters,
    string BattleId,
    int Tick);

internal readonly record struct BattleTargetMovementProposalBuildRequest(
    BattleRuntimeAiActionRequest Request,
    BattleRuntimeTickStartActorFact ActorFact,
    BattleRuntimeTickStartActorFact TargetFact,
    // H2 keeps full tick-start facts outside the actor controller; callers pass
    // only the already-resolved target engagement context needed for proposal policy.
    bool TargetEngagedBySameFactionActor,
    LocalCombatSituation LocalCombatSituation,
    BattleTacticalRegionSnapshot ScopedLocalCombatRegion,
    BattleMovementReservationMap CandidateReservations,
    bool UseStoredCombatSlotIntent,
    string MovementReasonCode);

internal readonly record struct BattleTargetMovementProposalResult(
    BattleRuntimeTickContext Context,
    string FailureReason);
