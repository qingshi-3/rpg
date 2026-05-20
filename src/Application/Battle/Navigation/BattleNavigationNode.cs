namespace Rpg.Application.Battle.Navigation;

public sealed class BattleNavigationNode
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Height { get; set; }
    public int MoveCost { get; set; } = 1;
}
