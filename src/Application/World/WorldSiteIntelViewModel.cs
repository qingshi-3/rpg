using System.Collections.Generic;
using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteIntelViewModel
{
    public string SiteId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public WorldIntelVisibility Visibility { get; set; } = WorldIntelVisibility.Unknown;
    public bool IsStale { get; set; }
    public int LastSeenWorldTick { get; set; }
    public WorldSiteIntelPolicy Policy { get; set; } = WorldSiteIntelPolicy.Transparent;
    public bool CanInspectStrategicSummary { get; set; }
    public bool CanInspectSiteMap { get; set; }
    public bool CanInspectFullTacticalLayout { get; set; }
    public string StrategicSummary { get; set; } = "";
    public string TacticalSummary { get; set; } = "";
    public string HiddenTacticalSummary { get; set; } = "";
    public List<string> KnownEntranceIds { get; set; } = new();
    public List<string> KnownExplorationPointIds { get; set; } = new();
    public List<string> UnknownIntelReasons { get; set; } = new();
    public List<string> ActiveObscurationSourceIds { get; set; } = new();
    public List<string> KnownTacticalTags { get; set; } = new();
    public List<string> ExplorationAdvantageTags { get; set; } = new();
    public List<WorldSiteApproachViewModel> AvailableApproaches { get; set; } = new();
}

public sealed class WorldSiteApproachViewModel
{
    public string ActionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRecommended { get; set; }
}
