using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.AI;

public sealed class BattleAiContext
{
    public BattleAiContext(BattleGridMap gridMap, IReadOnlyList<BattleEntity> entities)
    {
        GridMap = gridMap;
        Entities = entities;
    }

    public BattleGridMap GridMap { get; }
    public IReadOnlyList<BattleEntity> Entities { get; }
}
