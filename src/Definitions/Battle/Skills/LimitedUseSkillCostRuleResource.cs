using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class LimitedUseSkillCostRuleResource : BattleSkillCostRuleResource
{
    [Export] public int MaxUses { get; set; } = 1;
    [Export] public BattleSkillCostPayTimingDefinition ConsumeTiming { get; set; } = BattleSkillCostPayTimingDefinition.CommandAccepted;
    [Export] public BattleSkillRefundPolicyDefinition RefundPolicy { get; set; } = BattleSkillRefundPolicyDefinition.FailedBeforeRelease;
}
