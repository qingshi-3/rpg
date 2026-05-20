using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class WorldSiteMemoryState
{
    public List<string> RevealedEntranceIds { get; set; } = new();
    public List<string> ResolvedPointIds { get; set; } = new();
    public List<string> UnlockedFacilitySlotIds { get; set; } = new();
    public List<string> ClearedHazardIds { get; set; } = new();
    public List<string> KnownTacticalTags { get; set; } = new();
}
