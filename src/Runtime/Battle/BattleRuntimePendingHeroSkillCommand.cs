namespace Rpg.Runtime.Battle;

internal sealed class BattleRuntimePendingHeroSkillCommand
{
    public string CommandId { get; init; } = "";
    public string BattleGroupId { get; init; } = "";
    public string SourceActorId { get; init; } = "";
    public string SkillDefinitionId { get; init; } = "";
    public string GrantedSkillId { get; init; } = "";
    public string LoadoutSlotId { get; init; } = "";
    public string OwnerHeroId { get; init; } = "";
    public string TargetActorId { get; init; } = "";
    public bool HasTargetGrid { get; init; }
    public int TargetGridX { get; init; }
    public int TargetGridY { get; init; }
    public int TargetGridHeight { get; init; }
    public string SelectedSpatialMarkId { get; init; } = "";
    public int LockedTargetGridX { get; init; }
    public int LockedTargetGridY { get; init; }
    public int LockedTargetGridHeight { get; init; }
    public double AcceptedAtSeconds { get; init; }
    public long AcceptedOrderSequence { get; init; }
}
