namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleNavigationConnectionSnapshot
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int FromHeight { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public int ToHeight { get; set; }
    public int MoveCost { get; set; } = 1;
}
