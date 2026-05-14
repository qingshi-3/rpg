using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public sealed class BattleGridHighlightCellDiff
{
    private BattleGridHighlightCellDiff(
        IReadOnlyList<GridPosition> cellsToErase,
        IReadOnlyList<GridPosition> cellsToPaint,
        HashSet<GridPosition> nextCells)
    {
        CellsToErase = cellsToErase;
        CellsToPaint = cellsToPaint;
        NextCells = nextCells;
    }

    public IReadOnlyList<GridPosition> CellsToErase { get; }
    public IReadOnlyList<GridPosition> CellsToPaint { get; }
    public HashSet<GridPosition> NextCells { get; }

    public static BattleGridHighlightCellDiff Build(
        IReadOnlySet<GridPosition> currentCells,
        IEnumerable<GridPosition> nextCells)
    {
        HashSet<GridPosition> current = currentCells?.ToHashSet() ?? new HashSet<GridPosition>();
        HashSet<GridPosition> next = nextCells?.ToHashSet() ?? new HashSet<GridPosition>();

        GridPosition[] cellsToErase = current
            .Where(cell => !next.Contains(cell))
            .OrderBy(cell => cell.X)
            .ThenBy(cell => cell.Y)
            .ToArray();
        GridPosition[] cellsToPaint = next
            .Where(cell => !current.Contains(cell))
            .OrderBy(cell => cell.X)
            .ThenBy(cell => cell.Y)
            .ToArray();

        return new BattleGridHighlightCellDiff(cellsToErase, cellsToPaint, next);
    }
}
