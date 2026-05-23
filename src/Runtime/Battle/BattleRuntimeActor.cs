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
    // Runtime phase is the decision authority for tick resolution.
    public BattleRuntimeActorPhase Phase { get; set; } = BattleRuntimeActorPhase.AnchoredDecision;
    public string TargetActorId { get; set; } = "";
    public bool HasReservedGridCell { get; set; }
    public int ReservedGridX { get; set; }
    public int ReservedGridY { get; set; }
    public int ReservedGridHeight { get; set; }
    public bool HasMovementTarget { get; set; }
    public int MovementFromGridX { get; set; }
    public int MovementFromGridY { get; set; }
    public int MovementFromGridHeight { get; set; }
    public int MovementToGridX { get; set; }
    public int MovementToGridY { get; set; }
    public int MovementToGridHeight { get; set; }
    public double MovementStartedAtSeconds { get; set; }
    public double MovementDurationSeconds { get; set; }
    public double MovementProgress { get; set; }
    public int AttackRange { get; set; } = 1;
    public int AttackDamage { get; set; } = 1;
    public double AttackSpeed { get; set; } = 1.0;
    public double AttackCharge { get; set; } = 1.0;
    public double MoveStepSeconds { get; set; } = 0.16;
    public double AttackActionSeconds { get; set; } = 1.2;
    public double AttackImpactDelaySeconds { get; set; } = 0.66;
    // Central runtime time, in seconds, when this actor may submit its next action intent.
    public double ActionReadyAtSeconds { get; set; }
    public int ActionLockTicksRemaining { get; set; }
    public string ActionLockReason { get; set; } = "";
    public string CommandId { get; set; } = "";
    // Keeps blocked movement diagnosable without letting presentation infer combat truth.
    public int ConsecutiveAdvanceFailures { get; set; }
    public string LastAdvanceFailureReason { get; set; } = "";
}
