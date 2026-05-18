namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleNavigationSurfaceSnapshot
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Height { get; set; }
    public int MoveCost { get; set; } = 1;
}
