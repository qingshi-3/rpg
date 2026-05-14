using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class SiteAutoGarrisonProductionDefinition
{
    public string FactionId { get; set; } = "";
    public int IntervalTicks { get; set; } = 1;
    public int MaxStoredUnits { get; set; }
    public List<GarrisonDefinition> BatchUnits { get; set; } = new();
}
