namespace Rpg.Presentation.Battle.AI;

public sealed class BattleAiDecisionFacts
{
    public bool HasValidContext { get; init; }
    public bool ActorCanAct { get; init; }
    public bool HasTarget { get; init; }
    public bool HasPrimaryAbility { get; init; }
    public string PrimaryAbilityId { get; init; } = "";
    public int PrimaryAbilityRange { get; init; }
    public int PrimaryAbilityPower { get; init; }
    public bool CanStrikeNow { get; init; }
    public int? MoveRange { get; init; }
    public string NearestHostileTargetId { get; init; } = "";
    public string LowestHealthHostileTargetId { get; init; } = "";
    public string Command { get; init; } = "";
    public bool HasLocalCombatObservation { get; init; }
    public string LocalCombatOwnerBattleGroupId { get; init; } = "";
    public string LocalCombatRegionId { get; init; } = "";
    public string LocalCombatTargetActorId { get; init; } = "";
    public int LocalCombatCenterCellX { get; init; }
    public int LocalCombatCenterCellY { get; init; }
    public int LocalCombatCenterCellHeight { get; init; }
    public int LocalCombatWidth { get; init; } = 1;
    public int LocalCombatHeight { get; init; } = 1;
    public int LocalCombatVersion { get; init; }
    public string LocalCombatSelectedSlotKind { get; init; } = "";
    public string LocalCombatSelectedSlotRole { get; init; } = "";
    public int LocalCombatSelectedSlotCellX { get; init; }
    public int LocalCombatSelectedSlotCellY { get; init; }
    public int LocalCombatSelectedSlotCellHeight { get; init; }
    public string LocalCombatReasonCode { get; init; } = "";
}
