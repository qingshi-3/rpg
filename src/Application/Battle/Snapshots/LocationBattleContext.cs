using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class LocationBattleContext
{
    public string LocationId { get; set; } = "";
    public List<string> ActiveFacilityIds { get; set; } = new();
    public List<string> ActiveTags { get; set; } = new();
}
