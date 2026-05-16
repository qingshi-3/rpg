namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleForceReport
{
    public string ForceId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    public int InitialCount { get; set; }
    public int SurvivedCount { get; set; }
    public int DefeatedCount { get; set; }
    public int AttackCount { get; set; }
    public int DamageDealt { get; set; }
    public int UnitsDefeated { get; set; }
}
