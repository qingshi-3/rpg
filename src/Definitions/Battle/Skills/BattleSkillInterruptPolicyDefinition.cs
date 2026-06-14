namespace Rpg.Definitions.Battle.Skills;

public sealed class BattleSkillInterruptPolicyDefinition
{
    public bool CanInterruptBasicAttackWindup { get; init; }
    public bool CanCancelBasicAttackRecovery { get; init; }
    public bool ReleasesWithoutOccupyingCaster { get; init; }
}
