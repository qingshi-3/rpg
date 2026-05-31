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
    private const int PlannedLocalPerceptionRange = BattlePerceptionPolicy.DefaultLocalPerceptionRange;
    private const string CommandAssault = "Assault";
    private const string CommandFocusFire = "FocusFire";
    private const string CommandHoldLine = "HoldLine";
    private readonly IBattleRuntimeAiExecutor _aiExecutor;

    internal BattleRuntimeTickResolver(IBattleRuntimeAiExecutor aiExecutor)
    {
        _aiExecutor = aiExecutor ?? new DefaultBattleRuntimeAiExecutor();
    }

    private readonly record struct PendingAttack(BattleRuntimeTickContext Context, int DeclaredDamage);
    private readonly record struct AttackApplication(BattleRuntimeTickContext Context, int AppliedDamage, bool IsFinishingHit);

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
        BattleRuntimeActor[] preBoundaryLivingCorps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, System.StringComparer.Ordinal)
            .ToArray();
        BattleDynamicOccupancy occupancy = BattleDynamicOccupancy.FromActors(preBoundaryLivingCorps);
        HashSet<string> movementCompletedActorIds = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeActor actor in state.Actors.Where(item => item.Kind == BattleRuntimeActorKind.Corps))
        {
            if (!BattleRuntimeActorStateMachine.AdvanceTimeBoundary(
                    actor,
                    currentTimeSeconds,
                    out BattleGridCoord movementFrom,
                    out BattleGridCoord movementTo))
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
                    "movement_committed"));
            movementCompletedActorIds.Add(actor.ActorId ?? "");
        }

        BattleRuntimeActor[] livingCorps = CaptureLivingCorpsAndRefreshPerceptionSummaries(state, stream, battleId, tick, currentTimeSeconds);
        if (livingCorps.Length == 0)
        {
            return;
        }

        Dictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts = livingCorps.ToDictionary(
            item => item.ActorId,
            item => new BattleRuntimeTickStartActorFact(
                item,
                new BattleGridCoord(item.GridX, item.GridY, item.GridHeight),
                item.HitPoints,
                item.AttackCharge,
                item.TargetActorId ?? "",
                item.CommandId ?? ""),
            System.StringComparer.Ordinal);

        BattleFlowFieldCache flowFields = new(performanceCounters);
        BattleRuntimeActor[] decisionReadyCorps = livingCorps
            .Where(item => item.Phase == BattleRuntimeActorPhase.AnchoredDecision &&
                           !movementCompletedActorIds.Contains(item.ActorId ?? ""))
            .ToArray();
        performanceCounters?.RecordDecisionReadyActors(decisionReadyCorps.Length);
        if (decisionReadyCorps.Length == 0)
        {
            return;
        }

        List<BattleRuntimeTickContext> contexts = decisionReadyCorps
            .Select(item => BuildTickContext(
                item,
                tickStartFacts,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                currentTimeSeconds,
                tick,
                navigationFailureDiagnostics,
                state.TacticalStateStore))
            .ToList();
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
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
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
            TryRetargetStaleAdvanceContext);
        performanceCounters?.RecordMovementResolveElapsedTicks(Stopwatch.GetTimestamp() - movementResolveStartedAt);
        performanceCounters?.RecordActorsReadyNoMoveLastAdvance(decisionReadyCorps.Length - movementEvents);

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
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        double currentTimeSeconds,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        BattleRuntimeTickStartActorFact actorFact = facts[actor.ActorId];
        BattleRegionMovementGoal regionMovementGoal = ResolveRegionMovementGoal(actorFact, tacticalStateStore);
        BattleTacticalRegionSnapshot localCombatRegion = ResolveEngagedLocalCombatRegion(actorFact, tacticalStateStore);
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> targetFacts = localCombatRegion == null
            ? facts
            : FilterFactsToLocalCombatRegion(facts, actorFact, localCombatRegion);
        BattleRuntimeTickStartActorFact? preferredTarget = regionMovementGoal == null
            ? FindEnemyCorpsForCommand(
                targetFacts,
                actorFact,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters)
            : FindRegionScopedEnemyCorps(targetFacts, actorFact);
        LocalCombatSituation localCombatSituation = preferredTarget == null
            ? null
            : LocalCombatSituationBuilder.Build(
                actorFact.Actor,
                preferredTarget.Value.Actor,
                facts.Values.Select(item => item.Actor).ToArray(),
                navigationGraph,
                occupancy,
                currentTimeSeconds,
                localCombatRegion);
        BattleRuntimeAiActionRequest request = BuildCommandScopedAiActionRequest(actorFact, preferredTarget, localCombatSituation, regionMovementGoal);
        BattleRuntimeTickStartActorFact? requestedTarget = ResolveRequestedTarget(facts, actorFact, preferredTarget, request);

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
            return CreateContext(request, actorFact, requestedTarget, false, default, request.FailureReason, localCombatSituation: localCombatSituation, regionMovementGoal: regionMovementGoal);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return BuildRegionAdvanceContext(
                request,
                actorFact,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                tick);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective)
        {
            return BuildObjectiveAdvanceContext(
                request,
                actorFact,
                navigationGraph,
                occupancy,
                flowFields,
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

            BattleRuntimeActor tickStartActor = BuildTickStartProjection(actorFact);
            BattleRuntimeActor tickStartTarget = BuildTickStartProjection(requestedTarget.Value);
            bool preferSupport = request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                                 IsTargetEngagedBySameFactionActor(facts, actorFact, requestedTarget.Value);
            // Default assault is attack-opportunity first. Support slots are only
            // a fallback when no attack slot is reachable from the current facts.
            bool preferSupportSlots = request.Kind == BattleRuntimeAiActionKind.HoldSupport ||
                                      preferSupport &&
                                      movementGap > attackRange + 1 &&
                                      !HasReachableAttackSlot(
                                          tickStartActor,
                                          tickStartTarget,
                                      actorFact.Anchor,
                                      navigationGraph,
                                      occupancy,
                                      flowFields,
                                      performanceCounters,
                                      localCombatRegion);
            bool avoidOpeningNewAxisGapNearEngagedTarget = preferSupport && movementGap == attackRange + 1;
            IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget(
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
            if (moveOptions.Count == 0)
            {
                LogAdvanceFailureDiagnostic(
                    battleId,
                    tick,
                    actorFact,
                    requestedTarget.Value,
                    navigationGraph,
                    "path_not_found",
                    default,
                    navigationFailureDiagnostics);
                return CreateContext(
                    request,
                    actorFact,
                    requestedTarget,
                    hasMoveTo: false,
                    moveTo: default,
                    failureReason: request.Kind == BattleRuntimeAiActionKind.HoldSupport
                        ? LocalCombatDecisionReason.RejectNoReachableSlot
                        : "path_not_found",
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
                movementReasonCode: request.ReasonCode);
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

    private static BattleRuntimeTickContext CreateContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        bool hasMoveTo,
        BattleGridCoord moveTo,
        string failureReason,
        IReadOnlyList<BattleGridCoord> moveOptions = null,
        LocalCombatSituation localCombatSituation = null,
        string movementReasonCode = "",
        BattleRegionMovementGoal regionMovementGoal = null)
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
                FailureReason = failureReason ?? "",
                MovementReasonCode = movementReasonCode ?? "",
                LocalCombatSituationId = localCombatSituation?.SituationId ?? ""
            }
        };
    }

    private static BattleRuntimeActor BuildTickStartProjection(BattleRuntimeTickStartActorFact fact)
    {
        return new BattleRuntimeActor
        {
            ActorId = fact.Actor.ActorId,
            FactionId = fact.Actor.FactionId,
            Kind = fact.Actor.Kind,
            FootprintWidth = fact.Actor.FootprintWidth,
            FootprintHeight = fact.Actor.FootprintHeight,
            GridX = fact.Anchor.X,
            GridY = fact.Anchor.Y,
            GridHeight = fact.Anchor.Height,
            AttackRange = fact.Actor.AttackRange,
            AttackDamage = fact.Actor.AttackDamage,
            HitPoints = fact.HitPoints,
            EngagementRule = fact.Actor.EngagementRule,
            PlanState = fact.Actor.PlanState,
            HasObjectiveAnchor = fact.Actor.HasObjectiveAnchor,
            ObjectiveZoneId = fact.Actor.ObjectiveZoneId,
            ObjectiveGridX = fact.Actor.ObjectiveGridX,
            ObjectiveGridY = fact.Actor.ObjectiveGridY,
            ObjectiveGridHeight = fact.Actor.ObjectiveGridHeight,
            ObjectiveWidth = fact.Actor.ObjectiveWidth,
            ObjectiveHeight = fact.Actor.ObjectiveHeight
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

    private void ResolveAttackProposals(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        List<PendingAttack> pendingAttacks = new();
        foreach (BattleRuntimeTickContext context in contexts
                     .Where(item =>
                         item.Request.Kind == BattleRuntimeAiActionKind.AttackTarget &&
                         item.Result == null))
        {
            if (context.TargetFact == null)
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (GetOrthogonalAttackGap(
                    context.ActorFact.Actor,
                    context.ActorFact.Anchor,
                    context.TargetFact.Value.Actor,
                    context.TargetFact.Value.Anchor) > System.Math.Max(1, context.ActorFact.Actor.AttackRange))
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_out_of_range");
                continue;
            }

            if (context.ActorFact.AttackCharge < 1.0)
            {
                BattleRuntimeActorStateMachine.MarkWaitingForCharge(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "attack_charge_empty");
                continue;
            }

            pendingAttacks.Add(new PendingAttack(
                context,
                ResolveAttackDamage(context.ActorFact.Actor.AttackDamage)));
        }

        Dictionary<string, int> postAttackHitPoints = tickStartFacts.Values.ToDictionary(
            item => item.Actor.ActorId,
            item => System.Math.Max(0, item.HitPoints),
            System.StringComparer.Ordinal);

        List<AttackApplication> applications = new();
        foreach (IGrouping<string, PendingAttack> targetGroup in pendingAttacks
                     .GroupBy(item => item.Context.TargetFact!.Value.Actor.ActorId, System.StringComparer.Ordinal))
        {
            int remaining = postAttackHitPoints[targetGroup.Key];
            foreach (PendingAttack pending in targetGroup.OrderBy(
                         item => item.Context.ActorFact.Actor.ActorId,
                         System.StringComparer.Ordinal))
            {
                int applied = System.Math.Min(pending.DeclaredDamage, remaining);
                remaining = System.Math.Max(0, remaining - applied);
                applications.Add(new AttackApplication(
                    pending.Context,
                    applied,
                    IsFinishingHit: applied > 0 && remaining == 0));
            }

            postAttackHitPoints[targetGroup.Key] = remaining;
        }

        foreach (AttackApplication application in applications.OrderBy(
                     item => item.Context.ActorFact.Actor.ActorId,
                     System.StringComparer.Ordinal))
        {
            // The factory preserves RuntimeTimeSeconds = currentTimeSeconds; the resolver owns this stream position.
            stream.Add(BattleRuntimeEventFactory.CreateDamageApplied(
                battleId,
                tick,
                currentTimeSeconds,
                application.Context.ActorFact.Actor,
                application.Context.TargetFact!.Value.Actor,
                application.Context.ActorFact.Anchor,
                application.Context.TargetFact!.Value.Anchor,
                application.AppliedDamage,
                application.IsFinishingHit));
        }

        foreach (PendingAttack pending in pendingAttacks)
        {
            pending.Context.ActorFact.Actor.AttackCharge = System.Math.Max(0, pending.Context.ActorFact.Actor.AttackCharge - 1.0);
            ResetAdvanceFailureState(pending.Context.ActorFact.Actor);
            BattlePlanStateEmitter.SetPlanState(
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                pending.Context.ActorFact.Actor,
                BattleGroupPlanRuntimeState.Attacking,
                "attacking");
            BattleRuntimeActorStateMachine.MarkAttackRecovery(pending.Context.ActorFact.Actor, currentTimeSeconds);
            pending.Context.Result = BattleRuntimeAiActionResult.Succeeded(pending.Context.Request, "attacked");
        }

        foreach (KeyValuePair<string, int> pair in postAttackHitPoints)
        {
            if (tickStartFacts.TryGetValue(pair.Key, out BattleRuntimeTickStartActorFact targetFact))
            {
                targetFact.Actor.HitPoints = System.Math.Max(0, pair.Value);
                if (targetFact.Actor.HitPoints <= 0)
                {
                    BattlePlanStateEmitter.SetPlanState(
                        stream,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        targetFact.Actor,
                        BattleGroupPlanRuntimeState.Defeated,
                        "defeated");
                    BattleRuntimeActorStateMachine.MarkDefeated(targetFact.Actor);
                }
            }
        }
    }

    private static int GetOrthogonalAttackGap(BattleRuntimeTickStartActorFact first, BattleRuntimeTickStartActorFact second)
    {
        return GetOrthogonalAttackGap(first.Actor, first.Anchor, second.Actor, second.Anchor);
    }

    private static int GetOrthogonalAttackGap(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor)
    {
        return BattleActorFootprint.GetOrthogonalGap(first, firstAnchor, second, secondAnchor);
    }

    private static int GetSquareGridDistance(BattleRuntimeTickStartActorFact first, BattleRuntimeTickStartActorFact second)
    {
        return BattleActorFootprint.GetGap(first.Actor, first.Anchor, second.Actor, second.Anchor);
    }

    private static int ResolveAttackDamage(int attackDamage)
    {
        return attackDamage > 0 ? attackDamage : 1;
    }

    private static bool IsFocusFireCommand(string commandId)
    {
        return string.Equals(NormalizeCorpsCommandId(commandId), CommandFocusFire, System.StringComparison.Ordinal);
    }

    private static bool IsHoldLineCommand(string commandId)
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

    private static bool SameFaction(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return string.Equals(
            NormalizeFaction(first?.FactionId),
            NormalizeFaction(second?.FactionId),
            System.StringComparison.Ordinal);
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }
}
