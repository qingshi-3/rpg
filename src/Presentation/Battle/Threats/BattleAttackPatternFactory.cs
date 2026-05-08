using Rpg.Definitions.Battle.Abilities;

namespace Rpg.Presentation.Battle.Threats;

public static class BattleAttackPatternFactory
{
    private static readonly IBattleAttackPattern ManhattanRange = new ManhattanRangeAttackPattern();

    public static IBattleAttackPattern Resolve(AbilityDefinition ability)
    {
        return ManhattanRange;
    }
}
