using System.Collections.Generic;

namespace Rpg.Domain.Battle.Grid;

public sealed class BattleGridMap
{
    private readonly Dictionary<GridPosition, GridCell> _cells = new();

    public IReadOnlyDictionary<GridPosition, GridCell> Cells => _cells;

    public GridCell GetOrCreateCell(GridPosition position)
    {
        if (_cells.TryGetValue(position, out GridCell cell))
        {
            return cell;
        }

        cell = new GridCell(position);
        _cells.Add(position, cell);
        return cell;
    }

    public bool TryGetCell(GridPosition position, out GridCell cell)
    {
        return _cells.TryGetValue(position, out cell);
    }
}
