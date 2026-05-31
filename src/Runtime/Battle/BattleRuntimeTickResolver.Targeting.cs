using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
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

        if (IsHoldLineCommand(actorFact.CommandId))
        {
            return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact);
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.Hold)
        {
            // Hold-line player commands stay fixed, but an authored hold plan is
            // a guard posture: it should wake on local contact without global
            // attack-slot scoring across the whole map.
            return FindPlanScopedEnemyCorps(facts, actorFact);
        }

        if (actorFact.Actor.HasObjectiveAnchor)
        {
            if (IsObjectiveReached(actorFact))
            {
                // Objective zones are rally/approach intent, not a final idle
                // command. Once the group has reached the zone, normal contact
                // acquisition resumes so it can push into combat.
                return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
                       FindRetainedEnemyCorps(facts, actorFact) ??
                       FindFastestAttackOpportunityEnemyCorps(facts, actorFact, navigationGraph, occupancy, flowFields, performanceCounters) ??
                       FindNearestEnemyCorps(facts, actorFact);
            }

            return FindPlanScopedEnemyCorps(facts, actorFact);
        }

        // Mature RTS movement keeps target acquisition sticky while units are
        // marching. Full attack-opportunity scoring builds flow fields, so doing
        // it every movement step turns pathing into visible frame spikes. Units
        // still snap to immediate melee opportunities and fully score when they
        // do not yet have a live target.
        return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
               FindRetainedEnemyCorps(facts, actorFact) ??
               FindFastestAttackOpportunityEnemyCorps(facts, actorFact, navigationGraph, occupancy, flowFields, performanceCounters) ??
               FindNearestEnemyCorps(facts, actorFact);
    }

    private static TickStartActorFact? FindPlanScopedEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact)
    {
        // Once a battle group has an authored objective, local perception owns
        // target acquisition. This preserves the player plan and prevents
        // ordinary marching from rebuilding global attack-slot fields.
        TickStartActorFact? immediate = FindImmediateAttackOpportunityEnemyCorps(facts, actorFact);
        if (immediate != null)
        {
            return immediate;
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst)
        {
            return FindRouteBlockingEnemyCorps(facts, actorFact, PlannedLocalPerceptionRange);
        }

        TickStartActorFact? retained = FindRetainedEnemyCorps(
            facts,
            actorFact,
            PlannedLocalPerceptionRange);
        if (retained != null)
        {
            return retained;
        }

        return FindNearestEnemyCorps(
            facts,
            actorFact,
            PlannedLocalPerceptionRange);
    }

    private static TickStartActorFact? FindRetainedEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        int maxGap = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(actorFact.TargetActorId) ||
            !facts.TryGetValue(actorFact.TargetActorId, out TickStartActorFact retained))
        {
            return null;
        }

        return GetCurrentHitPoints(retained) > 0 &&
               !SameFaction(actorFact.Actor, retained.Actor) &&
               GetSquareGridDistance(actorFact, retained) <= maxGap
            ? retained
            : null;
    }

    private static TickStartActorFact? FindImmediateAttackOpportunityEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact)
    {
        TickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        int selectedHitPoints = int.MaxValue;
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);

        foreach (TickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int gap = GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, candidate.Actor, candidate.Anchor);
            if (gap > attackRange)
            {
                continue;
            }

            int hitPoints = GetCurrentHitPoints(candidate);
            if (selected == null ||
                gap < selectedGap ||
                gap == selectedGap && hitPoints < selectedHitPoints ||
                gap == selectedGap && hitPoints == selectedHitPoints &&
                string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
                selectedGap = gap;
                selectedHitPoints = hitPoints;
            }
        }

        return selected;
    }

    private static TickStartActorFact? FindNearestEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        int maxGap = int.MaxValue)
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
            if (gap > maxGap)
            {
                continue;
            }

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

    private static TickStartActorFact? FindRouteBlockingEnemyCorps(
        IReadOnlyDictionary<string, TickStartActorFact> facts,
        TickStartActorFact actorFact,
        int maxGap)
    {
        if (actorFact.Actor.HasObjectiveAnchor == false)
        {
            return null;
        }

        TickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        foreach (TickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                SameFaction(candidate.Actor, actorFact.Actor) ||
                !BlocksObjectiveRoute(actorFact.Actor, candidate.Actor))
            {
                continue;
            }

            int gap = GetSquareGridDistance(actorFact, candidate);
            if (gap > maxGap)
            {
                continue;
            }

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

    private static bool BlocksObjectiveRoute(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        if (actor?.HasObjectiveAnchor != true || target == null)
        {
            return false;
        }

        bool horizontalCorridor = actor.GridY == actor.ObjectiveGridY && target.GridY == actor.GridY;
        bool verticalCorridor = actor.GridX == actor.ObjectiveGridX && target.GridX == actor.GridX;
        return horizontalCorridor && IsBetween(actor.GridX, actor.ObjectiveGridX, target.GridX) ||
               verticalCorridor && IsBetween(actor.GridY, actor.ObjectiveGridY, target.GridY);
    }

    private static bool IsBetween(int first, int second, int value)
    {
        return value >= System.Math.Min(first, second) && value <= System.Math.Max(first, second);
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
        int fallbackGap,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || navigationGraph == null)
        {
            return 100000 + fallbackGap;
        }

        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuild(
            actor,
            target,
            navigationGraph,
            preferSupportSlots: false,
            localCombatRegion: localCombatRegion);
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
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        return ResolveAttackOpportunityTravelCost(
            actor,
            target,
            actorAnchor,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            fallbackGap: 0,
            localCombatRegion) < 5000;
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
