namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillTargetingSnapshot
{
    public BattleSkillInputFlow InputFlow { get; set; } = BattleSkillInputFlow.SelectActor;
    public BattleSkillTargetKind TargetKind { get; set; } = BattleSkillTargetKind.Actor;
    public int Range { get; set; }
    public BattleSkillRangeMetric RangeMetric { get; set; } = BattleSkillRangeMetric.Manhattan;
    public BattleSkillAreaShape AreaShape { get; set; } = BattleSkillAreaShape.SingleActor;
    public int AreaRadius { get; set; }
    public BattleSkillDirectionMode DirectionMode { get; set; } = BattleSkillDirectionMode.None;
    public bool RequiresSelectedMark { get; set; }
    public BattleSkillMarkKind RequiredMarkKind { get; set; } = BattleSkillMarkKind.None;
    public int LandingRadius { get; set; }
    public string PreviewProfileId { get; set; } = "";
}
