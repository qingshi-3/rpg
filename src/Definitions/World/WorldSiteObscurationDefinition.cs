using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class WorldSiteObscurationDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool HidesTacticalLayout { get; set; } = true;
    public bool HidesEntrances { get; set; }
    public bool HidesGarrisonDetails { get; set; }
    public List<string> DisabledByResolvedPointIds { get; set; } = new();
    public List<string> DisabledByActiveTags { get; set; } = new();
}
