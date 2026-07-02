namespace Rpg.Application.Battle.Snapshots;

public sealed class TeleportToMarkSkillEffectSnapshot : BattleSkillEffectSnapshot
{
    public override BattleSkillEffectSnapshotType EffectSnapshotType => BattleSkillEffectSnapshotType.TeleportToMark;
    public BattleSkillMarkKind RequiredMarkKind { get; set; } = BattleSkillMarkKind.ThunderMark;
    public int LandingRadius { get; set; }
    public bool ConsumesMark { get; set; }
}
