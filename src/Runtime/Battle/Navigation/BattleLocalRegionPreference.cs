using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleLocalRegionPreference
{
    public const int OutsideStepPenalty = 50_000;
    public const int OutsideSlotPriorityPenalty = 10_000;

    public static bool Contains(BattleGridCoord anchor, BattleTacticalRegionSnapshot localCombatRegion)
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

    public static int GetStepPenalty(BattleGridCoord anchor, BattleTacticalRegionSnapshot localCombatRegion)
    {
        // Local combat regions describe commander preference and diagnostics,
        // not topology authority. Outside cells stay legal fallback route cells.
        return Contains(anchor, localCombatRegion) ? 0 : OutsideStepPenalty;
    }

    public static int GetSlotPriorityPenalty(BattleGridCoord anchor, BattleTacticalRegionSnapshot localCombatRegion)
    {
        return Contains(anchor, localCombatRegion) ? 0 : OutsideSlotPriorityPenalty;
    }
}
