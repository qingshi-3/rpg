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

    private readonly record struct TickStartActorFact(
        BattleRuntimeActor Actor,
        BattleGridCoord Anchor,
        int HitPoints,
        double AttackCharge,
        string TargetActorId,
        string CommandId);

    private sealed class TickContext
    {
        public BattleRuntimeActionProposal Proposal { get; set; }
        public BattleRuntimeAiActionRequest Request { get; set; }
        public TickStartActorFact ActorFact { get; init; }
        public TickStartActorFact? TargetFact { get; set; }
        public BattleRuntimeAiActionResult Result { get; set; }
    }

    private sealed class MoveCandidate
    {
        public TickContext Context { get; init; }
        public BattleGridCoord From { get; init; }
        public BattleGridCoord To { get; init; }
        public IReadOnlyList<BattleGridCoord> OrderedMoves { get; init; } = new List<BattleGridCoord>();
    }

    private readonly record struct PendingAttack(TickContext Context, int DeclaredDamage);
    private readonly record struct AttackApplication(TickContext Context, int AppliedDamage, bool IsFinishingHit);

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

            stream.Add(BuildMovementEvent(
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

        BattleRuntimeActor[] livingCorps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, System.StringComparer.Ordinal)
            .ToArray();
        if (livingCorps.Length == 0)
        {
            return;
        }

        Dictionary<string, TickStartActorFact> tickStartFacts = livingCorps.ToDictionary(
            item => item.ActorId,
            item => new TickStartActorFact(
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

        List<TickContext> contexts = decisionReadyCorps
            .Select(item => BuildTickContext(
                item,
                tickStartFacts,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                battleId,
                tick,
                navigationFailureDiagnostics))
            .ToList();
        foreach (TickContext context in contexts)
        {
            if (context.Request.Kind == BattleRuntimeAiActionKind.Hold)
            {
                context.ActorFact.Actor.TargetActorId = "";
                ResetAdvanceFailureState(context.ActorFact.Actor);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Succeeded(context.Request, "held");
                continue;
            }

            if (context.TargetFact == null &&
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardObjective)
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
                    SetPlanState(
                        stream,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        context.ActorFact.Actor,
                        BattleGroupPlanRuntimeState.TargetLocked,
                        "target_locked");
                }
            }
            else if (context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective)
            {
                context.ActorFact.Actor.TargetActorId = "";
                SetPlanState(
                    stream,
                    battleId,
                    tick,
                    currentTimeSeconds,
                    context.ActorFact.Actor,
                    BattleGroupPlanRuntimeState.AdvancingToObjective,
                    "objective_advance");
            }

            if (!string.IsNullOrWhiteSpace(context.Proposal.FailureReason))
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, context.Proposal.FailureReason);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                if (context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
                    context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective)
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
                     context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardObjective)
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "unsupported_action");
            }
        }

        ResolveAttackProposals(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds);
        long movementResolveStartedAt = Stopwatch.GetTimestamp();
        int movementEvents = ResolveMovementProposals(
            contexts,
            tickStartFacts,
            occupancy,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            navigationFailureDiagnostics,
            performanceCounters);
        performanceCounters?.RecordMovementResolveElapsedTicks(Stopwatch.GetTimestamp() - movementResolveStartedAt);
        performanceCounters?.RecordActorsReadyNoMoveLastAdvance(decisionReadyCorps.Length - movementEvents);

        foreach (TickContext context in contexts.OrderBy(item => item.ActorFact.Actor.ActorId, System.StringComparer.Ordinal))
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

    private TickContext BuildTickContext(
        BattleRuntimeActor actor,
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick,
        HashSet<string> navigationFailureDiagnostics)
    {
        TickStartActorFact actorFact = facts[actor.ActorId];
        TickStartActorFact? preferredTarget = FindEnemyCorpsForCommand(
            facts,
            actorFact,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters);
        BattleRuntimeAiActionRequest request = BuildCommandScopedAiActionRequest(actorFact, preferredTarget);
        TickStartActorFact? requestedTarget = ResolveRequestedTarget(facts, actorFact, preferredTarget, request);

        if (!string.Equals(request.ActorId, actor.ActorId, System.StringComparison.Ordinal))
        {
            return CreateContext(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: false,
                moveTo: default,
                failureReason: "invalid_actor");
        }

        if (request.Kind == BattleRuntimeAiActionKind.Hold)
        {
            return CreateContext(request, actorFact, requestedTarget, false, default, request.FailureReason);
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
                failureReason: "invalid_target");
        }

        if (request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget)
        {
            int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
            int movementGap = BattleActorFootprint.GetGap(actorFact.Actor, actorFact.Anchor, requestedTarget.Value.Actor, requestedTarget.Value.Anchor);
            int attackGap = GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, requestedTarget.Value.Actor, requestedTarget.Value.Anchor);
            if (attackGap <= attackRange)
            {
                return CreateContext(
                    request,
                    actorFact,
                    requestedTarget,
                    hasMoveTo: false,
                    moveTo: default,
                    failureReason: "target_already_in_range");
            }

            BattleRuntimeActor tickStartActor = BuildTickStartProjection(actorFact);
            BattleRuntimeActor tickStartTarget = BuildTickStartProjection(requestedTarget.Value);
            bool preferSupport = IsTargetEngagedBySameFactionActor(facts, actorFact, requestedTarget.Value);
            // Default assault is attack-opportunity first. Support slots are only
            // a fallback when no attack slot is reachable from the current facts.
            bool preferSupportSlots = preferSupport &&
                                      movementGap > attackRange + 1 &&
                                      !HasReachableAttackSlot(
                                          tickStartActor,
                                          tickStartTarget,
                                          actorFact.Anchor,
                                          navigationGraph,
                                          occupancy,
                                          flowFields,
                                          performanceCounters);
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
                    performanceCounters);
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
                    failureReason: "path_not_found");
            }

            return CreateContext(
                request,
                actorFact,
                requestedTarget,
                hasMoveTo: true,
                moveTo: moveOptions[0],
                failureReason: "",
                moveOptions: moveOptions);
        }

        if (request.Kind == BattleRuntimeAiActionKind.AttackTarget ||
            request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge)
        {
            return CreateContext(request, actorFact, requestedTarget, false, default, "");
        }

        return CreateContext(
            request,
            actorFact,
            requestedTarget,
            hasMoveTo: false,
            moveTo: default,
            failureReason: "unsupported_action");
    }

    private static TickContext CreateContext(
        BattleRuntimeAiActionRequest request,
        TickStartActorFact actorFact,
        TickStartActorFact? targetFact,
        bool hasMoveTo,
        BattleGridCoord moveTo,
        string failureReason,
        IReadOnlyList<BattleGridCoord> moveOptions = null)
    {
        return new TickContext
        {
            Request = request,
            ActorFact = actorFact,
            TargetFact = targetFact,
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
                FailureReason = failureReason ?? ""
            }
        };
    }

    private static BattleRuntimeActor BuildTickStartProjection(TickStartActorFact fact)
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

    private static TickStartActorFact? ResolveRequestedTarget(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        TickStartActorFact? fallbackTarget,
        BattleRuntimeAiActionRequest request)
    {
        if (request == null ||
            request.Kind == BattleRuntimeAiActionKind.Hold ||
            request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
            request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge && fallbackTarget == null)
        {
            return request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                ? null
                : fallbackTarget;
        }

        if (string.IsNullOrWhiteSpace(request.TargetActorId))
        {
            return fallbackTarget;
        }

        if (!facts.TryGetValue(request.TargetActorId, out TickStartActorFact requestedTarget) ||
            requestedTarget.HitPoints <= 0 ||
            SameFaction(actorFact.Actor, requestedTarget.Actor))
        {
            return fallbackTarget;
        }

        return requestedTarget;
    }

    private void ResolveAttackProposals(
        List<TickContext> contexts,
        IReadOnlyDictionary<string, TickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        List<PendingAttack> pendingAttacks = new();
        foreach (TickContext context in contexts
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
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:tick_{tick}:{application.Context.ActorFact.Actor.ActorId}:attack:{application.Context.TargetFact!.Value.Actor.ActorId}",
                BattleId = battleId,
                BattleGroupId = application.Context.ActorFact.Actor.BattleGroupId,
                ActorId = application.Context.ActorFact.Actor.ActorId,
                TargetId = application.Context.TargetFact.Value.Actor.ActorId,
                Kind = BattleEventKind.DamageApplied,
                ReasonCode = application.IsFinishingHit
                    ? "auto_attack_target_defeated"
                    : "auto_attack",
                RuntimeTick = tick,
                RuntimeTimeSeconds = currentTimeSeconds,
                ActionDurationSeconds = application.Context.ActorFact.Actor.AttackActionSeconds,
                ActionImpactDelaySeconds = application.Context.ActorFact.Actor.AttackImpactDelaySeconds,
                CorpsStrengthDelta = -application.AppliedDamage,
                HasActorCells = true,
                ActorGridX = application.Context.ActorFact.Anchor.X,
                ActorGridY = application.Context.ActorFact.Anchor.Y,
                ActorGridHeight = application.Context.ActorFact.Anchor.Height,
                HasTargetCells = true,
                TargetGridX = application.Context.TargetFact.Value.Anchor.X,
                TargetGridY = application.Context.TargetFact.Value.Anchor.Y,
                TargetGridHeight = application.Context.TargetFact.Value.Anchor.Height
            });
        }

        foreach (PendingAttack pending in pendingAttacks)
        {
            pending.Context.ActorFact.Actor.AttackCharge = System.Math.Max(0, pending.Context.ActorFact.Actor.AttackCharge - 1.0);
            ResetAdvanceFailureState(pending.Context.ActorFact.Actor);
            SetPlanState(
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
            if (tickStartFacts.TryGetValue(pair.Key, out TickStartActorFact targetFact))
            {
                targetFact.Actor.HitPoints = System.Math.Max(0, pair.Value);
                if (targetFact.Actor.HitPoints <= 0)
                {
                    SetPlanState(
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

    private int ResolveMovementProposals(
        List<TickContext> contexts,
        IReadOnlyDictionary<string, TickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters)
    {
        int movementEvents = 0;
        List<MoveCandidate> moveCandidates = new();
        foreach (TickContext context in contexts
                     .Where(item =>
                         (item.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
                          item.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective) &&
                         item.Result == null))
        {
            bool isObjectiveAdvance = context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective;
            if (context.TargetFact == null && !isObjectiveAdvance)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (!context.Proposal.HasMoveTo)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "advance_failed");
                RecordAdvanceFailure(context.ActorFact.Actor, context.Proposal.FailureReason);
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                continue;
            }

            if (context.ActorFact.Actor.HitPoints <= 0)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "actor_defeated_before_move");
                continue;
            }

            if (!isObjectiveAdvance && context.TargetFact.Value.Actor.HitPoints <= 0)
            {
                if (!TryRetargetStaleAdvanceContext(
                        context,
                        tickStartFacts,
                        occupancy,
                        navigationGraph,
                        battleId,
                        tick,
                        navigationFailureDiagnostics,
                        performanceCounters))
                {
                    context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_defeated_before_move");
                    continue;
                }
            }

            IReadOnlyList<BattleGridCoord> orderedMoves = context.Proposal.MoveOptions?.Count > 0
                ? context.Proposal.MoveOptions
                : new[] { context.Proposal.MoveTo };

            moveCandidates.Add(new MoveCandidate
            {
                Context = context,
                From = context.ActorFact.Anchor,
                To = context.Proposal.MoveTo,
                OrderedMoves = orderedMoves
            });
        }

        BattleMovementReservationMap reservations = new();
        foreach (MoveCandidate candidate in moveCandidates
                     .OrderBy(item => BattleActorFootprint.GetGap(
                         item.Context.ActorFact.Actor,
                         item.Context.ActorFact.Anchor,
                         item.Context.TargetFact?.Actor ?? item.Context.ActorFact.Actor,
                         item.Context.TargetFact?.Anchor ?? GetObjectiveAnchor(item.Context.ActorFact.Actor)))
                     .ThenBy(item => item.From.Height)
                     .ThenBy(item => item.From.Y)
                     .ThenBy(item => item.From.X)
                     .ThenBy(item => item.Context.ActorFact.Actor.BattleGroupId, System.StringComparer.Ordinal))
        {
            bool reserved = false;
            BattleGridCoord selectedMove = candidate.To;
            foreach (BattleGridCoord move in candidate.OrderedMoves)
            {
                if (!reservations.TryReserveMove(candidate.Context.ActorFact.Actor, candidate.From, move, occupancy))
                {
                    performanceCounters?.RecordReservationRejected();
                    continue;
                }

                selectedMove = move;
                reserved = true;
                break;
            }

            if (!reserved)
            {
                candidate.Context.Result = BattleRuntimeAiActionResult.Failed(candidate.Context.Request, "reservation_rejected");
                RecordAdvanceFailure(candidate.Context.ActorFact.Actor, "reservation_rejected");
                performanceCounters?.RecordHoldDueReservation();
                BattleRuntimeActorStateMachine.MarkHolding(candidate.Context.ActorFact.Actor, currentTimeSeconds);
                LogAdvanceFailureDiagnostic(
                    battleId,
                    tick,
                    candidate.Context.ActorFact,
                    candidate.Context.TargetFact,
                    navigationGraph,
                    "reservation_rejected",
                    candidate.To,
                    navigationFailureDiagnostics);
                continue;
            }

            candidate.Context.ActorFact.Actor.HasReservedGridCell = true;
            candidate.Context.ActorFact.Actor.ReservedGridX = selectedMove.X;
            candidate.Context.ActorFact.Actor.ReservedGridY = selectedMove.Y;
            candidate.Context.ActorFact.Actor.ReservedGridHeight = selectedMove.Height;
            SetPlanState(
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                candidate.Context.ActorFact.Actor,
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                    ? BattleGroupPlanRuntimeState.AdvancingToObjective
                    : BattleGroupPlanRuntimeState.MovingToAttackSlot,
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                    ? "objective_advance"
                    : "moving_to_attack_slot");
            BattleRuntimeActorStateMachine.MarkMovementCommitted(candidate.Context.ActorFact.Actor, selectedMove, currentTimeSeconds);
            ResetAdvanceFailureState(candidate.Context.ActorFact.Actor);
            candidate.Context.Result = BattleRuntimeAiActionResult.Succeeded(candidate.Context.Request, "advanced");
            stream.Add(BuildMovementEvent(
                BattleEventKind.MovementStarted,
                battleId,
                tick,
                currentTimeSeconds,
                candidate.Context.ActorFact.Actor,
                candidate.Context.TargetFact?.Actor.ActorId ?? candidate.Context.ActorFact.Actor.ObjectiveZoneId,
                candidate.From,
                selectedMove,
                candidate.Context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective
                    ? "plan_objective_advance"
                    : "auto_advance"));
            movementEvents++;
            performanceCounters?.RecordMovementEvent(currentTimeSeconds);
        }

        return movementEvents;
    }

    private bool TryRetargetStaleAdvanceContext(
        TickContext context,
        IReadOnlyDictionary<string, TickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleNavigationGraph navigationGraph,
        string battleId,
        int tick,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters)
    {
        if (context == null ||
            context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget ||
            context.ActorFact.Actor.HitPoints <= 0)
        {
            return false;
        }

        BattleFlowFieldCache flowFields = new(performanceCounters);
        TickContext refreshed = BuildTickContext(
            context.ActorFact.Actor,
            tickStartFacts,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            battleId,
            tick,
            navigationFailureDiagnostics);
        if (refreshed.TargetFact == null ||
            refreshed.TargetFact.Value.Actor.HitPoints <= 0 ||
            refreshed.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget ||
            !refreshed.Proposal.HasMoveTo ||
            !string.IsNullOrWhiteSpace(refreshed.Proposal.FailureReason))
        {
            return false;
        }

        // Movement intents are built before same-tick damage is applied. If a
        // different actor kills that target first, this actor keeps its action
        // boundary and immediately spends it on the next live assault target.
        context.Request = refreshed.Request;
        context.TargetFact = refreshed.TargetFact;
        context.Proposal = refreshed.Proposal;
        context.ActorFact.Actor.TargetActorId = refreshed.TargetFact.Value.Actor.ActorId;
        ResetAdvanceFailureState(context.ActorFact.Actor);
        return true;
    }

    private BattleRuntimeAiActionRequest BuildCommandScopedAiActionRequest(
        TickStartActorFact actorFact,
        TickStartActorFact? targetFact)
    {
        if (actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst &&
            actorFact.Actor.HasObjectiveAnchor &&
            !IsObjectiveReached(actorFact) &&
            (targetFact == null ||
             GetOrthogonalAttackGap(actorFact, targetFact.Value) > System.Math.Max(1, actorFact.Actor.AttackRange)))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardObjective(actorFact.Actor.ActorId);
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

        return _aiExecutor.ChooseAction(BuildAiDecisionFacts(actorFact, targetFact)) ??
               BattleRuntimeAiActionRequest.Hold(actorFact.Actor.ActorId, "missing_ai_request");
    }

    private static BattleRuntimeAiDecisionFacts BuildAiDecisionFacts(
        TickStartActorFact actorFact,
        TickStartActorFact? targetFact)
    {
        return new BattleRuntimeAiDecisionFacts
        {
            ActorId = actorFact.Actor.ActorId ?? "",
            TargetActorId = targetFact?.Actor.ActorId ?? "",
            HasTarget = targetFact != null,
            DistanceToTarget = targetFact == null ? int.MaxValue : GetOrthogonalAttackGap(actorFact, targetFact.Value),
            AttackRange = System.Math.Max(1, actorFact.Actor.AttackRange),
            CanAttackNow = actorFact.AttackCharge >= 1.0
        };
    }

    private static int GetOrthogonalAttackGap(TickStartActorFact first, TickStartActorFact second)
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

    private static int GetSquareGridDistance(TickStartActorFact first, TickStartActorFact second)
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
