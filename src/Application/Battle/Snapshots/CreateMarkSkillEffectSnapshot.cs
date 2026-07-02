namespace Rpg.Application.Battle.Snapshots;

public sealed class CreateMarkSkillEffectSnapshot : BattleSkillEffectSnapshot
{
    public override BattleSkillEffectSnapshotType EffectSnapshotType => BattleSkillEffectSnapshotType.CreateMark;
    public BattleSkillMarkKind MarkKind { get; set; } = BattleSkillMarkKind.ThunderMark;
    public double LifetimeSeconds { get; set; }
    public bool AttachToActorWhenTargeted { get; set; }
    public bool ReplaceExistingOwnedMark { get; set; } = true;
}
