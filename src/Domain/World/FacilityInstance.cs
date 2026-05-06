using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class FacilityInstance
{
    public string InstanceId { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string SlotId { get; set; } = "";
    public int Level { get; set; } = 1;
    public FacilityState State { get; set; } = FacilityState.Active;
    public int AssignedPopulation { get; set; }
    public int ProgressTicks { get; set; }
    public List<string> Cooldowns { get; set; } = new();
    public List<string> ActiveTags { get; set; } = new();
}
