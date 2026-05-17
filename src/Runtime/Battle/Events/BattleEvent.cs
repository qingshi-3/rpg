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
    public bool HasMovementCells { get; set; }
    public int FromGridX { get; set; }
    public int FromGridY { get; set; }
    public int FromGridHeight { get; set; }
    public int ToGridX { get; set; }
    public int ToGridY { get; set; }
    public int ToGridHeight { get; set; }
}
