using System.Collections.Generic;
using System.Diagnostics;
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
        BattleFlowFieldSearchScope searchScope = BattleFlowFieldSearchScope.FromRegion(localCombatRegion);
        IReadOnlyList<BattleCombatSlot> slots = BattleCombatSlotAllocator.FindSlots(
            actor,
            target,
            graph,
            performanceCounters,
            localCombatRegion);
        List<BattleCombatSlot> goalList = new();
        if (preferSupportSlots)
        {
            foreach (BattleCombatSlot slot in slots)
            {
                if (slot.Kind == BattleCombatSlotKind.Support)
                {
                    goalList.Add(slot);
                }
            }
        }

        if (goalList.Count == 0)
        {
            foreach (BattleCombatSlot slot in slots)
            {
                if (slot.Kind == BattleCombatSlotKind.Attack)
                {
                    goalList.Add(slot);
                }
            }
        }

        return BuildFromGoalSlots(actor, graph, goalList, performanceCounters, searchScope);
    }

    public static BattleFlowField BuildFromGoalSlots(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        IEnumerable<BattleCombatSlot> goals,
        BattlePerformanceCounters performanceCounters = null,
        BattleFlowFieldSearchScope searchScope = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        List<BattleCombatSlot> goalList = new();
        foreach (BattleCombatSlot goal in goals ?? System.Array.Empty<BattleCombatSlot>())
        {
            goalList.Add(goal);
        }

        goalList.Sort(BattleCombatSlotPriorityComparer.Instance);
        BattleCombatSlot[] goalArray = goalList.ToArray();
        BattleCombatSlot[] activeGoals = ResolveScopedGoals(goalArray, searchScope, performanceCounters, out BattleFlowFieldSearchScope activeScope);
        int searchedNodes = 0;
        try
        {
            Dictionary<BattleGridCoord, int> costs = new();
            Dictionary<BattleGridCoord, BattleCombatSlot> bestGoals = new();
            if (actor == null || graph == null || activeGoals.Length == 0)
            {
                return new BattleFlowField(activeGoals, costs);
            }

            var frontier = new PriorityQueue<BattleGridCoord, int>();
            var settled = new HashSet<BattleGridCoord>();
            if (activeScope.IsEnabled)
            {
                performanceCounters?.RecordScopedFlowFieldBuild();
            }

            foreach (BattleCombatSlot goal in activeGoals)
            {
                int goalCost = System.Math.Max(0, goal.Priority);
                if (!costs.TryAdd(goal.Anchor, goalCost))
                {
                    continue;
                }

                bestGoals[goal.Anchor] = goal;
                frontier.Enqueue(goal.Anchor, goalCost);
            }

            while (frontier.Count > 0 && settled.Count < graph.MaxSearchNodes)
            {
                BattleGridCoord current = frontier.Dequeue();
                if (!settled.Add(current))
                {
                    continue;
                }

                searchedNodes = settled.Count;
                foreach (BattleGridCoord incoming in graph.GetIncomingNeighbors(current))
                {
                    if (!activeScope.Contains(incoming))
                    {
                        continue;
                    }

                    if (!BattlePathStepRules.CanUseStaticStep(actor, incoming, current, graph))
                    {
                        continue;
                    }

                    int newCost = costs[current] + GetStepCost(incoming, current);
                    BattleCombatSlot currentGoal = bestGoals.TryGetValue(current, out BattleCombatSlot propagatedGoal)
                        ? propagatedGoal
                        : new BattleCombatSlot(current, BattleCombatSlotKind.Support, 0, int.MaxValue);
                    if (costs.TryGetValue(incoming, out int knownCost))
                    {
                        if (newCost > knownCost ||
                            newCost == knownCost &&
                            bestGoals.TryGetValue(incoming, out BattleCombatSlot knownGoal) &&
                            !IsPreferredGoal(currentGoal, knownGoal))
                        {
                            continue;
                        }
                    }

                    costs[incoming] = newCost;
                    bestGoals[incoming] = currentGoal;
                    frontier.Enqueue(incoming, newCost);
                }
            }

            return new BattleFlowField(activeGoals, costs, bestGoals);
        }
        finally
        {
            performanceCounters?.RecordFlowFieldSearchNodes(searchedNodes, activeScope.IsEnabled);
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

    private static BattleCombatSlot[] ResolveScopedGoals(
        BattleCombatSlot[] goals,
        BattleFlowFieldSearchScope searchScope,
        BattlePerformanceCounters performanceCounters,
        out BattleFlowFieldSearchScope activeScope)
    {
        activeScope = searchScope;
        if (!searchScope.IsEnabled || goals == null || goals.Length == 0)
        {
            return goals ?? System.Array.Empty<BattleCombatSlot>();
        }

        List<BattleCombatSlot> scoped = new();
        foreach (BattleCombatSlot goal in goals)
        {
            if (searchScope.Contains(goal.Anchor))
            {
                scoped.Add(goal);
            }
        }

        if (scoped.Count > 0)
        {
            return scoped.ToArray();
        }

        // Bounded fields are the normal local-combat path. A full fallback is
        // diagnostic-visible so an undersized battlefield scope does not hide.
        performanceCounters?.RecordFullFlowFieldFallback();
        activeScope = BattleFlowFieldSearchScope.None;
        return goals;
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? DiagonalCost
            : OrthogonalCost;
    }

    private static bool IsPreferredGoal(BattleCombatSlot candidate, BattleCombatSlot known)
    {
        return candidate.Priority < known.Priority ||
               candidate.Priority == known.Priority && candidate.Anchor.Height < known.Anchor.Height ||
               candidate.Priority == known.Priority && candidate.Anchor.Height == known.Anchor.Height && candidate.Anchor.Y < known.Anchor.Y ||
               candidate.Priority == known.Priority && candidate.Anchor.Height == known.Anchor.Height && candidate.Anchor.Y == known.Anchor.Y && candidate.Anchor.X < known.Anchor.X;
    }
}
