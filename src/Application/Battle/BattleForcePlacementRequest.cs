namespace Rpg.Application.Battle;

public sealed class BattleForcePlacementRequest
{
    public string PlacementId { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}
