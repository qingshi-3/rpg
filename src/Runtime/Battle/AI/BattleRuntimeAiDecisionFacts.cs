namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiDecisionFacts
{
    public string ActorId { get; init; } = "";
    public string TargetActorId { get; init; } = "";
    public bool HasTarget { get; init; }
    public int DistanceToTarget { get; init; }
    public int AttackRange { get; init; }
    public bool CanAttackNow { get; init; }
}
