using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattlePathfinder
{
    private const int StepCost = 10;
    private const long CostPriorityScale = 1_000_000L;
    private const long HeuristicPriorityScale = 1_000L;

    public static bool TryFindNextStepTowardAttackRange(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        out BattleGridCoord nextStep)
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
        frontier.Enqueue(start, BuildPriority(0, Estimate(actor, start, target, attackRange), sequence++));

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
                return TryResolveFirstStep(start, current, cameFrom, out nextStep);
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (closed.Contains(neighbor) ||
                    !CanEnterAnchor(actor, start, current, neighbor, graph, occupancy, reservations))
                {
                    continue;
                }

                int newCost = costSoFar[current] + graph.GetStepCost(current, neighbor, StepCost);
                if (costSoFar.TryGetValue(neighbor, out int knownCost) && newCost >= knownCost)
                {
                    continue;
                }

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                int heuristic = Estimate(actor, neighbor, target, attackRange);
                frontier.Enqueue(neighbor, BuildPriority(newCost, heuristic, sequence++));
            }
        }

        return false;
    }

    private static bool CanEnterAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations)
    {
        if (!graph.Contains(neighbor))
        {
            return false;
        }

        return current == start
            ? reservations.CanReserveMove(actor, start, neighbor, occupancy)
            : reservations.CanReserveFootprint(actor, neighbor, occupancy);
    }

    private static bool IsAttackAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleRuntimeActor target,
        int attackRange)
    {
        return BattleActorFootprint.GetGap(actor, anchor, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight)) <= attackRange;
    }

    private static int Estimate(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleRuntimeActor target,
        int attackRange)
    {
        int gap = BattleActorFootprint.GetGap(actor, anchor, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        return System.Math.Max(0, gap - attackRange) * StepCost;
    }

    private static long BuildPriority(int cost, int heuristic, long sequence)
    {
        return ((long)cost + heuristic) * CostPriorityScale +
               ((long)heuristic * HeuristicPriorityScale) +
               sequence;
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
