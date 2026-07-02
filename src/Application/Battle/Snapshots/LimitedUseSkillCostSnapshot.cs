namespace Rpg.Application.Battle.Snapshots;

public sealed class LimitedUseSkillCostSnapshot : BattleSkillCostSnapshot
{
    public int MaxUses { get; set; } = 1;
    public BattleSkillCostPayTiming ConsumeTiming { get; set; } = BattleSkillCostPayTiming.CommandAccepted;
    public BattleSkillRefundPolicy RefundPolicy { get; set; } = BattleSkillRefundPolicy.FailedBeforeRelease;
}
