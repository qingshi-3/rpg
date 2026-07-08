using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class BattleSkillDefinitionResource : Resource
{
    [Export] public string SkillDefinitionId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string IconText { get; set; } = "";
    [Export] public string IconPath { get; set; } = "";
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public BattleSkillCommandChannelDefinition CommandChannel { get; set; } = BattleSkillCommandChannelDefinition.Hero;
    [Export] public BattleSkillTypeDefinition SkillType { get; set; } = BattleSkillTypeDefinition.Active;
    [Export] public BattleSkillTimingResource Timing { get; set; } = new();
    [Export] public BattleSkillInterruptPolicyResource InterruptPolicy { get; set; } = new();
    [Export] public Godot.Collections.Array<BattleSkillCostRuleResource> CostRules { get; set; } = new();
    [Export] public Godot.Collections.Array<BattleSkillCooldownRuleResource> CooldownRules { get; set; } = new();
    [Export] public BattleSkillTargetingProfileResource Targeting { get; set; } = new();
    [Export] public Godot.Collections.Array<BattleSkillEffectResource> Effects { get; set; } = new();
    [Export] public BattleSkillPresentationProfileResource Presentation { get; set; } = new();
}
