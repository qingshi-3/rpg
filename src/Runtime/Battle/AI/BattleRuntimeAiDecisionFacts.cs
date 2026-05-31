namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiDecisionFacts
{
    public string ActorId { get; init; } = "";
    public string TargetActorId { get; init; } = "";
    public bool HasTarget { get; init; }
    public int DistanceToTarget { get; init; }
    public int AttackRange { get; init; }
    public bool CanAttackNow { get; init; }
    public bool HasLocalCombatSituation { get; init; }
    public string LocalCombatSituationId { get; init; } = "";
    public string LocalCombatOwnerBattleGroupId { get; init; } = "";
    public string LocalCombatRegionId { get; init; } = "";
    public int LocalCombatCenterCellX { get; init; }
    public int LocalCombatCenterCellY { get; init; }
    public int LocalCombatCenterCellHeight { get; init; }
    public int LocalCombatWidth { get; init; } = 1;
    public int LocalCombatHeight { get; init; } = 1;
    public int LocalCombatVersion { get; init; }
    public string LocalCombatRegionReasonCode { get; init; } = "";
    public string LocalCombatTargetActorId { get; init; } = "";
    public bool LocalCombatHasReachableAttackSlot { get; init; }
    public bool LocalCombatHasReachableSupportSlot { get; init; }
    public bool LocalCombatBlocksObjectiveRoute { get; init; }
    public bool LocalCombatInsideLeash { get; init; } = true;
    public string LocalCombatJoinReasonCode { get; init; } = "";
    public string LocalCombatSupportReasonCode { get; init; } = "";
    public string LocalCombatRejectReasonCode { get; init; } = "";
}
