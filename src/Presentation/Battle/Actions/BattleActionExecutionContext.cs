using System;
using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Actions;

public sealed class BattleActionExecutionContext
{
    public BattleActionExecutionContext(
        BattleGridMap gridMap,
        IReadOnlyList<BattleEntity> entities,
        Action<BattleEntity, IReadOnlyList<GridSurfacePosition>> moveEntityTo,
        Action<BattleEntity> markEntityDefeated)
    {
        GridMap = gridMap;
        Entities = entities;
        MoveEntityTo = moveEntityTo;
        MarkEntityDefeated = markEntityDefeated;
    }

    public BattleGridMap GridMap { get; }
    public IReadOnlyList<BattleEntity> Entities { get; }
    public Action<BattleEntity, IReadOnlyList<GridSurfacePosition>> MoveEntityTo { get; }
    public Action<BattleEntity> MarkEntityDefeated { get; }
}
