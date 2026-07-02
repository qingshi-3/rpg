namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillTimingSnapshot
{
    public double CastSeconds { get; set; }
    public double ImpactDelaySeconds { get; set; }
    public double RecoverySeconds { get; set; }
}
