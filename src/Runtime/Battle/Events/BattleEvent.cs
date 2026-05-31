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
    public int RuntimeTick { get; set; } = -1;
    public double RuntimeTimeSeconds { get; set; }
    public double ActionDurationSeconds { get; set; }
    public double ActionImpactDelaySeconds { get; set; }
    public int CorpsStrengthDelta { get; set; }
    public bool HasActorCells { get; set; }
    public int ActorGridX { get; set; }
    public int ActorGridY { get; set; }
    public int ActorGridHeight { get; set; }
    public bool HasTargetCells { get; set; }
    public int TargetGridX { get; set; }
    public int TargetGridY { get; set; }
    public int TargetGridHeight { get; set; }
    public string TacticalRegionId { get; set; } = "";
    public string TacticalRegionKind { get; set; } = "";
    public int TacticalRegionVersion { get; set; }
    public bool HasMovementCells { get; set; }
    public int FromGridX { get; set; }
    public int FromGridY { get; set; }
    public int FromGridHeight { get; set; }
    public int ToGridX { get; set; }
    public int ToGridY { get; set; }
    public int ToGridHeight { get; set; }
}
