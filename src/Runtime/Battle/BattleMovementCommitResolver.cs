using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Stale-target rebuilds stay behind a narrow service callback; movement commit
// owns only stale-target detection and the point where a retarget may be spent.
internal delegate bool TryRetargetStaleAdvanceContextCallback(
    BattleRuntimeTickContext context,
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
    BattleDynamicOccupancy occupancy,
    BattleNavigationGraph navigationGraph,
    string battleId,
    int tick,
    double currentTimeSeconds,
    HashSet<string> navigationFailureDiagnostics,
    BattlePerformanceCounters performanceCounters,
    BattleGroupTacticalStateStore tacticalStateStore,
    IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
    IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones);

internal static class BattleMovementCommitResolver
{
    private sealed class MoveCandidate
    {
        public BattleRuntimeTickContext Context { get; init; }
        public BattleGridCoord From { get; init; }
        public BattleGridCoord To { get; init; }
        public IReadOnlyList<BattleGridCoord> OrderedMoves { get; init; } = new List<BattleGridCoord>();
    }

    private sealed class MoveCandidateComparer : IComparer<MoveCandidate>
    {
        public static readonly MoveCandidateComparer Instance = new();

        private MoveCandidateComparer()
        {
        }

        public int Compare(MoveCandidate x, MoveCandidate y)
        {
            int gap = GetReservationGap(x).CompareTo(GetReservationGap(y));
            if (gap != 0)
            {
                return gap;
            }

            int height = x.From.Height.CompareTo(y.From.Height);
            if (height != 0)
            {
                return height;
            }

            int row = x.From.Y.CompareTo(y.From.Y);
            if (row != 0)
            {
                return row;
            }

            int column = x.From.X.CompareTo(y.From.X);
            if (column != 0)
            {
                return column;
            }

            return string.Compare(
                x.Context?.ActorFact.Actor.BattleGroupId ?? "",
                y.Context?.ActorFact.Actor.BattleGroupId ?? "",
                System.StringComparison.Ordinal);
        }

        private static int GetReservationGap(MoveCandidate candidate)
        {
            return BattleActorFootprint.GetGap(
                candidate.Context.ActorFact.Actor,
                candidate.Context.ActorFact.Anchor,
                candidate.Context.TargetFact?.Actor ?? candidate.Context.ActorFact.Actor,
                candidate.Context.TargetFact?.Anchor ?? BattleObjectiveAdvancePlanner.GetObjectiveAnchor(candidate.Context.ActorFact.Actor));
        }
    }

    internal static int Resolve(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        TryRetargetStaleAdvanceContextCallback retargetStaleAdvanceContext)
    {
        int movementEvents = 0;
        List<MoveCandidate> moveCandidates = new();
        foreach (BattleRuntimeTickContext context in contexts ?? new List<BattleRuntimeTickContext>())
        {
            if (context?.Result != null ||
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget &&
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardObjective &&
                context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardRegion &&
                context.Request.Kind != BattleRuntimeAiActionKind.JoinLocalCombat &&
                context.Request.Kind != BattleRuntimeAiActionKind.HoldSupport &&
                context.Request.Kind != BattleRuntimeAiActionKind.ReturnToObjective)
            {
                continue;
            }

            bool isObjectiveAdvance = context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
                                      context.Request.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion ||
                                      context.Request.Kind == BattleRuntimeAiActionKind.ReturnToObjective;
            if (context.TargetFact == null && !isObjectiveAdvance)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (!context.Proposal.HasMoveTo)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "advance_failed");
                BattleAdvanceFailureStateBoundary.RecordAdvanceFailure(context.ActorFact.Actor, context.Proposal.FailureReason);
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
                if (!context.AllowStaleTargetRetarget ||
                    !retargetStaleAdvanceContext(
                        context,
                        tickStartFacts,
                        occupancy,
                        navigationGraph,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        navigationFailureDiagnostics,
                        performanceCounters,
                        tacticalStateStore,
                        groupActionZones,
                        combatZones))
                {
                    context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_defeated_before_move");
                    continue;
                }
            }

            IReadOnlyList<BattleGridCoord> orderedMoves = context.AllowReservationFallback && context.Proposal.MoveOptions?.Count > 0
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
        moveCandidates.Sort(MoveCandidateComparer.Instance);
        foreach (MoveCandidate candidate in moveCandidates)
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
                BattleAdvanceFailureStateBoundary.RecordAdvanceFailure(candidate.Context.ActorFact.Actor, "reservation_rejected");
                performanceCounters?.RecordHoldDueReservation();
                BattleRuntimeActorStateMachine.MarkHolding(candidate.Context.ActorFact.Actor, currentTimeSeconds);
                BattleRuntimeAdvanceDiagnostics.LogAdvanceFailureDiagnostic(
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

            BattleMovementCommitBoundary.ApplyAcceptedMove(
                candidate.Context,
                candidate.From,
                selectedMove,
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                performanceCounters);
            movementEvents++;
        }

        return movementEvents;
    }
}
