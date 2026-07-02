using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class ManaCostSkillCostRuleResource : BattleSkillCostRuleResource
{
    [Export] public string PoolId { get; set; } = "mana";
    [Export] public int Amount { get; set; }
    [Export] public BattleSkillCostPayTimingDefinition PayTiming { get; set; } = BattleSkillCostPayTimingDefinition.CommandAccepted;
    [Export] public BattleSkillRefundPolicyDefinition RefundPolicy { get; set; } = BattleSkillRefundPolicyDefinition.FailedBeforeRelease;
}
