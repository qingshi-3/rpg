using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class StrategicWorldDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string StartingSiteId { get; set; } = "";
    public string PlayerFactionId { get; set; } = "player";
    public List<string> EnemyFactionIds { get; set; } = new();
    public List<FactionDefinition> FactionDefinitions { get; set; } = new();
    public List<ResourceDefinition> ResourceDefinitions { get; set; } = new();
    public List<WorldSiteDefinition> SiteDefinitions { get; set; } = new();
    public List<WorldOpportunityDefinition> OpportunityDefinitions { get; set; } = new();
    public List<OpportunitySpawnPointDefinition> OpportunitySpawnPoints { get; set; } = new();
    public List<OpportunitySpawnRuleDefinition> OpportunitySpawnRules { get; set; } = new();
    public List<WorldActionDefinition> ActionDefinitions { get; set; } = new();
    public List<ResourceAmountDefinition> InitialResources { get; set; } = new();
}
