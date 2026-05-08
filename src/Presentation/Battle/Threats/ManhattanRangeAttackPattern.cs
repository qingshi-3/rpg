using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Threats;

public sealed class ManhattanRangeAttackPattern : IBattleAttackPattern
{
    public IEnumerable<GridPosition> ProjectThreatCells(BattleAttackPatternContext context)
    {
        if (context?.GridMap == null)
        {
            yield break;
        }

        GridPosition origin = context.Origin.Position;
        int range = System.Math.Max(0, context.Ability?.Range ?? 0);

        for (int x = origin.X - range; x <= origin.X + range; x++)
        {
            for (int y = origin.Y - range; y <= origin.Y + range; y++)
            {
                var position = new GridPosition(x, y);
                if (position == origin ||
                    BattleRuleQueries.GetManhattanDistance(origin, position) > range ||
                    !context.GridMap.TryGetCell(position, out _))
                {
                    continue;
                }

                yield return position;
            }
        }
    }
}
