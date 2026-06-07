using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleCombatSlotPriorityComparer : IComparer<BattleCombatSlot>
{
    public static readonly BattleCombatSlotPriorityComparer Instance = new();

    private BattleCombatSlotPriorityComparer()
    {
    }

    public int Compare(BattleCombatSlot x, BattleCombatSlot y)
    {
        int priority = x.Priority.CompareTo(y.Priority);
        if (priority != 0)
        {
            return priority;
        }

        return CompareAnchor(x.Anchor, y.Anchor);
    }

    internal static int CompareAnchor(BattleGridCoord x, BattleGridCoord y)
    {
        int height = x.Height.CompareTo(y.Height);
        if (height != 0)
        {
            return height;
        }

        int row = x.Y.CompareTo(y.Y);
        return row != 0 ? row : x.X.CompareTo(y.X);
    }
}

internal sealed class BattleCombatSlotKindPriorityComparer : IComparer<BattleCombatSlot>
{
    public static readonly BattleCombatSlotKindPriorityComparer Instance = new();

    private BattleCombatSlotKindPriorityComparer()
    {
    }

    public int Compare(BattleCombatSlot x, BattleCombatSlot y)
    {
        int kind = x.Kind.CompareTo(y.Kind);
        if (kind != 0)
        {
            return kind;
        }

        return BattleCombatSlotPriorityComparer.Instance.Compare(x, y);
    }
}
