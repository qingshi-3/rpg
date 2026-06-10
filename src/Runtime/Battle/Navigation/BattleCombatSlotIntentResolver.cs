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
                target,
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
                target,
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
            target,
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
        BattleRuntimeActor target,
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

        int bestScore = int.MaxValue;
        BattleCombatSlot bestGoal = default;
        IReadOnlyList<BattleGridCoord> bestMoveOptions = System.Array.Empty<BattleGridCoord>();
        foreach (BattleCombatSlot goal in goals)
        {
            moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot(
                actor,
                goal.Anchor,
                goal.Kind,
                graph,
                occupancy,
                reservations,
                performanceCounters,
                localCombatRegion);
            if (moveOptions.Count == 0)
            {
                continue;
            }

            int score = ScoreExecutableGoal(actor, target, goal, moveOptions[0], occupancy);
            if (score < bestScore ||
                score == bestScore && IsBefore(goal.Anchor, bestGoal.Anchor))
            {
                bestScore = score;
                bestGoal = goal;
                bestMoveOptions = moveOptions;
            }
        }

        if (bestMoveOptions.Count == 0)
        {
            return false;
        }

        // Slot choice is a state-machine intent. Movement only proves the
        // immediate local step is executable; it does not construct a field
        // or claim that the whole future route is globally optimal.
        intent = new BattleCombatSlotIntent(bestGoal.Anchor, bestGoal.Kind);
        moveOptions = bestMoveOptions;
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

    private static int ScoreExecutableGoal(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleCombatSlot goal,
        BattleGridCoord firstStep,
        BattleDynamicOccupancy occupancy)
    {
        int attackRange = System.Math.Max(1, actor?.AttackRange ?? 1);
        int immediateAttackPenalty = IsAttackCapableAnchor(actor, firstStep, target, attackRange)
            ? 0
            : 10000;
        int targetGap = BattleActorFootprint.GetGap(
            actor,
            firstStep,
            target,
            new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        int slotStepDistance = System.Math.Abs(firstStep.X - goal.Anchor.X) +
                               System.Math.Abs(firstStep.Y - goal.Anchor.Y) +
                               System.Math.Abs(firstStep.Height - goal.Anchor.Height) * 4;
        return immediateAttackPenalty +
               GetBlockedLanePenalty(actor, target, firstStep, occupancy) +
               targetGap * 100 +
               goal.Priority * 10 +
               slotStepDistance;
    }

    private static int GetBlockedLanePenalty(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord firstStep,
        BattleDynamicOccupancy occupancy)
    {
        if (actor == null || target == null || occupancy == null)
        {
            return 0;
        }

        int targetWidth = BattleActorFootprint.NormalizeSize(target.FootprintWidth);
        int targetHeight = BattleActorFootprint.NormalizeSize(target.FootprintHeight);
        int targetMinX = target.GridX;
        int targetMaxX = target.GridX + targetWidth - 1;
        int targetMinY = target.GridY;
        int targetMaxY = target.GridY + targetHeight - 1;

        if (firstStep.X >= targetMinX && firstStep.X <= targetMaxX)
        {
            int laneY = firstStep.Y > targetMaxY ? targetMaxY + 1 : firstStep.Y < targetMinY ? targetMinY - 1 : int.MinValue;
            if (laneY != int.MinValue &&
                occupancy.IsOccupiedByOther(actor, new BattleGridCoord(firstStep.X, laneY, firstStep.Height)))
            {
                return 5000;
            }
        }

        if (firstStep.Y >= targetMinY && firstStep.Y <= targetMaxY)
        {
            int laneX = firstStep.X > targetMaxX ? targetMaxX + 1 : firstStep.X < targetMinX ? targetMinX - 1 : int.MinValue;
            if (laneX != int.MinValue &&
                occupancy.IsOccupiedByOther(actor, new BattleGridCoord(laneX, firstStep.Y, firstStep.Height)))
            {
                return 5000;
            }
        }

        return 0;
    }

    private static bool IsBefore(BattleGridCoord candidate, BattleGridCoord known)
    {
        return candidate.Height < known.Height ||
               candidate.Height == known.Height && candidate.Y < known.Y ||
               candidate.Height == known.Height && candidate.Y == known.Y && candidate.X < known.X;
    }
}
