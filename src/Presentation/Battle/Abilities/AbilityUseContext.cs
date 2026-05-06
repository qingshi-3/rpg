using System;
using System.Collections.Generic;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Abilities;

public sealed class AbilityUseContext
{
    public AbilityUseContext(
        BattleGridMap gridMap,
        IReadOnlyList<BattleEntity> entities,
        BattleEntity actor,
        BattleEntity target,
        GridPosition destination,
        AbilityDefinition ability,
        Action<BattleEntity> markEntityDefeated)
    {
        GridMap = gridMap;
        Entities = entities;
        Actor = actor;
        Target = target;
        Destination = destination;
        Ability = ability;
        MarkEntityDefeated = markEntityDefeated;
    }

    public BattleGridMap GridMap { get; }
    public IReadOnlyList<BattleEntity> Entities { get; }
    public BattleEntity Actor { get; }
    public BattleEntity Target { get; }
    public GridPosition Destination { get; }
    public AbilityDefinition Ability { get; }
    public Action<BattleEntity> MarkEntityDefeated { get; }
}
