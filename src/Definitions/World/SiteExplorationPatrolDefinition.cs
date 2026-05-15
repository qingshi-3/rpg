using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class SiteExplorationPatrolDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public string SourcePlacementId { get; set; } = "";
    public List<SiteExplorationRouteCellDefinition> RouteCells { get; set; } = new();
    public int AlertRadiusCells { get; set; } = 2;
    public int ActionPointRegenPerTick { get; set; } = 1;
    public int MoveCostPerCell { get; set; } = 1;
    public bool InitiallyActive { get; set; } = true;
}

public sealed class SiteExplorationRouteCellDefinition
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}
