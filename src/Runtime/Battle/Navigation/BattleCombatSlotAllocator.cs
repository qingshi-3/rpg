using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || graph == null)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        long startedAt = Stopwatch.GetTimestamp();
        int attackRange = System.Math.Max(1, actor.AttackRange);
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        List<BattleCombatSlot> slots = new();
        int scannedAnchors = 0;
        foreach (BattleGridCoord anchor in GetCandidateAnchors(actor, target, graph, targetAnchor, attackRange))
        {
            scannedAnchors++;
            // Engaged local combat may narrow legal slot goals to the group's
            // Runtime-owned local region; topology remains the final movement authority.
            if (!IsInsideLocalCombatRegion(anchor, localCombatRegion))
            {
                continue;
            }

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
                slots.Add(new BattleCombatSlot(anchor, BattleCombatSlotKind.Attack, gap, GetPriority(anchor, targetAnchor)));
                continue;
            }

            if (gap == attackRange + 1)
            {
                slots.Add(new BattleCombatSlot(
                    anchor,
                    BattleCombatSlotKind.Support,
                    gap,
                    GetPriority(anchor, targetAnchor) + 1000,
                    ResolveSupportRole(actor, target, anchor, targetAnchor)));
            }
        }

        performanceCounters?.RecordCombatSlotScan(scannedAnchors, Stopwatch.GetTimestamp() - startedAt);
        return slots
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Anchor.Height)
            .ThenBy(item => item.Anchor.Y)
            .ThenBy(item => item.Anchor.X)
            .ToArray();
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

    private static bool IsInsideLocalCombatRegion(
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

    private static int GetPriority(BattleGridCoord anchor, BattleGridCoord targetAnchor)
    {
        return System.Math.Abs(anchor.X - targetAnchor.X) +
               System.Math.Abs(anchor.Y - targetAnchor.Y) +
               System.Math.Abs(anchor.Height - targetAnchor.Height) * 4;
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
