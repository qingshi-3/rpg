using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class WorldSiteState
{
    public string SiteId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public SiteControlState ControlState { get; set; } = SiteControlState.Unknown;
    public WorldSiteMode SiteMode { get; set; } = WorldSiteMode.Peacetime;
    public int DamageLevel { get; set; }
    public ResourceStore LocalResources { get; set; } = new();
    public List<FacilityInstance> Facilities { get; set; } = new();
    public List<GarrisonState> Garrison { get; set; } = new();
    public List<WorldSiteUnitPlacement> UnitPlacements { get; set; } = new();
    public List<string> ActiveTags { get; set; } = new();
    public int LastVisitedTick { get; set; }
    public int LastModeChangedTick { get; set; }
}
