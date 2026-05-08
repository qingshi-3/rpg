using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Threats;

public sealed class BattleAttackPatternContext
{
    public BattleAttackPatternContext(
        BattleGridMap gridMap,
        BattleEntity actor,
        GridSurfacePosition origin,
        AbilityDefinition ability)
    {
        GridMap = gridMap;
        Actor = actor;
        Origin = origin;
        Ability = ability;
    }

    public BattleGridMap GridMap { get; }
    public BattleEntity Actor { get; }
    public GridSurfacePosition Origin { get; }
    public AbilityDefinition Ability { get; }
}
