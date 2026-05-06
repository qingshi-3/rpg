namespace Rpg.Domain.Battle.Grid;

public readonly record struct GridSurfacePosition(int X, int Y, int Height)
{
    public GridSurfacePosition(GridPosition position, int height)
        : this(position.X, position.Y, height)
    {
    }

    public GridPosition Position => new(X, Y);

    public override string ToString()
    {
        return $"({X}, {Y}, h={Height})";
    }
}
