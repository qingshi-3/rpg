using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCombatSlotAllocator
{
    public static IReadOnlyList<BattleCombatSlot> FindSlots(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattlePerformanceCounters performanceCounters = null)
    {
        if (actor == null || target == null || graph == null)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        int attackRange = System.Math.Max(1, actor.AttackRange);
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        List<BattleCombatSlot> slots = new();
        int scannedAnchors = 0;
        foreach (BattleGridCoord anchor in GetCandidateAnchors(actor, target, graph, targetAnchor, attackRange))
        {
            scannedAnchors++;
            if (!graph.CanPlaceFootprint(actor, anchor))
            {
                continue;
            }

            int gap = BattleActorFootprint.GetGap(actor, anchor, target, targetAnchor);
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
                slots.Add(new BattleCombatSlot(anchor, BattleCombatSlotKind.Support, gap, GetPriority(anchor, targetAnchor) + 1000));
            }
        }

        performanceCounters?.RecordCombatSlotScan(scannedAnchors);
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

    private static int GetPriority(BattleGridCoord anchor, BattleGridCoord targetAnchor)
    {
        return System.Math.Abs(anchor.X - targetAnchor.X) +
               System.Math.Abs(anchor.Y - targetAnchor.Y) +
               System.Math.Abs(anchor.Height - targetAnchor.Height) * 4;
    }
}
