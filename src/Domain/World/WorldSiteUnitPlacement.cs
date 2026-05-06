namespace Rpg.Domain.World;

public sealed class WorldSiteUnitPlacement
{
    public string PlacementId { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public int UnitIndex { get; set; }
    public string ZoneId { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}
