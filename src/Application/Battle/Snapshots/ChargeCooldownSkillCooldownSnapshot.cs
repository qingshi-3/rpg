namespace Rpg.Application.Battle.Snapshots;

public sealed class ChargeCooldownSkillCooldownSnapshot : BattleSkillCooldownSnapshot
{
    public int MaxCharges { get; set; } = 1;
    public double RechargeSeconds { get; set; }
    public bool StartsFull { get; set; } = true;
}
