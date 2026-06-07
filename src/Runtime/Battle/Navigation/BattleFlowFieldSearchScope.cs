using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Navigation;

internal readonly record struct BattleFlowFieldSearchScope(
    bool IsEnabled,
    int MinX,
    int MaxX,
    int MinY,
    int MaxY)
{
    private const int BattlefieldPadding = 6;

    public static BattleFlowFieldSearchScope None => default;

    public static BattleFlowFieldSearchScope FromRegion(BattleTacticalRegionSnapshot region)
    {
        if (region == null)
        {
            return None;
        }

        int width = System.Math.Max(1, region.Width);
        int height = System.Math.Max(1, region.Height);
        int minX = region.CenterCellX - (width - 1) / 2;
        int minY = region.CenterCellY - (height - 1) / 2;
        return new BattleFlowFieldSearchScope(
            true,
            minX - BattlefieldPadding,
            minX + width - 1 + BattlefieldPadding,
            minY - BattlefieldPadding,
            minY + height - 1 + BattlefieldPadding);
    }

    public bool Contains(BattleGridCoord anchor)
    {
        return !IsEnabled ||
               anchor.X >= MinX &&
               anchor.X <= MaxX &&
               anchor.Y >= MinY &&
               anchor.Y <= MaxY;
    }
}
