using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCrowdMovementPlanner
{
    private const int FlowCostWeight = 100;
    private const int OccupiedPenalty = 1000;

    public static bool TryFindNextStepTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        out BattleGridCoord nextStep)
    {
        nextStep = default;
        IReadOnlyList<BattleGridCoord> candidates = FindNextStepCandidatesTowardTarget(
            actor,
            target,
            graph,
            occupancy,
            reservations,
            flowFields,
            preferSupportSlots,
            avoidOpeningNewAxisGapNearEngagedTarget);
        if (candidates.Count == 0)
        {
            return false;
        }

        nextStep = candidates[0];
        return true;
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget)
    {
        if (actor == null || target == null || graph == null || occupancy == null || reservations == null)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return new List<BattleGridCoord>();
        }

        BattleFlowField field = (flowFields ?? new BattleFlowFieldCache()).GetOrBuild(actor, target, graph, preferSupportSlots);
        if (!field.HasCosts || !field.TryGetCost(start, out int startCost))
        {
            return new List<BattleGridCoord>();
        }

        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                reservations.CanReserveMove(actor, start, neighbor, occupancy) == false && occupancy.CountOtherOccupiedCells(actor, neighbor) == 0 ||
                avoidOpeningNewAxisGapNearEngagedTarget && OpensNewAxisGap(actor, target, start, neighbor) ||
                !field.TryGetCost(neighbor, out int neighborCost) ||
                neighborCost >= startCost)
            {
                continue;
            }

            // Occupied next cells are proposals only. The reservation resolver
            // may accept them later when the occupant is also moving out in the
            // same tick; open alternatives still win because of this penalty.
            int score = neighborCost * FlowCostWeight +
                        occupancy.CountOtherOccupiedCells(actor, neighbor) * OccupiedPenalty +
                        GetStepCost(start, neighbor);
            MoveOption option = new(neighbor, score, neighborCost);
            options.Add(option);
        }

        return options
            .OrderBy(item => item, MoveOptionComparer.Instance)
            .Select(item => item.Anchor)
            .ToArray();
    }

    private static bool OpensNewAxisGap(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord start,
        BattleGridCoord candidate)
    {
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        BattleActorFootprint.GetAxisGaps(actor, start, target, targetAnchor, out int startGapX, out int startGapY);
        BattleActorFootprint.GetAxisGaps(actor, candidate, target, targetAnchor, out int candidateGapX, out int candidateGapY);
        return candidateGapX > startGapX || candidateGapY > startGapY;
    }

    private static bool IsBetter(MoveOption candidate, MoveOption known)
    {
        return candidate.Score < known.Score ||
               candidate.Score == known.Score && candidate.FlowCost < known.FlowCost ||
               candidate.Score == known.Score && candidate.FlowCost == known.FlowCost && IsBefore(candidate.Anchor, known.Anchor);
    }

    private static bool IsBefore(BattleGridCoord candidate, BattleGridCoord known)
    {
        return candidate.Height < known.Height ||
               candidate.Height == known.Height && candidate.Y < known.Y ||
               candidate.Height == known.Height && candidate.Y == known.Y && candidate.X < known.X;
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? BattlePathCostPolicy.DiagonalStepCost
            : BattlePathCostPolicy.StepCost;
    }

    private readonly record struct MoveOption(BattleGridCoord Anchor, int Score, int FlowCost);

    private sealed class MoveOptionComparer : IComparer<MoveOption>
    {
        public static readonly MoveOptionComparer Instance = new();

        public int Compare(MoveOption x, MoveOption y)
        {
            if (IsBetter(x, y))
            {
                return -1;
            }

            return IsBetter(y, x) ? 1 : 0;
        }
    }
}
