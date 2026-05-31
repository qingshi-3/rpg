using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleFlowFieldBuilder
{
    private const int OrthogonalCost = 10;
    private const int DiagonalCost = 14;

    public static BattleFlowField Build(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        bool preferSupportSlots,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        performanceCounters?.RecordFlowFieldBuild();
        IReadOnlyList<BattleCombatSlot> slots = BattleCombatSlotAllocator.FindSlots(
            actor,
            target,
            graph,
            performanceCounters,
            localCombatRegion);
        BattleCombatSlot[] goals = preferSupportSlots
            ? slots.Where(item => item.Kind == BattleCombatSlotKind.Support).ToArray()
            : System.Array.Empty<BattleCombatSlot>();
        if (goals.Length == 0)
        {
            goals = slots
                .Where(item => item.Kind == BattleCombatSlotKind.Attack)
                .ToArray();
        }

        return BuildFromGoalSlots(actor, graph, goals, performanceCounters);
    }

    public static BattleFlowField BuildFromGoalSlots(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        IEnumerable<BattleCombatSlot> goals,
        BattlePerformanceCounters performanceCounters = null)
    {
        long startedAt = Stopwatch.GetTimestamp();
        BattleCombatSlot[] goalArray = (goals ?? Enumerable.Empty<BattleCombatSlot>()).ToArray();
        try
        {
            Dictionary<BattleGridCoord, int> costs = new();
            if (actor == null || graph == null || goalArray.Length == 0)
            {
                return new BattleFlowField(goalArray, costs);
            }

            var frontier = new PriorityQueue<BattleGridCoord, int>();
            var settled = new HashSet<BattleGridCoord>();
            foreach (BattleCombatSlot goal in goalArray)
            {
                if (!costs.TryAdd(goal.Anchor, 0))
                {
                    continue;
                }

                frontier.Enqueue(goal.Anchor, 0);
            }

            while (frontier.Count > 0 && settled.Count < graph.MaxSearchNodes)
            {
                BattleGridCoord current = frontier.Dequeue();
                if (!settled.Add(current))
                {
                    continue;
                }

                foreach (BattleGridCoord incoming in graph.GetIncomingNeighbors(current))
                {
                    if (!BattlePathStepRules.CanUseStaticStep(actor, incoming, current, graph))
                    {
                        continue;
                    }

                    int newCost = costs[current] + GetStepCost(incoming, current);
                    if (costs.TryGetValue(incoming, out int knownCost) && newCost >= knownCost)
                    {
                        continue;
                    }

                    costs[incoming] = newCost;
                    frontier.Enqueue(incoming, newCost);
                }
            }

            return new BattleFlowField(goalArray, costs);
        }
        finally
        {
            performanceCounters?.RecordFlowFieldBuildElapsedTicks(Stopwatch.GetTimestamp() - startedAt);
        }
    }

    public static BattleFlowField PreferOpenAttackSlots(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleFlowField field,
        BattlePerformanceCounters performanceCounters = null)
    {
        // Keep open attack-slot filtering under the cache owner so runtime callers
        // do not grow a second uncached implementation of the same decision facts.
        return new BattleFlowFieldCache(performanceCounters).PreferOpenAttackSlots(actor, graph, occupancy, field);
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? DiagonalCost
            : OrthogonalCost;
    }
}
