using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    public bool TryResolveCellCenter(GridPosition cell, out Vector2 center)
    {
        center = Vector2.Zero;
        if (_coordinateLayer == null)
        {
            return false;
        }

        center = _highlightGeometry.BuildCellCenter(cell);
        return true;
    }

    public bool TryResolveCellPolygon(GridPosition cell, out Vector2[] polygon)
    {
        polygon = System.Array.Empty<Vector2>();
        if (_coordinateLayer == null)
        {
            return false;
        }

        polygon = _highlightGeometry.BuildCellPolygon(cell);
        return true;
    }
}
