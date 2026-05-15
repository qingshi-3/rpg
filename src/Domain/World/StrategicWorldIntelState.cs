using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Rpg.Domain.World;

public sealed class StrategicWorldIntelState
{
    public List<string> ExploredCells { get; set; } = new();
    public Dictionary<string, WorldSiteIntelSnapshot> KnownSites { get; set; } = new();
    public int LastUpdatedWorldTick { get; set; }

    [JsonIgnore]
    public List<string> VisibleCells { get; set; } = new();
}

public sealed class WorldSiteIntelSnapshot
{
    public string SiteId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int LastSeenWorldTick { get; set; }
    public string OwnerFactionId { get; set; } = "";
    public SiteControlState ControlState { get; set; } = SiteControlState.Unknown;
    public WorldSiteMode SiteMode { get; set; } = WorldSiteMode.Peacetime;
    public int DamageLevel { get; set; }
    public ResourceStore KnownLocalResources { get; set; } = new();
    public List<FacilityInstance> KnownFacilities { get; set; } = new();
    public List<GarrisonState> KnownGarrison { get; set; } = new();
    public List<string> KnownPendingThreatIds { get; set; } = new();
}
