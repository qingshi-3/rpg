namespace Rpg.Application.Battle.Snapshots;

public sealed class ManaCostSkillCostSnapshot : BattleSkillCostSnapshot
{
    public string PoolId { get; set; } = "";
    public int Amount { get; set; }
    public BattleSkillCostPayTiming PayTiming { get; set; } = BattleSkillCostPayTiming.CommandAccepted;
    public BattleSkillRefundPolicy RefundPolicy { get; set; } = BattleSkillRefundPolicy.FailedBeforeRelease;
}
