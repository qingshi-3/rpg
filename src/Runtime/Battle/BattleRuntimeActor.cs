namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeActor
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceForceId { get; set; } = "";
    public string SourceStateId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public int HitPoints { get; set; } = 1;
    public int Position { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridHeight { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public BattleRuntimeActorMotionState MotionState { get; set; } = BattleRuntimeActorMotionState.Anchored;
    public string TargetActorId { get; set; } = "";
    public bool HasReservedGridCell { get; set; }
    public int ReservedGridX { get; set; }
    public int ReservedGridY { get; set; }
    public int ReservedGridHeight { get; set; }
    public int AttackRange { get; set; } = 1;
    public double AttackSpeed { get; set; } = 1.0;
    public double AttackCharge { get; set; } = 1.0;
}
