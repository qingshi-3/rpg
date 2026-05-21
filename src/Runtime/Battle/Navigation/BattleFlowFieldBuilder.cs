using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        BattlePerformanceCounters performanceCounters = null)
    {
        performanceCounters?.RecordFlowFieldBuild();
        IReadOnlyList<BattleCombatSlot> slots = BattleCombatSlotAllocator.FindSlots(actor, target, graph, performanceCounters);
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
            foreach (BattleCombatSlot goal in goalArray)
            {
                if (!costs.TryAdd(goal.Anchor, 0))
                {
                    continue;
                }

                frontier.Enqueue(goal.Anchor, 0);
            }

            int searched = 0;
            while (frontier.Count > 0 && searched < graph.MaxSearchNodes)
            {
                BattleGridCoord current = frontier.Dequeue();
                searched++;
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
        if (actor == null || graph == null || occupancy == null || field?.GoalSlots == null)
        {
            return field;
        }

        BattleCombatSlot[] attackGoals = field.GoalSlots
            .Where(item => item.Kind == BattleCombatSlotKind.Attack)
            .ToArray();
        if (attackGoals.Length == 0)
        {
            return field;
        }

        BattleCombatSlot[] openAttackGoals = attackGoals
            .Where(item => occupancy.CountOtherOccupiedCells(actor, item.Anchor) == 0)
            .ToArray();
        if (openAttackGoals.Length == 0 || openAttackGoals.Length == attackGoals.Length)
        {
            return field;
        }

        // Occupied attack slots are not executable for this actor. Dynamic
        // decision costs must point at open slots before support fallback.
        return BuildFromGoalSlots(actor, graph, openAttackGoals, performanceCounters);
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? DiagonalCost
            : OrthogonalCost;
    }
}
