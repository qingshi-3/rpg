namespace Rpg.Definitions.Battle.Skills;

public sealed class BattleSkillActionTimingDefinition
{
    public double CastSeconds { get; init; }
    public double ImpactDelaySeconds { get; init; }
    public double RecoverySeconds { get; init; }
}
