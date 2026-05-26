namespace Rpg.Presentation.World.Sites;

public sealed class BattleObjectiveMapRegion
{
    public string RegionId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DeploymentSide { get; init; } = "";
    public int Priority { get; init; }
    public int CellX { get; init; }
    public int CellY { get; init; }
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
    public bool Selectable { get; init; }
}
