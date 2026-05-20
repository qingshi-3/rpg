namespace Rpg.Application.Battle.Navigation;

public sealed class BattleNavigationEdge
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int FromHeight { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public int ToHeight { get; set; }
    public int MoveCost { get; set; } = 1;
    public BattleNavigationEdgeKind Kind { get; set; } = BattleNavigationEdgeKind.GeneratedSameLevel;
}
