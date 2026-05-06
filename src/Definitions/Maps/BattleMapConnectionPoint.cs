using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Definitions.Maps;

[GlobalClass]
public partial class BattleMapConnectionPoint : Resource
{
    [Export]
    public int X { get; set; }

    [Export]
    public int Y { get; set; }

    [Export]
    public int Height { get; set; }

    public GridSurfacePosition ToSurfacePosition()
    {
        return new GridSurfacePosition(X, Y, Height);
    }
}
