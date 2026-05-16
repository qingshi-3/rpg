namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleReportEvent
{
    public int Tick { get; set; }
    public AutoBattleEventKind Kind { get; set; }
    public string SummaryKey { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ForceId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    public int Damage { get; set; }
    public int RemainingHealth { get; set; }
    public BattleOutcome Outcome { get; set; } = BattleOutcome.None;
}
