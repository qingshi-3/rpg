using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class ChargeCooldownRuleResource : BattleSkillCooldownRuleResource
{
    [Export] public int MaxCharges { get; set; } = 1;
    [Export] public double RechargeSeconds { get; set; }
    [Export] public bool StartsFull { get; set; } = true;
}
