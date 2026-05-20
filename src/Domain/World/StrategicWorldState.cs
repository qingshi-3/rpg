using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class StrategicWorldState
{
    public string RunId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public int Seed { get; set; }
    public int WorldTick { get; set; }
    public string PlayerFactionId { get; set; } = "player";
    public StrategicWorldFogState Fog { get; set; } = new();
    public ResourceStore PlayerResources { get; set; } = new();
    public Dictionary<string, WorldSiteState> SiteStates { get; set; } = new();
    public Dictionary<string, WorldArmyState> ArmyStates { get; set; } = new();
    public Dictionary<string, WorldOpportunityState> OpportunityStates { get; set; } = new();
    public Dictionary<string, int> OpportunityRuleCooldowns { get; set; } = new();
    public List<string> CompletedEventIds { get; set; } = new();
    public List<string> Flags { get; set; } = new();
}
