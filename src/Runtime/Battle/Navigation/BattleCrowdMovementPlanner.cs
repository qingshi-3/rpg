using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCrowdMovementPlanner
{
    private const int FlowCostWeight = 100;
    private const int AxisGapPenalty = 10000;

    public static bool TryFindNextStepTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        out BattleGridCoord nextStep,
        BattlePerformanceCounters performanceCounters = null)
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
            avoidOpeningNewAxisGapNearEngagedTarget,
            performanceCounters);
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
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        BattlePerformanceCounters performanceCounters = null)
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

        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuild(actor, target, graph, preferSupportSlots);
        if (!preferSupportSlots)
        {
            field = cache.PreferOpenAttackSlots(actor, graph, occupancy, field);
        }
        if (!field.HasCosts || !field.TryGetCost(start, out int startCost))
        {
            return new List<BattleGridCoord>();
        }

        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                !field.TryGetCost(neighbor, out int neighborCost) ||
                neighborCost >= startCost)
            {
                continue;
            }

            // The first committed step must already satisfy reservation authority:
            // same-tick released cells are not treated as open. Axis-gap opening
            // remains a preference near engaged targets, not a hard ban, so a
            // support unit can flank when the straight approach is occupied.
            int score = neighborCost * FlowCostWeight +
                        (avoidOpeningNewAxisGapNearEngagedTarget && OpensNewAxisGap(actor, target, start, neighbor) ? AxisGapPenalty : 0) +
                        GetStepCost(start, neighbor);
            MoveOption option = new(neighbor, score, neighborCost);
            options.Add(option);
        }

        return options
            .OrderBy(item => item, MoveOptionComparer.Instance)
            .Select(item => item.Anchor)
            .ToArray();
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardObjective(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters = null)
    {
        if (actor == null || graph == null || occupancy == null || reservations == null || !actor.HasObjectiveAnchor)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord objectiveAnchor = new(actor.ObjectiveGridX, actor.ObjectiveGridY, actor.ObjectiveGridHeight);
        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuildObjective(
            actor,
            graph,
            objectiveAnchor,
            actor.ObjectiveWidth,
            actor.ObjectiveHeight);
        if (!field.HasCosts || !field.TryGetCost(start, out int startCost) || startCost == 0)
        {
            return new List<BattleGridCoord>();
        }

        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                !field.TryGetCost(neighbor, out int neighborCost) ||
                neighborCost >= startCost)
            {
                continue;
            }

            int score = neighborCost * FlowCostWeight + GetStepCost(start, neighbor);
            options.Add(new MoveOption(neighbor, score, neighborCost));
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
