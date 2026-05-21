using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private readonly record struct AssaultTargetScore(int TravelCost, int RetainedPriority, int Gap, string ActorId);

    private static TickStartActorFact? FindEnemyCorpsForCommand(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        if (IsFocusFireCommand(actorFact.CommandId))
        {
            return FindLowestHealthEnemyCorps(facts, actorFact);
        }

        return FindFastestAttackOpportunityEnemyCorps(facts, actorFact, navigationGraph, occupancy, flowFields, performanceCounters) ??
               FindRetainedEnemyCorps(facts, actorFact) ??
               FindNearestEnemyCorps(facts, actorFact);
    }

    private static TickStartActorFact? FindRetainedEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact)
    {
        if (string.IsNullOrWhiteSpace(actorFact.TargetActorId) ||
            !facts.TryGetValue(actorFact.TargetActorId, out TickStartActorFact retained))
        {
            return null;
        }

        return GetCurrentHitPoints(retained) > 0 && !SameFaction(actorFact.Actor, retained.Actor)
            ? retained
            : null;
    }

    private static TickStartActorFact? FindNearestEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact)
    {
        TickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        foreach (TickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int gap = GetSquareGridDistance(actorFact, candidate);
            if (selected == null ||
                gap < selectedGap ||
                gap == selectedGap && string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
                selectedGap = gap;
            }
        }

        return selected;
    }

    private static TickStartActorFact? FindFastestAttackOpportunityEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        long startedAt = Stopwatch.GetTimestamp();
        TickStartActorFact? retained = FindRetainedEnemyCorps(facts, actorFact);
        TickStartActorFact? selected = null;
        AssaultTargetScore selectedScore = default;
        try
        {
            foreach (TickStartActorFact candidate in facts.Values)
            {
                if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                    GetCurrentHitPoints(candidate) <= 0 ||
                    SameFaction(candidate.Actor, actorFact.Actor))
                {
                    continue;
                }

                AssaultTargetScore score = ScoreAssaultTarget(
                    actorFact,
                    candidate,
                    retained?.Actor.ActorId ?? "",
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters);
                if (selected == null || IsBetterAssaultTarget(score, selectedScore))
                {
                    selected = candidate;
                    selectedScore = score;
                }
            }

            return selected;
        }
        finally
        {
            performanceCounters?.RecordTargetScoringElapsedTicks(Stopwatch.GetTimestamp() - startedAt);
        }
    }

    private static AssaultTargetScore ScoreAssaultTarget(
        TickStartActorFact actorFact,
        TickStartActorFact targetFact,
        string retainedTargetId,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
        int gap = GetSquareGridDistance(actorFact, targetFact);
        int travelCost = gap <= attackRange
            ? 0
            : ResolveAttackOpportunityTravelCost(
                BuildTickStartProjection(actorFact),
                BuildTickStartProjection(targetFact),
                actorFact.Anchor,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                gap);
        int retainedPriority = string.Equals(targetFact.Actor.ActorId, retainedTargetId, System.StringComparison.Ordinal)
            ? 0
            : 1;
        return new AssaultTargetScore(travelCost, retainedPriority, gap, targetFact.Actor.ActorId ?? "");
    }

    private static int ResolveAttackOpportunityTravelCost(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        int fallbackGap)
    {
        if (actor == null || target == null || navigationGraph == null)
        {
            return 100000 + fallbackGap;
        }

        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuild(actor, target, navigationGraph, preferSupportSlots: false);
        field = cache.PreferOpenAttackSlots(actor, navigationGraph, occupancy, field);
        if (!field.TryGetCost(actorAnchor, out int cost))
        {
            return 100000 + fallbackGap;
        }

        bool hasOpenAttackSlot = field.GoalSlots.Any(slot => slot.Kind == BattleCombatSlotKind.Attack);
        return hasOpenAttackSlot ? cost : cost + 5000;
    }

    private static bool HasReachableAttackSlot(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        return ResolveAttackOpportunityTravelCost(
            actor,
            target,
            actorAnchor,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            fallbackGap: 0) < 5000;
    }

    private static bool IsBetterAssaultTarget(AssaultTargetScore candidate, AssaultTargetScore known)
    {
        return candidate.TravelCost < known.TravelCost ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority < known.RetainedPriority ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority == known.RetainedPriority && candidate.Gap < known.Gap ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority == known.RetainedPriority && candidate.Gap == known.Gap &&
               string.CompareOrdinal(candidate.ActorId, known.ActorId) < 0;
    }

    private static TickStartActorFact? FindLowestHealthEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact)
    {
        TickStartActorFact? selected = null;
        foreach (TickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            if (selected == null)
            {
                selected = candidate;
                continue;
            }

            int compare = GetCurrentHitPoints(candidate).CompareTo(GetCurrentHitPoints(selected.Value));
            if (compare < 0)
            {
                selected = candidate;
                continue;
            }

            if (compare > 0)
            {
                continue;
            }

            int gapCompare = GetSquareGridDistance(actorFact, candidate)
                .CompareTo(GetSquareGridDistance(actorFact, selected.Value));
            if (gapCompare < 0 ||
                gapCompare == 0 && string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
            }
        }

        return selected;
    }

    private static bool IsTargetEngagedBySameFactionActor(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        TickStartActorFact targetFact)
    {
        foreach (TickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                !SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            if (GetOrthogonalAttackGap(
                    candidate.Actor,
                    candidate.Anchor,
                    targetFact.Actor,
                    targetFact.Anchor) <= System.Math.Max(1, candidate.Actor.AttackRange))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetCurrentHitPoints(TickStartActorFact fact)
    {
        return fact.Actor?.HitPoints ?? fact.HitPoints;
    }
}
