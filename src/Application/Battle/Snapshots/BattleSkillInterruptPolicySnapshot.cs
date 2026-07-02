namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillInterruptPolicySnapshot
{
    public bool CanInterruptBasicAttackWindup { get; set; }
    public bool CanCancelBasicAttackRecovery { get; set; }
    public bool ReleasesWithoutOccupyingCaster { get; set; }
    public bool CanInterruptActiveChannel { get; set; }
}
