namespace Rpg.Runtime.Battle.Events;

public sealed class BattleEvent
{
    public string EventId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public BattleEventKind Kind { get; set; }
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string SourceCommandId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int CorpsStrengthDelta { get; set; }
}
