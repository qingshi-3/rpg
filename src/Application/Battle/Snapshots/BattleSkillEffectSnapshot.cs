namespace Rpg.Application.Battle.Snapshots;

public abstract class BattleSkillEffectSnapshot
{
    public abstract BattleSkillEffectSnapshotType EffectSnapshotType { get; }
    public BattleSkillEffectInstancePolicy EffectInstancePolicy { get; set; } = BattleSkillEffectInstancePolicy.Instant;
    public string PresentationProfileId { get; set; } = "";
}
