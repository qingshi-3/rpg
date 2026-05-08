namespace Rpg.Domain.World;

public sealed class WorldSiteUnitPlacement
{
    public string PlacementId { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public int UnitIndex { get; set; }
    public string FactionId { get; set; } = "";
    public WorldSiteUnitPlacementKind PlacementKind { get; set; } = WorldSiteUnitPlacementKind.Garrison;
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ArmyId { get; set; } = "";
    public string ThreatId { get; set; } = "";
    public string ZoneId { get; set; } = "";
    public string EntranceId { get; set; } = "";
    public WorldSiteAttackDirection AttackDirection { get; set; } = WorldSiteAttackDirection.Any;
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}
