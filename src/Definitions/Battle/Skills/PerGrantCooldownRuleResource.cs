using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class PerGrantCooldownRuleResource : BattleSkillCooldownRuleResource
{
    [Export] public double DurationSeconds { get; set; }
    [Export] public BattleSkillCooldownStartDefinition StartsOn { get; set; } = BattleSkillCooldownStartDefinition.CommandAccepted;
    [Export] public string SharedCooldownGroupId { get; set; } = "";
}
