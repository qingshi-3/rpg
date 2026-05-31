using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleRegionMovementGoal
{
    public string RegionId { get; init; } = "";
    public string OwnerBattleGroupId { get; init; } = "";
    public BattleTacticalRegionKind Kind { get; init; }
    public int CenterCellX { get; init; }
    public int CenterCellY { get; init; }
    public int CenterCellHeight { get; init; }
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
    public string SourceRegionId { get; init; } = "";
    public string ReasonCode { get; init; } = "";
}
