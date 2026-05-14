using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Preview;

public sealed class BattleHoverCellPresentation
{
    private BattleHoverCellPresentation(
        IReadOnlyList<GridPosition> attackCells,
        IReadOnlyList<GridPosition> targetCells,
        IReadOnlyList<GridPosition> targetPointerCells)
    {
        AttackCells = attackCells;
        TargetCells = targetCells;
        TargetPointerCells = targetPointerCells;
    }

    public IReadOnlyList<GridPosition> AttackCells { get; }

    public IReadOnlyList<GridPosition> TargetCells { get; }

    public IReadOnlyList<GridPosition> TargetPointerCells { get; }

    public static BattleHoverCellPresentation Build(
        IEnumerable<GridPosition> attackCells,
        IEnumerable<GridPosition> targetCells)
    {
        GridPosition[] distinctTargetCells = targetCells?
            .Distinct()
            .ToArray() ?? Array.Empty<GridPosition>();
        HashSet<GridPosition> targetSet = distinctTargetCells.ToHashSet();

        // Target pointers own occupied target cells, so range paint stays off the unit footprint.
        GridPosition[] filteredAttackCells = attackCells?
            .Where(cell => !targetSet.Contains(cell))
            .Distinct()
            .ToArray() ?? Array.Empty<GridPosition>();

        return new BattleHoverCellPresentation(filteredAttackCells, Array.Empty<GridPosition>(), distinctTargetCells);
    }
}
