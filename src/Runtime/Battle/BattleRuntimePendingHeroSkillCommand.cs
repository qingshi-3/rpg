namespace Rpg.Runtime.Battle;

internal sealed class BattleRuntimePendingHeroSkillCommand
{
    public string CommandId { get; init; } = "";
    public string BattleGroupId { get; init; } = "";
    public string SourceActorId { get; init; } = "";
    public string SkillId { get; init; } = "";
    public string TargetActorId { get; init; } = "";
    public int LockedTargetGridX { get; init; }
    public int LockedTargetGridY { get; init; }
    public int LockedTargetGridHeight { get; init; }
    public double AcceptedAtSeconds { get; init; }
}
