using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattlePathfinder
{
    private const long CostPriorityScale = 1_000_000L;
    private const long HeuristicPriorityScale = 1_000L;

    public static bool TryFindNextStepTowardAttackRange(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        bool preferSupportWhenFirstStepMovesAway,
        out BattleGridCoord nextStep,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        nextStep = default;
        if (actor == null || target == null || graph == null || occupancy == null || reservations == null)
        {
            return false;
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return false;
        }

        int attackRange = System.Math.Max(1, actor.AttackRange);
        var frontier = new PriorityQueue<BattleGridCoord, long>();
        var cameFrom = new Dictionary<BattleGridCoord, BattleGridCoord>();
        var costSoFar = new Dictionary<BattleGridCoord, int>
        {
            [start] = 0
        };
        var closed = new HashSet<BattleGridCoord>();
        long sequence = 0;
        int startEstimate = BattlePathCostPolicy.EstimateToAttackRange(actor, start, target, attackRange);
        int bestSupportEstimate = startEstimate;
        int bestSupportCost = int.MaxValue;
        BattleGridCoord bestSupportAnchor = default;
        bool hasBestSupportAnchor = false;
        frontier.Enqueue(start, BuildPriority(0, startEstimate, sequence++));

        int searched = 0;
        while (frontier.Count > 0 && searched < graph.MaxSearchNodes)
        {
            BattleGridCoord current = frontier.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            searched++;
            if (current != start && IsAttackAnchor(actor, current, target, attackRange))
            {
                if (TryResolveFirstStep(start, current, cameFrom, out BattleGridCoord attackStep))
                {
                    if (ShouldPreferSupportStep(
                            actor,
                            target,
                            start,
                            attackStep,
                            hasBestSupportAnchor,
                            bestSupportAnchor,
                            cameFrom,
                            preferSupportWhenFirstStepMovesAway,
                            out nextStep))
                    {
                        return true;
                    }

                    nextStep = attackStep;
                    return true;
                }

                return false;
            }

            if (current != start)
            {
                int currentEstimate = BattlePathCostPolicy.EstimateToAttackRange(actor, current, target, attackRange);
                int currentCost = costSoFar.TryGetValue(current, out int value) ? value : int.MaxValue;
                if (currentEstimate < bestSupportEstimate ||
                    currentEstimate == bestSupportEstimate && currentCost < bestSupportCost)
                {
                    bestSupportEstimate = currentEstimate;
                    bestSupportCost = currentCost;
                    bestSupportAnchor = current;
                    hasBestSupportAnchor = true;
                }
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (closed.Contains(neighbor) ||
                    !IsAnchorInsideLocalCombatRegion(neighbor, localCombatRegion) ||
                    !CanUseCurrentDynamicStep(actor, start, current, neighbor, graph, occupancy, reservations))
                {
                    continue;
                }

                int newCost = costSoFar[current] +
                              BattlePathCostPolicy.GetTraversalCost(actor, start, current, neighbor, graph, occupancy);
                if (costSoFar.TryGetValue(neighbor, out int knownCost) && newCost >= knownCost)
                {
                    continue;
                }

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                int heuristic = BattlePathCostPolicy.EstimateToAttackRange(actor, neighbor, target, attackRange);
                frontier.Enqueue(neighbor, BuildPriority(newCost, heuristic, sequence++));
            }
        }

        // If the actual attack anchors are occupied by the frontline, still let
        // backline actors close to the best reachable support cell instead of
        // idling at their spawn until the blocker dies or moves.
        if (hasBestSupportAnchor)
        {
            return TryResolveFirstStep(start, bestSupportAnchor, cameFrom, out nextStep);
        }

        return false;
    }

    public static bool TryFindNextStepTowardAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord goal,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        out BattleGridCoord nextStep,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        nextStep = default;
        if (actor == null || graph == null || occupancy == null || reservations == null)
        {
            return false;
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start) ||
            start == goal ||
            !IsAnchorInsideLocalCombatRegion(goal, localCombatRegion) ||
            !graph.CanPlaceFootprint(actor, goal) ||
            !occupancy.CanPlaceFootprint(actor, goal))
        {
            return false;
        }

        var frontier = new PriorityQueue<BattleGridCoord, long>();
        var cameFrom = new Dictionary<BattleGridCoord, BattleGridCoord>();
        var costSoFar = new Dictionary<BattleGridCoord, int>
        {
            [start] = 0
        };
        var closed = new HashSet<BattleGridCoord>();
        long sequence = 0;
        frontier.Enqueue(start, BuildPriority(0, EstimateToAnchor(start, goal), sequence++));

        int searched = 0;
        while (frontier.Count > 0 && searched < graph.MaxSearchNodes)
        {
            BattleGridCoord current = frontier.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            searched++;
            if (current == goal)
            {
                return TryResolveFirstStep(start, current, cameFrom, out nextStep);
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (closed.Contains(neighbor) ||
                    !IsAnchorInsideLocalCombatRegion(neighbor, localCombatRegion) ||
                    !CanUseCurrentDynamicStep(actor, start, current, neighbor, graph, occupancy, reservations))
                {
                    continue;
                }

                int newCost = costSoFar[current] +
                              BattlePathCostPolicy.GetTraversalCost(actor, start, current, neighbor, graph, occupancy);
                if (costSoFar.TryGetValue(neighbor, out int knownCost) && newCost >= knownCost)
                {
                    continue;
                }

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                frontier.Enqueue(neighbor, BuildPriority(newCost, EstimateToAnchor(neighbor, goal), sequence++));
            }
        }

        return false;
    }

    private static bool ShouldPreferSupportStep(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord start,
        BattleGridCoord attackStep,
        bool hasBestSupportAnchor,
        BattleGridCoord bestSupportAnchor,
        IReadOnlyDictionary<BattleGridCoord, BattleGridCoord> cameFrom,
        bool preferSupportWhenFirstStepMovesAway,
        out BattleGridCoord supportStep)
    {
        supportStep = default;
        if (!preferSupportWhenFirstStepMovesAway || !hasBestSupportAnchor)
        {
            return false;
        }

        int startGap = BattleActorFootprint.GetGap(actor, start, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        int attackStepGap = BattleActorFootprint.GetGap(actor, attackStep, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        if (attackStepGap <= startGap)
        {
            return false;
        }

        if (!TryResolveFirstStep(start, bestSupportAnchor, cameFrom, out BattleGridCoord candidateSupportStep))
        {
            return false;
        }

        int supportStepGap = BattleActorFootprint.GetGap(actor, candidateSupportStep, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        if (supportStepGap >= startGap)
        {
            return false;
        }

        // When another ally is already in contact, V0 should reinforce the
        // local fight instead of sending support units away on a long flank.
        supportStep = candidateSupportStep;
        return true;
    }

    private static bool IsAttackAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleRuntimeActor target,
        int attackRange)
    {
        int gap = BattleActorFootprint.GetOrthogonalGap(
            actor,
            anchor,
            target,
            new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        return gap > 0 && gap <= attackRange;
    }

    private static bool CanUseCurrentDynamicStep(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations)
    {
        if (!BattlePathStepRules.CanUseStaticStep(actor, current, neighbor, graph))
        {
            return false;
        }

        // Detour fallback is only for currently executable local-combat paths.
        // It may move away for one step, but it must not route through live
        // footprints and hope they clear later.
        return current == start
            ? reservations.CanReserveMove(actor, start, neighbor, occupancy)
            : occupancy.CanPlaceFootprint(actor, neighbor);
    }

    private static bool IsAnchorInsideLocalCombatRegion(
        BattleGridCoord anchor,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (localCombatRegion == null)
        {
            return true;
        }

        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        return anchor.Height == localCombatRegion.CenterCellHeight &&
               anchor.X >= minX &&
               anchor.X < minX + width &&
               anchor.Y >= minY &&
               anchor.Y < minY + height;
    }

    private static long BuildPriority(int cost, int heuristic, long sequence)
    {
        return ((long)cost + heuristic) * CostPriorityScale +
               ((long)heuristic * HeuristicPriorityScale) +
               sequence;
    }

    private static int EstimateToAnchor(BattleGridCoord anchor, BattleGridCoord goal)
    {
        return System.Math.Max(
            System.Math.Abs(anchor.X - goal.X),
            System.Math.Abs(anchor.Y - goal.Y)) * BattlePathCostPolicy.StepCost +
            System.Math.Abs(anchor.Height - goal.Height) * BattlePathCostPolicy.StepCost * 4;
    }

    private static bool TryResolveFirstStep(
        BattleGridCoord start,
        BattleGridCoord goal,
        IReadOnlyDictionary<BattleGridCoord, BattleGridCoord> cameFrom,
        out BattleGridCoord nextStep)
    {
        nextStep = default;
        BattleGridCoord current = goal;
        while (cameFrom.TryGetValue(current, out BattleGridCoord previous))
        {
            if (previous == start)
            {
                nextStep = current;
                return true;
            }

            current = previous;
        }

        return false;
    }
}
