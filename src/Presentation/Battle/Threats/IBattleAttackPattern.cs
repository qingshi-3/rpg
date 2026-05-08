using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Threats;

public interface IBattleAttackPattern
{
    IEnumerable<GridPosition> ProjectThreatCells(BattleAttackPatternContext context);
}
