using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

internal sealed class WorldSiteLivePlacementSnapshot
{
    public string PlacementId { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public int UnitIndex { get; set; }
    public BattleFaction Faction { get; set; } = BattleFaction.Neutral;
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}

internal sealed class WorldFacilitySlotRuntimeLayout
{
    public string SlotId { get; set; } = "";
    public GridPosition SortCell { get; set; }
    public GridSurfacePosition SortSurface { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public int ZIndex { get; set; }
    public string SourcePath { get; set; } = "";
    public List<GridPosition> FootprintCells { get; } = new();
}

internal sealed class BattlePreparationPlacementDragContext
{
    public string PlacementId { get; set; } = "";
    public string ForceId { get; set; } = "";
    public int ForceIndex { get; set; } = -1;
    public string UnitTypeId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public BattleFaction FallbackFaction { get; set; } = BattleFaction.Neutral;
    public Vector2I FootprintSize { get; set; } = Vector2I.One;
    public bool CanEnterWater { get; set; }
    public BattleForcePlacementRequest RequestPlacement { get; set; }
    public WorldSiteUnitPlacement SitePlacement { get; set; }
}

internal sealed class BattlePreparationCompanyPlacementSnapshot
{
    public BattleForceRequest Force { get; set; }
    public List<BattleForcePlacementRequest> Placements { get; set; } = new();
}

internal sealed class BattlePreparationCompanyPreviewEntity
{
    public BattleForceRequest Force { get; set; }
    public int ForceIndex { get; set; }
    public BattleEntity Entity { get; set; }
}
