using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleFlowFieldBuilder
{
    private const int OrthogonalCost = 10;
    private const int DiagonalCost = 14;

    public static BattleFlowField Build(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        bool preferSupportSlots)
    {
        IReadOnlyList<BattleCombatSlot> slots = BattleCombatSlotAllocator.FindSlots(actor, target, graph);
        BattleCombatSlot[] goals = preferSupportSlots
            ? slots.Where(item => item.Kind == BattleCombatSlotKind.Support).ToArray()
            : System.Array.Empty<BattleCombatSlot>();
        if (goals.Length == 0)
        {
            goals = slots
                .Where(item => item.Kind == BattleCombatSlotKind.Attack)
                .ToArray();
        }

        Dictionary<BattleGridCoord, int> costs = new();
        if (actor == null || graph == null || goals.Length == 0)
        {
            return new BattleFlowField(goals, costs);
        }

        var frontier = new PriorityQueue<BattleGridCoord, int>();
        foreach (BattleCombatSlot goal in goals)
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

        return new BattleFlowField(goals, costs);
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? DiagonalCost
            : OrthogonalCost;
    }
}
