using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private sealed class SiteVisualFootprint
    {
        public SiteVisualFootprint(string siteId, HashSet<Vector2I> cells, Rect2 mapBounds)
        {
            SiteId = siteId;
            Cells = cells;
            MapBounds = mapBounds;
        }

        public string SiteId { get; }
        public HashSet<Vector2I> Cells { get; }
        public Rect2 MapBounds { get; }
    }

    private sealed class PendingBattleLaunchRollback
    {
        public string SiteId { get; set; } = "";
        public string WorldArmyId { get; set; } = "";
        public string StrategicExpeditionId { get; set; } = "";
        public bool HasPreviousSiteMode { get; set; }
        public WorldSiteMode PreviousSiteMode { get; set; } = WorldSiteMode.Peacetime;
        public bool PreviousWorldClockPaused { get; set; }
    }

    private enum StrategicRuntimeStage
    {
        Bootstrapping,
        WaitingForNavigation,
        Active
    }
}
