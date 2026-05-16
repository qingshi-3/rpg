using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class WorldSiteIntelDefinition
{
    public WorldSiteIntelPolicy Policy { get; set; } = WorldSiteIntelPolicy.Transparent;
    public string StrategicSummary { get; set; } = "";
    public string TacticalSummary { get; set; } = "";
    public string HiddenTacticalSummary { get; set; } = "";
    public List<string> PublicEntranceIds { get; set; } = new();
    public List<string> PublicFacilitySlotIds { get; set; } = new();
    public List<string> PublicExplorationPointIds { get; set; } = new();
    public List<WorldSiteObscurationDefinition> ObscurationSources { get; set; } = new();
}
