using System.Collections.Generic;
using Rpg.Domain.World;

namespace Rpg.Application.Battle;

public sealed class SiteStateSnapshot
{
    public string SiteId { get; set; } = "";
    public SiteControlState ControlState { get; set; } = SiteControlState.Unknown;
    public int DamageLevel { get; set; }
    public List<string> ActiveFacilityIds { get; set; } = new();
    public List<string> DamagedFacilityIds { get; set; } = new();
    public Dictionary<string, int> GarrisonSummary { get; set; } = new();
    public List<string> ActiveTags { get; set; } = new();
}
