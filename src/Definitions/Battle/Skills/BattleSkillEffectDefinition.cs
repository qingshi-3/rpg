namespace Rpg.Definitions.Battle.Skills;

public sealed class BattleSkillEffectDefinition
{
    public BattleSkillEffectKind Kind { get; init; } = BattleSkillEffectKind.Damage;
    public int Amount { get; init; }
}
