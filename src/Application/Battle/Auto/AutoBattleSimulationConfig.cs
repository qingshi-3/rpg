namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleSimulationConfig
{
    public int MaxTicks { get; set; } = 256;
    public int HealthPerUnit { get; set; } = 10;
    public int AttackDamage { get; set; } = 2;
    public int AttackRange { get; set; } = 1;
    public int AttackCooldownTicks { get; set; } = 1;
}
