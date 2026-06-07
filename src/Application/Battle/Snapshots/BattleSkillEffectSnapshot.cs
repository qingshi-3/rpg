namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillEffectSnapshot
{
    public BattleSkillEffectKind Kind { get; set; } = BattleSkillEffectKind.Damage;
    public int Amount { get; set; }
}
