using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.World;

namespace Rpg.Application.World;

public sealed class StrategicWorldDefinitionQueries
{
    private readonly Dictionary<string, ResourceDefinition> _resources;
    private readonly Dictionary<string, FactionDefinition> _factions;
    private readonly Dictionary<string, WorldSiteDefinition> _sites;
    private readonly Dictionary<string, WorldOpportunityDefinition> _opportunities;
    private readonly Dictionary<string, OpportunitySpawnPointDefinition> _opportunitySpawnPoints;
    private readonly Dictionary<string, OpportunitySpawnRuleDefinition> _opportunitySpawnRules;
    private readonly Dictionary<string, WorldActionDefinition> _actions;

    public StrategicWorldDefinitionQueries(StrategicWorldDefinition definition)
    {
        Definition = definition;
        _factions = definition.FactionDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _resources = definition.ResourceDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _sites = definition.SiteDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _opportunities = definition.OpportunityDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _opportunitySpawnPoints = definition.OpportunitySpawnPoints.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _opportunitySpawnRules = definition.OpportunitySpawnRules.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _actions = definition.ActionDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
    }

    public StrategicWorldDefinition Definition { get; }
    public IReadOnlyDictionary<string, FactionDefinition> Factions => _factions;
    public IReadOnlyDictionary<string, ResourceDefinition> Resources => _resources;
    public IReadOnlyDictionary<string, WorldSiteDefinition> Sites => _sites;
    public IReadOnlyDictionary<string, WorldOpportunityDefinition> Opportunities => _opportunities;
    public IReadOnlyDictionary<string, OpportunitySpawnPointDefinition> OpportunitySpawnPoints => _opportunitySpawnPoints;
    public IReadOnlyDictionary<string, OpportunitySpawnRuleDefinition> OpportunitySpawnRules => _opportunitySpawnRules;
    public IReadOnlyDictionary<string, WorldActionDefinition> Actions => _actions;

    public ResourceDefinition GetResource(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _resources.TryGetValue(id, out ResourceDefinition value) ? value : null;
    }

    public FactionDefinition GetFaction(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _factions.TryGetValue(id, out FactionDefinition value) ? value : null;
    }

    public WorldSiteDefinition GetSite(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _sites.TryGetValue(id, out WorldSiteDefinition value) ? value : null;
    }

    public WorldOpportunityDefinition GetOpportunity(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _opportunities.TryGetValue(id, out WorldOpportunityDefinition value) ? value : null;
    }

    public OpportunitySpawnPointDefinition GetOpportunitySpawnPoint(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _opportunitySpawnPoints.TryGetValue(id, out OpportunitySpawnPointDefinition value) ? value : null;
    }

    public OpportunitySpawnRuleDefinition GetOpportunitySpawnRule(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _opportunitySpawnRules.TryGetValue(id, out OpportunitySpawnRuleDefinition value) ? value : null;
    }

    public WorldActionDefinition GetAction(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _actions.TryGetValue(id, out WorldActionDefinition value) ? value : null;
    }

}
