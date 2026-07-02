namespace Rpg.Application.Battle.Snapshots;

public sealed class PerGrantCooldownSkillCooldownSnapshot : BattleSkillCooldownSnapshot
{
    public double DurationSeconds { get; set; }
    public BattleSkillCooldownStart StartsOn { get; set; } = BattleSkillCooldownStart.CommandAccepted;
    public string SharedCooldownGroupId { get; set; } = "";
}
