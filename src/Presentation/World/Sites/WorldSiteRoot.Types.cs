using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private sealed class WorldSiteLivePlacementSnapshot
    {
        public string PlacementId { get; set; } = "";
        public string UnitTypeId { get; set; } = "";
        public int UnitIndex { get; set; }
        public BattleFaction Faction { get; set; } = BattleFaction.Neutral;
        public int CellX { get; set; }
        public int CellY { get; set; }
        public int CellHeight { get; set; }
    }

    private sealed class WorldFacilitySlotRuntimeLayout
    {
        public string SlotId { get; set; } = "";
        public GridPosition SortCell { get; set; }
        public GridSurfacePosition SortSurface { get; set; }
        public int FootprintWidth { get; set; } = 1;
        public int FootprintHeight { get; set; } = 1;
        public int ZIndex { get; set; }
        public List<GridPosition> FootprintCells { get; } = new();
    }
}
