using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal readonly record struct BattleCombatSlotIntent(BattleGridCoord Anchor, BattleCombatSlotKind Kind);

internal static class BattleCombatSlotIntentResolver
{
    internal static bool TryResolveStoredIntent(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleTacticalRegionSnapshot localCombatRegion,
        out BattleCombatSlotIntent intent)
    {
        intent = default;
        if (actor?.HasMovementIntentCombatSlot != true || target == null)
        {
            return false;
        }

        BattleCombatSlotKind kind = actor.MovementIntentCombatSlotKind;
        BattleGridCoord anchor = new(
            actor.MovementIntentCombatSlotX,
            actor.MovementIntentCombatSlotY,
            actor.MovementIntentCombatSlotHeight);
        if (!IsSlotStillValid(actor, target, anchor, kind, graph, occupancy, localCombatRegion))
        {
            return false;
        }

        intent = new BattleCombatSlotIntent(anchor, kind);
        return true;
    }

    internal static bool TrySelectExecutableIntent(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        bool preferSupportSlots,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        out BattleCombatSlotIntent intent,
        out IReadOnlyList<BattleGridCoord> moveOptions)
    {
        intent = default;
        moveOptions = System.Array.Empty<BattleGridCoord>();
        if (actor == null || target == null || graph == null || occupancy == null || reservations == null)
        {
            return false;
        }

        IReadOnlyList<BattleCombatSlot> candidates = BuildCandidateSlots(
            actor,
            target,
            graph,
            occupancy,
            performanceCounters,
            localCombatRegion);
        BattleCombatSlotKind preferredKind = preferSupportSlots
            ? BattleCombatSlotKind.Support
            : BattleCombatSlotKind.Attack;
        BattleCombatSlotKind fallbackKind = preferSupportSlots
            ? BattleCombatSlotKind.Attack
            : BattleCombatSlotKind.Support;
        if (ContainsCurrentTerminalSlot(candidates, actorAnchor, preferredKind, actor, target))
        {
            return false;
        }

        if (TrySelectExecutableCandidateGroup(
                actor,
                graph,
                occupancy,
                reservations,
                flowFields,
                performanceCounters,
                localCombatRegion,
                candidates,
                preferredKind,
                out intent,
                out moveOptions))
        {
            return true;
        }

        if (preferredKind == BattleCombatSlotKind.Support &&
            TrySelectExpandedSupportCandidate(
                actor,
                target,
                actorAnchor,
                graph,
                occupancy,
                reservations,
                flowFields,
                performanceCounters,
                localCombatRegion,
                out intent,
                out moveOptions))
        {
            return true;
        }

        if (ContainsCurrentTerminalSlot(candidates, actorAnchor, fallbackKind, actor, target))
        {
            return false;
        }

        if (TrySelectExecutableCandidateGroup(
                actor,
                graph,
                occupancy,
                reservations,
                flowFields,
                performanceCounters,
                localCombatRegion,
                candidates,
                fallbackKind,
                out intent,
                out moveOptions))
        {
            return true;
        }

        return fallbackKind == BattleCombatSlotKind.Support &&
               TrySelectExpandedSupportCandidate(
                   actor,
                   target,
                   actorAnchor,
                   graph,
                   occupancy,
                   reservations,
                   flowFields,
                   performanceCounters,
                   localCombatRegion,
                   out intent,
                   out moveOptions);
    }

    private static bool TrySelectExpandedSupportCandidate(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        out BattleCombatSlotIntent intent,
        out IReadOnlyList<BattleGridCoord> moveOptions)
    {
        intent = default;
        moveOptions = System.Array.Empty<BattleGridCoord>();
        if (localCombatRegion == null)
        {
            return false;
        }

        IReadOnlyList<BattleCombatSlot> candidates = BuildCandidateSlots(
            actor,
            target,
            graph,
            occupancy,
            performanceCounters,
            localCombatRegion,
            includeLocalCombatJoinSupport: true);
        return TrySelectExecutableCandidateGroup(
            actor,
            graph,
            occupancy,
            reservations,
            flowFields,
            performanceCounters,
            localCombatRegion,
            candidates,
            BattleCombatSlotKind.Support,
            out intent,
            out moveOptions);
    }

    private static bool TrySelectExecutableCandidateGroup(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        IReadOnlyList<BattleCombatSlot> candidates,
        BattleCombatSlotKind kind,
        out BattleCombatSlotIntent intent,
        out IReadOnlyList<BattleGridCoord> moveOptions)
    {
        intent = default;
        moveOptions = System.Array.Empty<BattleGridCoord>();
        List<BattleCombatSlot> goalList = new();
        foreach (BattleCombatSlot candidate in candidates ?? System.Array.Empty<BattleCombatSlot>())
        {
            if (candidate.Kind == kind)
            {
                goalList.Add(candidate);
            }
        }

        goalList.Sort(BattleCombatSlotPriorityComparer.Instance);
        BattleCombatSlot[] goals = goalList.ToArray();
        if (goals.Length == 0)
        {
            return false;
        }

        // Candidate groups share one battlefield-scoped multi-goal field. The
        // movement executor still validates the chosen next step per actor.
        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuildGoalField(
            actor,
            graph,
            goals,
            kind,
            localCombatRegion);
        moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatField(
            actor,
            field,
            graph,
            occupancy,
            reservations,
            out BattleCombatSlot selectedGoal,
            localCombatRegion);
        if (moveOptions.Count == 0)
        {
            return false;
        }

        intent = new BattleCombatSlotIntent(selectedGoal.Anchor, selectedGoal.Kind);
        return true;
    }

    private static bool ContainsCurrentTerminalSlot(
        IReadOnlyList<BattleCombatSlot> candidates,
        BattleGridCoord actorAnchor,
        BattleCombatSlotKind kind,
        BattleRuntimeActor actor,
        BattleRuntimeActor target)
    {
        bool contains = false;
        foreach (BattleCombatSlot candidate in candidates ?? System.Array.Empty<BattleCombatSlot>())
        {
            if (candidate.Kind == kind && candidate.Anchor == actorAnchor)
            {
                contains = true;
                break;
            }
        }

        if (!contains)
        {
            return false;
        }

        if (kind == BattleCombatSlotKind.Attack)
        {
            return true;
        }

        // Support slots are terminal staging positions for a local fight, not
        // transient waypoints that must keep pushing into occupied attack slots.
        return true;
    }

    private static IReadOnlyList<BattleCombatSlot> BuildCandidateSlots(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        bool includeLocalCombatJoinSupport = false)
    {
        IReadOnlyList<BattleCombatSlot> rawSlots = BattleCombatSlotAllocator.FindSlots(
                actor,
                target,
                graph,
                performanceCounters,
                localCombatRegion,
                includeLocalCombatJoinSupport);
        List<BattleCombatSlot> slots = new();
        foreach (BattleCombatSlot slot in rawSlots)
        {
            if (occupancy.CanPlaceFootprint(actor, slot.Anchor))
            {
                slots.Add(slot);
            }
        }

        if (slots.Count == 0)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        slots.Sort(BattleCombatSlotKindPriorityComparer.Instance);
        return slots.ToArray();
    }

    private static bool IsSlotStillValid(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord anchor,
        BattleCombatSlotKind kind,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (graph?.CanPlaceFootprint(actor, anchor) != true ||
            occupancy?.CanPlaceFootprint(actor, anchor) != true)
        {
            return false;
        }

        int attackRange = System.Math.Max(1, actor?.AttackRange ?? 1);
        int gap = BattleActorFootprint.GetOrthogonalGap(
            actor,
            anchor,
            target,
            new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        return kind == BattleCombatSlotKind.Attack
            ? gap > 0 && gap <= attackRange
            : !IsAttackCapableAnchor(actor, anchor, target, attackRange);
    }

    private static bool IsAttackCapableAnchor(
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
}
