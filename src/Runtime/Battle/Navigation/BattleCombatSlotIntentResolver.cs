using System.Collections.Generic;
using System.Linq;
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
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        IReadOnlyList<BattleCombatSlot> candidates,
        BattleCombatSlotKind kind,
        out BattleCombatSlotIntent intent,
        out IReadOnlyList<BattleGridCoord> moveOptions)
    {
        intent = default;
        moveOptions = System.Array.Empty<BattleGridCoord>();
        BattleCombatSlot[] goals = (candidates ?? System.Array.Empty<BattleCombatSlot>())
            .Where(item => item.Kind == kind)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Anchor.Height)
            .ThenBy(item => item.Anchor.Y)
            .ThenBy(item => item.Anchor.X)
            .ToArray();
        if (goals.Length == 0)
        {
            return false;
        }

        // Candidate groups share one multi-goal field. The movement executor
        // still validates the chosen next step per actor through reservations.
        BattleFlowField field = BattleFlowFieldBuilder.BuildFromGoalSlots(
            actor,
            graph,
            goals,
            performanceCounters);
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
        bool contains = candidates.Any(item =>
            item.Kind == kind &&
            item.Anchor == actorAnchor);
        if (!contains)
        {
            return false;
        }

        if (kind == BattleCombatSlotKind.Attack)
        {
            return true;
        }

        int attackRange = System.Math.Max(1, actor?.AttackRange ?? 1);
        int gap = BattleActorFootprint.GetOrthogonalGap(
            actor,
            actorAnchor,
            target,
            new BattleGridCoord(target?.GridX ?? 0, target?.GridY ?? 0, target?.GridHeight ?? 0));
        return gap == attackRange + 1;
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
        BattleCombatSlot[] slots = BattleCombatSlotAllocator.FindSlots(
                actor,
                target,
                graph,
                performanceCounters,
                localCombatRegion,
                includeLocalCombatJoinSupport)
            .Where(item => occupancy.CanPlaceFootprint(actor, item.Anchor))
            .ToArray();
        if (slots.Length == 0)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        return slots
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Anchor.Height)
            .ThenBy(item => item.Anchor.Y)
            .ThenBy(item => item.Anchor.X)
            .ToArray();
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
            occupancy?.CanPlaceFootprint(actor, anchor) != true ||
            !IsAnchorInsideLocalCombatRegion(anchor, localCombatRegion))
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
}
