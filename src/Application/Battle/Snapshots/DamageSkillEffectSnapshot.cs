namespace Rpg.Application.Battle.Snapshots;

public sealed class DamageSkillEffectSnapshot : BattleSkillEffectSnapshot
{
    public override BattleSkillEffectSnapshotType EffectSnapshotType => BattleSkillEffectSnapshotType.Damage;
    public int BaseDamage { get; set; }
    public BattleSkillDamageType DamageType { get; set; } = BattleSkillDamageType.Physical;
    public bool CanHitActors { get; set; } = true;
    public bool CanHitWorldObjects { get; set; }
}
