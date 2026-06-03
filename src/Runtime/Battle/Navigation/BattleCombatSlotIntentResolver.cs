using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal readonly record struct BattleCombatSlotIntent(BattleGridCoord Anchor, BattleCombatSlotKind Kind);

internal static class BattleCombatSlotIntentResolver
{
    private const int FlowCostWeight = 1000;
    private const int KindFallbackPenalty = 100000;

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

    internal static bool TrySelectNewIntent(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        bool preferSupportSlots,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        out BattleCombatSlotIntent intent)
    {
        intent = default;
        if (actor == null || target == null || graph == null || occupancy == null)
        {
            return false;
        }

        BattleCombatSlot[] slots = BattleCombatSlotAllocator.FindSlots(
                actor,
                target,
                graph,
                performanceCounters,
                localCombatRegion)
            .Where(item => occupancy.CanPlaceFootprint(actor, item.Anchor))
            .ToArray();
        if (slots.Length == 0)
        {
            return false;
        }

        BattleCombatSlotIntentCandidate? selected = null;
        foreach (BattleCombatSlot slot in slots)
        {
            if (!TryScoreSlot(
                    actor,
                    actorAnchor,
                    graph,
                    slot,
                    preferSupportSlots,
                    performanceCounters,
                    out BattleCombatSlotIntentCandidate candidate))
            {
                continue;
            }

            if (selected == null || candidate.Score < selected.Value.Score)
            {
                selected = candidate;
            }
        }

        if (selected == null)
        {
            return false;
        }

        intent = new BattleCombatSlotIntent(selected.Value.Anchor, selected.Value.Kind);
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

        IReadOnlyList<BattleCombatSlotIntentCandidate> candidates = BuildScoredCandidates(
            actor,
            target,
            actorAnchor,
            graph,
            occupancy,
            preferSupportSlots,
            performanceCounters,
            localCombatRegion);
        if (candidates.Count == 0)
        {
            return false;
        }

        BattleCombatSlotKind preferredKind = preferSupportSlots
            ? BattleCombatSlotKind.Support
            : BattleCombatSlotKind.Attack;
        BattleCombatSlotKind fallbackKind = preferSupportSlots
            ? BattleCombatSlotKind.Attack
            : BattleCombatSlotKind.Support;
        if (ContainsCurrentSlot(candidates, actorAnchor, preferredKind))
        {
            return false;
        }

        if (TrySelectExecutableCandidate(
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

        if (ContainsCurrentSlot(candidates, actorAnchor, fallbackKind))
        {
            return false;
        }

        return TrySelectExecutableCandidate(
                actor,
                graph,
                occupancy,
                reservations,
                performanceCounters,
                localCombatRegion,
                candidates,
                fallbackKind,
                out intent,
                out moveOptions);
    }

    private static bool TrySelectExecutableCandidate(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion,
        IReadOnlyList<BattleCombatSlotIntentCandidate> candidates,
        BattleCombatSlotKind kind,
        out BattleCombatSlotIntent intent,
        out IReadOnlyList<BattleGridCoord> moveOptions)
    {
        intent = default;
        moveOptions = System.Array.Empty<BattleGridCoord>();
        foreach (BattleCombatSlotIntentCandidate candidate in candidates.Where(item => item.Kind == kind))
        {
            IReadOnlyList<BattleGridCoord> candidateMoves = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot(
                actor,
                candidate.Anchor,
                candidate.Kind,
                graph,
                occupancy,
                reservations,
                performanceCounters,
                localCombatRegion);
            if (candidateMoves.Count == 0)
            {
                continue;
            }

            // Local-combat movement is only allowed to leave the actor state
            // machine when the selected combat position also has an executable
            // next segment. This prevents blocked attack-position selection
            // from degrading into ordinary target chasing.
            intent = new BattleCombatSlotIntent(candidate.Anchor, candidate.Kind);
            moveOptions = candidateMoves;
            return true;
        }

        return false;
    }

    private static bool ContainsCurrentSlot(
        IReadOnlyList<BattleCombatSlotIntentCandidate> candidates,
        BattleGridCoord actorAnchor,
        BattleCombatSlotKind kind)
    {
        return candidates.Any(item =>
            item.Kind == kind &&
            item.Anchor == actorAnchor);
    }

    private static bool TryScoreSlot(
        BattleRuntimeActor actor,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph graph,
        BattleCombatSlot slot,
        bool preferSupportSlots,
        BattlePerformanceCounters performanceCounters,
        out BattleCombatSlotIntentCandidate candidate)
    {
        candidate = default;
        BattleFlowField field = BattleFlowFieldBuilder.BuildFromGoalSlots(
            actor,
            graph,
            new[] { slot },
            performanceCounters);
        if (!field.TryGetCost(actorAnchor, out int flowCost))
        {
            return false;
        }

        int kindPenalty = IsPreferredKind(slot.Kind, preferSupportSlots) ? 0 : KindFallbackPenalty;
        candidate = new BattleCombatSlotIntentCandidate(
            slot.Anchor,
            slot.Kind,
            kindPenalty + flowCost * FlowCostWeight + slot.Priority);
        return true;
    }

    private static IReadOnlyList<BattleCombatSlotIntentCandidate> BuildScoredCandidates(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        bool preferSupportSlots,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        BattleCombatSlot[] slots = BattleCombatSlotAllocator.FindSlots(
                actor,
                target,
                graph,
                performanceCounters,
                localCombatRegion)
            .Where(item => occupancy.CanPlaceFootprint(actor, item.Anchor))
            .ToArray();
        if (slots.Length == 0)
        {
            return System.Array.Empty<BattleCombatSlotIntentCandidate>();
        }

        List<BattleCombatSlotIntentCandidate> candidates = new();
        foreach (BattleCombatSlot slot in slots)
        {
            if (TryScoreSlot(
                    actor,
                    actorAnchor,
                    graph,
                    slot,
                    preferSupportSlots,
                    performanceCounters,
                    out BattleCombatSlotIntentCandidate candidate))
            {
                candidates.Add(candidate);
            }
        }

        return candidates
            .OrderBy(item => item.Score)
            .ThenBy(item => item.Anchor.Height)
            .ThenBy(item => item.Anchor.Y)
            .ThenBy(item => item.Anchor.X)
            .ToArray();
    }

    private static bool IsPreferredKind(BattleCombatSlotKind kind, bool preferSupportSlots)
    {
        return preferSupportSlots
            ? kind == BattleCombatSlotKind.Support
            : kind == BattleCombatSlotKind.Attack;
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
            : gap == attackRange + 1;
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

    private readonly record struct BattleCombatSlotIntentCandidate(
        BattleGridCoord Anchor,
        BattleCombatSlotKind Kind,
        int Score);
}
