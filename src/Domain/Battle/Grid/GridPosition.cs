namespace Rpg.Domain.Battle.Grid;

public readonly record struct GridPosition(int X, int Y)
{
    public override string ToString()
    {
        return $"({X}, {Y})";
    }
}
