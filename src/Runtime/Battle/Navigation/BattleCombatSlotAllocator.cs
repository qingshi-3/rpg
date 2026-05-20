using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCombatSlotAllocator
{
    public static IReadOnlyList<BattleCombatSlot> FindSlots(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph)
    {
        if (actor == null || target == null || graph == null)
        {
            return System.Array.Empty<BattleCombatSlot>();
        }

        int attackRange = System.Math.Max(1, actor.AttackRange);
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        List<BattleCombatSlot> slots = new();
        foreach (BattleGridCoord anchor in graph.GetAnchors())
        {
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

        return slots
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Anchor.Height)
            .ThenBy(item => item.Anchor.Y)
            .ThenBy(item => item.Anchor.X)
            .ToArray();
    }

    private static int GetPriority(BattleGridCoord anchor, BattleGridCoord targetAnchor)
    {
        return System.Math.Abs(anchor.X - targetAnchor.X) +
               System.Math.Abs(anchor.Y - targetAnchor.Y) +
               System.Math.Abs(anchor.Height - targetAnchor.Height) * 4;
    }
}
