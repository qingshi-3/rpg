using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private const double MaxAttackCharge = 1.0;
    private const string CommandAssault = "Assault";
    private const string CommandFocusFire = "FocusFire";
    private const string CommandHoldLine = "HoldLine";
    private readonly IBattleRuntimeAiExecutor _aiExecutor;

    internal BattleRuntimeTickResolver(IBattleRuntimeAiExecutor aiExecutor)
    {
        _aiExecutor = aiExecutor ?? new DefaultBattleRuntimeAiExecutor();
    }

    internal void ResolveTick(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters = null)
    {
        if (state?.Actors == null || stream == null || navigationGraph == null)
        {
            return;
        }

        performanceCounters?.BeginRuntimeAdvance();
        performanceCounters?.RecordRuntimeTick();
        BattleDynamicOccupancy occupancy = AdvanceMovementBoundaries(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            out HashSet<string> movementCompletedActorIds);

        // Player skill commands submitted while tactical pause is active are queued
        // here so pause-time input never mutates combat state until Runtime advances.
        HashSet<string> skillWaitingAfterMovementActorIds = new(System.StringComparer.Ordinal);
        HashSet<string> skillActionActorIds = BattleRuntimeHeroSkillCommandResolver.ResolvePending(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            movementCompletedActorIds,
            skillWaitingAfterMovementActorIds);
        HashSet<string> skillConsumedActorIds = new(skillActionActorIds, System.StringComparer.Ordinal);
        skillConsumedActorIds.UnionWith(skillWaitingAfterMovementActorIds);

        BattleRuntimeActor[] livingCorps = BattleTacticalObservationUpdater.RefreshAtTickStart(state, stream, battleId, tick, currentTimeSeconds);
        if (livingCorps.Length == 0)
        {
            return;
        }

        Dictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts = BattleTickStartProjectionBuilder.BuildFactMap(livingCorps);

        BattleRuntimeActor[] decisionReadyCorps = livingCorps
            .Where(item => item.Phase == BattleRuntimeActorPhase.AnchoredDecision &&
                           !movementCompletedActorIds.Contains(item.ActorId ?? "") &&
                           !skillConsumedActorIds.Contains(item.ActorId ?? ""))
            .ToArray();
        performanceCounters?.RecordDecisionReadyActors(decisionReadyCorps.Length);

        List<BattleRuntimeTickContext> contexts = decisionReadyCorps
            .Select(item => BuildTickContext(
                item,
                tickStartFacts,
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                currentTimeSeconds,
                tick,
                navigationFailureDiagnostics,
                state.TacticalStateStore,
                state.GroupActionZones,
                state.CombatZones))
            .ToList();
        ApplyDecisionOutcomes(contexts, stream, battleId, tick, currentTimeSeconds);
        HashSet<string> continuationEligibleMovementCompletedActorIds = movementCompletedActorIds
            .Where(actorId => !skillConsumedActorIds.Contains(actorId ?? ""))
            .ToHashSet(System.StringComparer.Ordinal);
        contexts.AddRange(BattleMovementContinuationPlanner.BuildContinuationContexts(
            continuationEligibleMovementCompletedActorIds,
            tickStartFacts,
            navigationGraph,
            occupancy,
            performanceCounters,
            battleId,
            currentTimeSeconds,
            tick,
            navigationFailureDiagnostics,
            state.TacticalStateStore,
            state.GroupActionZones,
            state.CombatZones));

        // Attack resolution still emits damage with RuntimeTimeSeconds = currentTimeSeconds;
        // the resolver owns the engagement stream slice immediately around that service call.
        ResolveAttackProposalsAndEngagementTriggers(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds, state);
        long movementResolveStartedAt = Stopwatch.GetTimestamp();
        int movementEvents = BattleMovementCommitResolver.Resolve(
            contexts,
            tickStartFacts,
            occupancy,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            navigationFailureDiagnostics,
            performanceCounters,
            state.TacticalStateStore,
            state.GroupActionZones,
            state.CombatZones,
            TryRetargetStaleAdvanceContext);
        performanceCounters?.RecordMovementResolveElapsedTicks(Stopwatch.GetTimestamp() - movementResolveStartedAt);
        performanceCounters?.RecordActorsReadyNoMoveLastAdvance(decisionReadyCorps.Length - movementEvents);
        BattleMovementContinuationPlanner.ClearEndedMovementChains(state.Actors, movementCompletedActorIds);

        LogTickActionResults(contexts, battleId, tick, currentTimeSeconds);
    }

    private BattleDynamicOccupancy AdvanceMovementBoundaries(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        out HashSet<string> movementCompletedActorIds)
    {
        BattleRuntimeActor[] preBoundaryLivingCorps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, System.StringComparer.Ordinal)
            .ToArray();
        BattleDynamicOccupancy occupancy = BattleDynamicOccupancy.FromActors(preBoundaryLivingCorps);
        movementCompletedActorIds = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeActor actor in state.Actors.Where(item => item.Kind == BattleRuntimeActorKind.Corps))
        {
            if (!BattleRuntimeActorStateMachine.AdvanceTimeBoundary(
                    actor,
                    currentTimeSeconds,
                    out BattleGridCoord movementFrom,
                    out BattleGridCoord movementTo,
                    out string movementBoundaryReasonCode))
            {
                continue;
            }

            stream.Add(BattleRuntimeEventFactory.CreateMovementEvent(
                BattleEventKind.MovementCompleted,
                battleId,
                tick,
                currentTimeSeconds,
                actor,
                actor.TargetActorId ?? "",
                movementFrom,
                    movementTo,
                    movementBoundaryReasonCode));
            movementCompletedActorIds.Add(actor.ActorId ?? "");
        }

        return occupancy;
    }

    private void ApplyDecisionOutcomes(
        List<BattleRuntimeTickContext> contexts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        foreach (BattleRuntimeTickContext context in contexts)
        {
            if (context.Request.Kind == BattleRuntimeAiActionKind.Hold)
            {
                context.ActorFact.Actor.TargetActorId = "";
                if (string.Equals(context.Request.FailureReason, LocalCombatDecisionReason.RejectOutsideLeash, System.StringComparison.Ordinal) ||
                    string.Equals(context.Request.FailureReason, BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, System.StringComparison.Ordinal))
                {
                    RecordAdvanceFailure(context.ActorFact.Actor, context.Request.FailureReason);
                }
                else
                {
                    ResetAdvanceFailureState(context.ActorFact.Actor);
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
                ResetAdvanceFailureState(context.ActorFact.Actor);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (context.TargetFact != null)
            {
                bool targetChanged = !string.Equals(
                    context.ActorFact.Actor.TargetActorId,
                    context.TargetFact.Value.Actor.ActorId,
                    System.StringComparison.Ordinal);
                context.ActorFact.Actor.TargetActorId = context.TargetFact.Value.Actor.ActorId;
                if (targetChanged)
                {
                    ResetAdvanceFailureState(context.ActorFact.Actor);
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
                    RecordAdvanceFailure(context.ActorFact.Actor, context.Proposal.FailureReason);
                }

                continue;
            }

            if (context.Request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge)
            {
                ResetAdvanceFailureState(context.ActorFact.Actor);
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

    private void LogTickActionResults(
        List<BattleRuntimeTickContext> contexts,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        foreach (BattleRuntimeTickContext context in contexts.OrderBy(item => item.ActorFact.Actor.ActorId, System.StringComparer.Ordinal))
        {
            if (context.Result == null)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "unresolved_action");
            }

            LogRuntimeActionResult(
                battleId,
                tick,
                currentTimeSeconds,
                context.ActorFact.Actor,
                context.TargetFact?.Actor,
                context.Request,
                context.Result);
        }
    }

    private BattleRuntimeTickContext BuildTickContext(
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
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones)
    {
        if (actor == null ||
            string.IsNullOrWhiteSpace(actor.ActorId) ||
            facts == null ||
            !facts.TryGetValue(actor.ActorId, out BattleRuntimeTickStartActorFact actorFact))
        {
            throw new System.InvalidOperationException($"missing runtime tick-start fact: actorId={actor?.ActorId ?? ""}");
        }

        BattleRegionMovementGoal regionMovementGoal = BattleLocalCombatRegionResolver.ResolveRegionMovementGoal(actorFact, tacticalStateStore);
        BattleGroupActionZoneSnapshot combatJoinActionZone = BattleGroupActionZoneResolver.ResolveActorCombatJoinActionZone(
            actorFact,
            groupActionZones,
            combatZones);
        BattleTacticalRegionSnapshot localCombatRegion = BattleLocalCombatRegionResolver.ResolveEngagedLocalCombatRegion(actorFact, tacticalStateStore);
        BattleTacticalRegionSnapshot combatJoinRegion = BattleGroupActionZoneBuilder.ToLocalCombatRegion(combatJoinActionZone);
        if (BattleCombatJoinRegionPlanner.TryBuildOutsiderAdvanceContext(
                actorFact,
                combatJoinActionZone,
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick,
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
        BattleTargetSelectionService.BattleTargetCandidateSet targetCandidateSet = combatJoinActionZone != null
            ? BattleTargetSelectionService.BuildCombatZoneScopedTargetCandidates(
                targetFacts, actorFact, navigationGraph, occupancy, performanceCounters, combatJoinRegion)
            : regionMovementGoal == null
            ? BattleTargetSelectionService.BuildTargetCandidatesForCommand(
                targetFacts,
                actorFact,
                navigationGraph,
                occupancy,
                performanceCounters)
            : BattleTargetSelectionService.BuildRegionScopedTargetCandidates(targetFacts, actorFact);
        // Target choice is part of the behavior-tree decision boundary. The
        // resolver only builds scoped candidate facts, then uses the selected id
        // to build target-specific local combat facts for the final action pass.
        BattleRuntimeAiDecisionFacts targetSelectionFacts = BattleAiActionRequestBuilder.BuildDecisionFacts(
            actorFact,
            null,
            null,
            targetCandidateSet.Candidates,
            targetCandidateSet.SelectionPolicy);
        BattleRuntimeAiActionRequest targetSelectionRequest = _aiExecutor.ChooseAction(targetSelectionFacts);
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
            _aiExecutor,
            RecordAdvanceFailure,
            targetCandidateSet.Candidates,
            targetCandidateSet.SelectionPolicy);
        BattleRuntimeTickStartActorFact? requestedTarget = ResolveRequestedTarget(facts, actorFact, selectedTarget, request);

        if (!string.Equals(request.ActorId, actor.ActorId, System.StringComparison.Ordinal))
        {
            return CreateContext(
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
                IsBlockedLocalCombatHold(request.FailureReason) &&
                string.Equals(requestedTarget.Value.Actor.ActorId ?? "", actorFact.TargetActorId ?? "", System.StringComparison.Ordinal) &&
                TryBuildAlternateCombatZoneJoinContext(
                    actorFact,
                    requestedTarget.Value,
                    targetFacts,
                    targetCandidateSet.Candidates,
                    localCombatRegion,
                    combatJoinRegion,
                    navigationGraph,
                    occupancy,
                    performanceCounters,
                    currentTimeSeconds,
                    out BattleRuntimeTickContext alternateCombatJoinContext))
            {
                return alternateCombatJoinContext;
            }

            if (BattleCombatJoinRegionPlanner.TryBuildPressureAdvanceContext(actorFact, requestedTarget, localCombatSituation, navigationGraph, occupancy, performanceCounters, out BattleRuntimeTickContext pressureHoldContext))
            {
                return pressureHoldContext;
            }

            return CreateContext(request, actorFact, requestedTarget, false, default, request.FailureReason, localCombatSituation: localCombatSituation, regionMovementGoal: regionMovementGoal);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext(
                request,
                actorFact,
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective)
        {
            return BattleObjectiveAdvancePlanner.BuildObjectiveAdvanceContext(
                request,
                actorFact,
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                tick);
        }

        if (requestedTarget == null)
        {
            return CreateContext(
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
            int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
            int movementGap = BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, requestedTarget.Value.Actor, requestedTarget.Value.Anchor);
            int attackGap = GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, requestedTarget.Value.Actor, requestedTarget.Value.Anchor);
            if (attackGap <= attackRange && request.Kind != BattleRuntimeAiActionKind.HoldSupport)
            {
                return CreateContext(
                    request,
                    actorFact,
                    requestedTarget,
                    hasMoveTo: false,
                    moveTo: default,
                    failureReason: "target_already_in_range",
                    localCombatSituation: localCombatSituation);
            }

            BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(actorFact);
            BattleRuntimeActor tickStartTarget = BattleTickStartProjectionBuilder.Build(requestedTarget.Value);
            bool preferSupport = request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                                 BattleTargetSelectionService.IsTargetEngagedBySameFactionActor(facts, actorFact, requestedTarget.Value);
            // Default assault is attack-opportunity first. Support slots are only
            // a fallback when no attack slot is reachable from the current facts.
            bool preferSupportSlots = request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                                      preferSupport &&
                                      movementGap > attackRange + 1 &&
                                      !BattleTargetSelectionService.HasReachableAttackSlot(
                                          tickStartActor,
                                          tickStartTarget,
                                      actorFact.Anchor,
                                      navigationGraph,
                                      occupancy,
                                      performanceCounters,
                                      scopedLocalCombatRegion);
            bool avoidOpeningNewAxisGapNearEngagedTarget = preferSupport && movementGap == attackRange + 1;
            BattleCombatSlotIntent? combatSlotIntent = null;
            IReadOnlyList<BattleGridCoord> moveOptions = System.Array.Empty<BattleGridCoord>();
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
                    scopedLocalCombatRegion,
                    out BattleCombatSlotIntent selectedSlotIntent,
                    out IReadOnlyList<BattleGridCoord> slotMoveOptions))
            {
                combatSlotIntent = selectedSlotIntent;
                moveOptions = slotMoveOptions;
            }

            if (moveOptions.Count == 0 && localCombatSituation == null)
            {
                moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget(
                    tickStartActor,
                    tickStartTarget,
                    navigationGraph,
                    occupancy,
                    new BattleMovementReservationMap(),
                    preferSupportSlots,
                    avoidOpeningNewAxisGapNearEngagedTarget,
                    performanceCounters,
                    scopedLocalCombatRegion);
            }
            if (moveOptions.Count == 0)
            {
                if (combatJoinActionZone != null &&
                    localCombatSituation != null &&
                    string.Equals(requestedTarget.Value.Actor.ActorId ?? "", actorFact.TargetActorId ?? "", System.StringComparison.Ordinal) &&
                    TryBuildAlternateCombatZoneJoinContext(
                        actorFact,
                        requestedTarget.Value,
                        targetFacts,
                        targetCandidateSet.Candidates,
                        localCombatRegion,
                        combatJoinRegion,
                        navigationGraph,
                        occupancy,
                        performanceCounters,
                        currentTimeSeconds,
                        out BattleRuntimeTickContext alternateCombatJoinContext))
                {
                    return alternateCombatJoinContext;
                }

                if (BattleCombatJoinRegionPlanner.TryBuildPressureAdvanceContext(actorFact, requestedTarget, localCombatSituation, navigationGraph, occupancy, performanceCounters, out BattleRuntimeTickContext pressureAdvanceContext))
                {
                    return pressureAdvanceContext;
                }

                // Runtime validation names blocked local-combat ingress, but
                // target/support selection stays with commander/local-combat request construction.
                string failureReason = localCombatSituation != null
                    ? localCombatSituation.HasReachableSupportSlot
                        ? LocalCombatDecisionReason.HoldSupportAttackSlotsFull
                        : LocalCombatDecisionReason.RejectNoReachableSlot
                    : "path_not_found";
                BattleRuntimeAdvanceDiagnostics.LogAdvanceFailureDiagnostic(
                    battleId,
                    tick,
                    actorFact,
                    requestedTarget.Value,
                    navigationGraph,
                    failureReason,
                    default,
                    navigationFailureDiagnostics);
                return CreateContext(
                    request,
                    actorFact,
                    requestedTarget,
                    hasMoveTo: false,
                    moveTo: default,
                    failureReason: failureReason,
                    localCombatSituation: localCombatSituation);
            }

            return CreateContext(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: true,
                moveTo: moveOptions[0],
                failureReason: "",
                moveOptions: moveOptions,
                localCombatSituation: localCombatSituation,
                movementReasonCode: request.ReasonCode,
                combatSlotIntent: combatSlotIntent);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AttackTarget ||
            request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge)
        {
            return CreateContext(request, actorFact, requestedTarget, false, default, "", localCombatSituation: localCombatSituation);
        }

        return CreateContext(
            request,
            actorFact,
            requestedTarget,
            hasMoveTo: false,
            moveTo: default,
            failureReason: "unsupported_action",
            localCombatSituation: localCombatSituation);
    }

    internal static BattleRuntimeTickContext CreateContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        bool hasMoveTo,
        BattleGridCoord moveTo,
        string failureReason,
        IReadOnlyList<BattleGridCoord> moveOptions = null,
        LocalCombatSituation localCombatSituation = null,
        string movementReasonCode = "",
        BattleRegionMovementGoal regionMovementGoal = null,
        BattleCombatSlotIntent? combatSlotIntent = null)
    {
        return new BattleRuntimeTickContext
        {
            Request = request,
            ActorFact = actorFact,
            TargetFact = targetFact,
            LocalCombatSituation = localCombatSituation,
            Proposal = new BattleRuntimeActionProposal
            {
                Request = request,
                Actor = actorFact.Actor,
                Target = targetFact?.Actor,
                ActorStart = actorFact.Anchor,
                TargetStart = targetFact?.Anchor ?? default,
                HasMoveTo = hasMoveTo,
                MoveTo = moveTo,
                MoveOptions = moveOptions ?? new List<BattleGridCoord>(),
                HasCombatSlotIntent = combatSlotIntent.HasValue,
                CombatSlotAnchor = combatSlotIntent?.Anchor ?? default,
                CombatSlotKind = combatSlotIntent?.Kind ?? BattleCombatSlotKind.Support,
                FailureReason = failureReason ?? "",
                MovementReasonCode = movementReasonCode ?? "",
                LocalCombatSituationId = localCombatSituation?.SituationId ?? ""
            }
        };
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
            SameFaction(actorFact.Actor, requestedTarget.Actor))
        {
            return fallbackTarget;
        }

        return requestedTarget;
    }

    internal static int GetOrthogonalAttackGap(BattleRuntimeTickStartActorFact first, BattleRuntimeTickStartActorFact second)
    {
        return GetOrthogonalAttackGap(first.Actor, first.Anchor, second.Actor, second.Anchor);
    }

    internal static int GetOrthogonalAttackGap(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor)
    {
        return BattleActorFootprint.GetOrthogonalGap(first, firstAnchor, second, secondAnchor);
    }

    internal static int ResolveAttackDamage(int attackDamage)
    {
        return attackDamage > 0 ? attackDamage : 1;
    }

    internal static bool IsFocusFireCommand(string commandId)
    {
        return string.Equals(NormalizeCorpsCommandId(commandId), CommandFocusFire, System.StringComparison.Ordinal);
    }

    internal static bool IsHoldLineCommand(string commandId)
    {
        return string.Equals(NormalizeCorpsCommandId(commandId), CommandHoldLine, System.StringComparison.Ordinal);
    }

    private static string NormalizeCorpsCommandId(string commandId)
    {
        string value = commandId?.Trim() ?? "";
        if (string.Equals(value, CommandFocusFire, System.StringComparison.OrdinalIgnoreCase))
        {
            return CommandFocusFire;
        }

        if (string.Equals(value, CommandHoldLine, System.StringComparison.OrdinalIgnoreCase))
        {
            return CommandHoldLine;
        }

        return CommandAssault;
    }

    internal static bool SameFaction(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return string.Equals(
            NormalizeFaction(first?.FactionId),
            NormalizeFaction(second?.FactionId),
            System.StringComparison.Ordinal);
    }

    internal static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }
}
