using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Entities;

public partial class GridOccupantComponent : BattleEntityComponent
{
    [ExportGroup("位置")]

    [Export]
    public int GridX { get; set; }

    [Export]
    public int GridY { get; set; }

    [Export]
    public int GridHeight { get; set; }

    [Export]
    public bool UseExplicitHeight { get; set; }

    [ExportGroup("阻挡")]

    [Export]
    public bool BlocksMovement { get; set; } = true;

    [Export]
    public bool BlocksLineOfSight { get; set; }

    public GridPosition Position => new(GridX, GridY);
    public GridSurfacePosition SurfacePosition => new(GridX, GridY, GridHeight);

    public void SetPosition(GridPosition position)
    {
        GridX = position.X;
        GridY = position.Y;
    }

    public void SetSurfacePosition(GridSurfacePosition position)
    {
        GridX = position.X;
        GridY = position.Y;
        GridHeight = position.Height;
    }
}
