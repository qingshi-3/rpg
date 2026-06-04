using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
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
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
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
            performanceCounters,
            localCombatRegion);
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
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
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
        BattleFlowField field = cache.GetOrBuild(actor, target, graph, preferSupportSlots, localCombatRegion);
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
                IsImmediateReverseOfPreviousCombatStep(actor, start, neighbor) ||
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

        BattleGridCoord[] ordered = options
            .OrderBy(item => item, MoveOptionComparer.Instance)
            .Select(item => item.Anchor)
            .ToArray();
        if (ordered.Length > 0)
        {
            return ordered;
        }

        // Combat ingress sometimes needs one temporary detour when live
        // footprints occupy every step that directly lowers the shared flow
        // cost. This remains a fallback; shared flow fields are still the
        // normal battle navigation authority.
        if (localCombatRegion == null)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        BattleGridCoord? discouragedReverseStep = GetImmediateReverseStep(actor, start);
        if (BattlePathfinder.TryFindNextStepTowardAttackRange(
            actor,
            target,
            graph,
            occupancy,
            reservations,
            preferSupportWhenFirstStepMovesAway: false,
            out BattleGridCoord detourStep,
            localCombatRegion,
            discouragedReverseStep))
        {
            return new[] { detourStep };
        }

        return BattlePathfinder.TryFindNextStepTowardAttackRange(
            actor,
            target,
            graph,
            occupancy,
            reservations,
            preferSupportWhenFirstStepMovesAway: false,
            out detourStep,
            localCombatRegion)
            ? new[] { detourStep }
            : System.Array.Empty<BattleGridCoord>();
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardCombatSlot(
        BattleRuntimeActor actor,
        BattleGridCoord combatSlotAnchor,
        BattleCombatSlotKind combatSlotKind,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || graph == null || occupancy == null || reservations == null)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start) || start == combatSlotAnchor)
        {
            return new List<BattleGridCoord>();
        }

        BattleCombatSlot goal = new(combatSlotAnchor, combatSlotKind, 0, 0);
        BattleFlowField field = BattleFlowFieldBuilder.BuildFromGoalSlots(
            actor,
            graph,
            new[] { goal },
            performanceCounters);
        return FindNextStepCandidatesTowardCombatField(
            actor,
            field,
            graph,
            occupancy,
            reservations,
            out _,
            localCombatRegion);
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardCombatField(
        BattleRuntimeActor actor,
        BattleFlowField field,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        out BattleCombatSlot selectedGoal,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        selectedGoal = default;
        if (actor == null || field == null || graph == null || occupancy == null || reservations == null)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return new List<BattleGridCoord>();
        }

        if (!field.HasCosts || !field.TryGetCost(start, out int startCost))
        {
            return new List<BattleGridCoord>();
        }

        if (!field.TryGetBestGoal(start, out selectedGoal) && field.GoalSlots.Count > 0)
        {
            selectedGoal = field.GoalSlots[0];
        }
        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                IsImmediateReverseOfPreviousCombatStep(actor, start, neighbor) ||
                !field.TryGetCost(neighbor, out int neighborCost) ||
                neighborCost >= startCost)
            {
                continue;
            }

            int score = neighborCost * FlowCostWeight + GetStepCost(start, neighbor);
            options.Add(new MoveOption(neighbor, score, neighborCost));
        }

        BattleGridCoord[] ordered = options
            .OrderBy(item => item, MoveOptionComparer.Instance)
            .Select(item => item.Anchor)
            .ToArray();
        if (ordered.Length > 0)
        {
            if (field.TryGetBestGoal(ordered[0], out BattleCombatSlot nextGoal))
            {
                selectedGoal = nextGoal;
            }

            return ordered;
        }

        BattleGridCoord? discouragedReverseStep = GetImmediateReverseStep(actor, start);
        if (BattlePathfinder.TryFindNextStepTowardAnyAnchor(
            actor,
            field.GoalSlots,
            graph,
            occupancy,
            reservations,
            out BattleGridCoord detourStep,
            out BattleCombatSlot detourGoal,
            localCombatRegion,
            discouragedReverseStep))
        {
            selectedGoal = detourGoal;
            return new[] { detourStep };
        }

        if (BattlePathfinder.TryFindNextStepTowardAnyAnchor(
            actor,
            field.GoalSlots,
            graph,
            occupancy,
            reservations,
            out detourStep,
            out detourGoal,
            localCombatRegion))
        {
            selectedGoal = detourGoal;
            return new[] { detourStep };
        }

        return System.Array.Empty<BattleGridCoord>();
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

    private static bool IsImmediateReverseOfPreviousCombatStep(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord candidate)
    {
        BattleGridCoord? reverseStep = GetImmediateReverseStep(actor, start);
        return reverseStep.HasValue && reverseStep.Value == candidate;
    }

    private static BattleGridCoord? GetImmediateReverseStep(
        BattleRuntimeActor actor,
        BattleGridCoord start)
    {
        if (actor == null)
        {
            return null;
        }

        BattleGridCoord previousFrom = new(
            actor.MovementFromGridX,
            actor.MovementFromGridY,
            actor.MovementFromGridHeight);
        BattleGridCoord previousTo = new(
            actor.MovementToGridX,
            actor.MovementToGridY,
            actor.MovementToGridHeight);
        if (previousFrom == previousTo || previousTo != start)
        {
            return null;
        }

        // Prefer any other currently executable route first. If the full local
        // path genuinely needs a backtrack around live footprints, the detour
        // search may still use it instead of freezing the actor in place.
        return previousFrom;
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
