using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class StrategicWorldFogState
{
    public List<string> VisibleCells { get; set; } = new();
    public List<string> ExploredCells { get; set; } = new();
    public int LastUpdatedWorldTick { get; set; }
}
