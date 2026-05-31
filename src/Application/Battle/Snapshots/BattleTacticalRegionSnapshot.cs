namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleTacticalRegionSnapshot
{
    public string RegionId { get; set; } = "";
    public string OwnerBattleGroupId { get; set; } = "";
    public BattleTacticalRegionKind Kind { get; set; }
    public string SourceRegionId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int CenterCellX { get; set; }
    public int CenterCellY { get; set; }
    public int CenterCellHeight { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public int Version { get; set; }
}
