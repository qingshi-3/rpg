namespace Rpg.Presentation.Battle;

public static class BattleRenderSortPolicy
{
    public const int HeightZStride = 100;
    public const int UnitZOffset = 30;

    public static int GetUnitZIndex(int height)
    {
        return height * HeightZStride + UnitZOffset;
    }
}
