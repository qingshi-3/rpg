namespace Rpg.Application.Battle.Snapshots;

public sealed class ChanneledAreaDamageSkillEffectSnapshot : BattleSkillEffectSnapshot
{
    public override BattleSkillEffectSnapshotType EffectSnapshotType => BattleSkillEffectSnapshotType.ChanneledAreaDamage;
    public int BaseDamage { get; set; }
    public BattleSkillDamageType DamageType { get; set; } = BattleSkillDamageType.Lightning;
    public double DurationSeconds { get; set; }
    public double TickIntervalSeconds { get; set; }
    public BattleSkillAreaShape AreaShape { get; set; } = BattleSkillAreaShape.GridRadius;
    public int Radius { get; set; }
    public bool FollowsCaster { get; set; }
    public bool UsesTargetOffset { get; set; }
}
