using System.Collections.Generic;
using System.Diagnostics;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCombatSlotAllocator
{
    public static IReadOnlyList<BattleCombatSlot> FindSlots(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null,
        bool includeLocalCombatJoinSupport = false)
    {
        if (actor == null || target == null || graph == null)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        long startedAt = Stopwatch.GetTimestamp();
        int attackRange = System.Math.Max(1, actor.AttackRange);
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        List<BattleCombatSlot> slots = new();
        HashSet<BattleGridCoord> assignedAnchors = new();
        int scannedAnchors = 0;
        foreach (BattleGridCoord anchor in GetCandidateAnchors(actor, target, graph, targetAnchor, attackRange))
        {
            scannedAnchors++;
            if (!graph.CanPlaceFootprint(actor, anchor))
            {
                continue;
            }

            int gap = BattleActorFootprint.GetOrthogonalGap(actor, anchor, target, targetAnchor);
            if (gap <= 0)
            {
                continue;
            }

            if (gap <= attackRange)
            {
                if (assignedAnchors.Add(anchor))
                {
                    slots.Add(new BattleCombatSlot(anchor, BattleCombatSlotKind.Attack, gap, GetPriority(anchor, targetAnchor, localCombatRegion)));
                }

                continue;
            }

            if (gap == attackRange + 1)
            {
                if (assignedAnchors.Add(anchor))
                {
                    slots.Add(new BattleCombatSlot(
                        anchor,
                        BattleCombatSlotKind.Support,
                        gap,
                        GetPriority(anchor, targetAnchor, localCombatRegion) + 1000,
                        ResolveSupportRole(actor, target, anchor, targetAnchor)));
                }
            }
        }

        if (includeLocalCombatJoinSupport && localCombatRegion != null)
        {
            foreach (BattleGridCoord anchor in GetLocalCombatSupportAnchors(localCombatRegion, graph))
            {
                scannedAnchors++;
                if (!assignedAnchors.Add(anchor) ||
                    !graph.CanPlaceFootprint(actor, anchor))
                {
                    continue;
                }

                int orthogonalGap = BattleActorFootprint.GetOrthogonalGap(actor, anchor, target, targetAnchor);
                if (orthogonalGap > 0 && orthogonalGap <= attackRange)
                {
                    continue;
                }

                int gap = BattleActorFootprint.GetGap(actor, anchor, target, targetAnchor);
                if (gap <= 0)
                {
                    continue;
                }

                // These are combat-zone join positions, not target-adjacent attack
                // slots. They let large rear units route around live footprints while
                // still staying inside the commander-selected local fight.
                slots.Add(new BattleCombatSlot(
                    anchor,
                    BattleCombatSlotKind.Support,
                    gap,
                    GetPriority(anchor, targetAnchor, localCombatRegion) + 5000,
                    ResolveSupportRole(actor, target, anchor, targetAnchor)));
            }
        }

        performanceCounters?.RecordCombatSlotScan(scannedAnchors, Stopwatch.GetTimestamp() - startedAt);
        slots.Sort(BattleCombatSlotKindPriorityComparer.Instance);
        return slots.ToArray();
    }

    private static IEnumerable<BattleGridCoord> GetCandidateAnchors(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleGridCoord targetAnchor,
        int attackRange)
    {
        int actorWidth = BattleActorFootprint.NormalizeSize(actor.FootprintWidth);
        int actorHeight = BattleActorFootprint.NormalizeSize(actor.FootprintHeight);
        int targetWidth = BattleActorFootprint.NormalizeSize(target.FootprintWidth);
        int targetHeight = BattleActorFootprint.NormalizeSize(target.FootprintHeight);
        int maxGap = attackRange + 1;

        // Combat slot rules are target-local. The final legality remains
        // graph.CanPlaceFootprint plus BattleActorFootprint.GetGap below.
        int minX = targetAnchor.X - maxGap - actorWidth + 1;
        int maxX = targetAnchor.X + targetWidth - 1 + maxGap;
        int minY = targetAnchor.Y - maxGap - actorHeight + 1;
        int maxY = targetAnchor.Y + targetHeight - 1 + maxGap;
        return graph.GetAnchorsInBounds(minX, maxX, minY, maxY);
    }

    private static IEnumerable<BattleGridCoord> GetLocalCombatSupportAnchors(
        BattleTacticalRegionSnapshot localCombatRegion,
        BattleNavigationGraph graph)
    {
        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        int maxX = minX + width - 1;
        int maxY = minY + height - 1;
        List<BattleGridCoord> anchors = new();
        foreach (BattleGridCoord anchor in graph.GetAnchorsInBounds(minX, maxX, minY, maxY))
        {
            if (anchor.Height == localCombatRegion.CenterCellHeight)
            {
                anchors.Add(anchor);
            }
        }

        return anchors;
    }

    private static int GetPriority(
        BattleGridCoord anchor,
        BattleGridCoord targetAnchor,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        return System.Math.Abs(anchor.X - targetAnchor.X) +
               System.Math.Abs(anchor.Y - targetAnchor.Y) +
               System.Math.Abs(anchor.Height - targetAnchor.Height) * 4 +
               BattleLocalRegionPreference.GetSlotPriorityPenalty(anchor, localCombatRegion);
    }

    private static LocalCombatSupportSlotRole ResolveSupportRole(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord anchor,
        BattleGridCoord targetAnchor)
    {
        if (actor?.AttackRange > 1)
        {
            return LocalCombatSupportSlotRole.RangedHold;
        }

        int targetYMin = targetAnchor.Y;
        int targetYMax = targetAnchor.Y + BattleActorFootprint.NormalizeSize(target?.FootprintHeight ?? 1) - 1;
        if (anchor.Y >= targetYMin && anchor.Y <= targetYMax)
        {
            return LocalCombatSupportSlotRole.MeleeQueue;
        }

        return LocalCombatSupportSlotRole.LineHold;
    }
}
