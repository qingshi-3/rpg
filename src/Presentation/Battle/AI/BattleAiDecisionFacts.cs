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
}
