namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleObjectiveZoneSnapshot
{
    public string ObjectiveZoneId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ObjectiveRole { get; set; } = "";
    public string DeploymentSide { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Priority { get; set; }
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
}

