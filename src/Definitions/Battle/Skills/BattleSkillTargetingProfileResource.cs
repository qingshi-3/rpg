using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class BattleSkillTargetingProfileResource : Resource
{
    [Export] public BattleSkillInputFlowDefinition InputFlow { get; set; } = BattleSkillInputFlowDefinition.SelectActor;
    [Export] public BattleSkillTargetKindDefinition TargetKind { get; set; } = BattleSkillTargetKindDefinition.Actor;
    [Export] public int Range { get; set; }
    [Export] public BattleSkillRangeMetricDefinition RangeMetric { get; set; } = BattleSkillRangeMetricDefinition.Manhattan;
    [Export] public BattleSkillAreaShapeDefinition AreaShape { get; set; } = BattleSkillAreaShapeDefinition.SingleActor;
    [Export] public int AreaRadius { get; set; }
    [Export] public BattleSkillDirectionModeDefinition DirectionMode { get; set; } = BattleSkillDirectionModeDefinition.None;
    [Export] public bool RequiresSelectedMark { get; set; }
    [Export] public BattleSkillMarkKindDefinition RequiredMarkKind { get; set; } = BattleSkillMarkKindDefinition.None;
    [Export] public int LandingRadius { get; set; }
    [Export] public string PreviewProfileId { get; set; } = "";
}
