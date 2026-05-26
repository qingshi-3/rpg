namespace Rpg.Presentation.World.Sites;

public sealed class BattleObjectiveMapCell
{
    public int X { get; init; }
    public int Y { get; init; }
    public bool IsWater { get; init; }
    public bool IsWalkable { get; init; } = true;
}
