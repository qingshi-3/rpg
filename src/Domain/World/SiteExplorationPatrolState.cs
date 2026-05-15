namespace Rpg.Domain.World;

public sealed class SiteExplorationPatrolState
{
    public string PatrolId { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
    public int RouteIndex { get; set; }
    public int ActionPoints { get; set; }
    public bool IsRemoved { get; set; }
}
