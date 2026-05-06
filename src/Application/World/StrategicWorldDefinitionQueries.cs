using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.World;

namespace Rpg.Application.World;

public sealed class StrategicWorldDefinitionQueries
{
    private readonly Dictionary<string, ResourceDefinition> _resources;
    private readonly Dictionary<string, FacilityDefinition> _facilities;
    private readonly Dictionary<string, WorldSiteDefinition> _sites;
    private readonly Dictionary<string, WorldActionDefinition> _actions;
    private readonly Dictionary<string, ThreatRuleDefinition> _threatRules;

    public StrategicWorldDefinitionQueries(StrategicWorldDefinition definition)
    {
        Definition = definition;
        _resources = definition.ResourceDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _facilities = definition.FacilityDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _sites = definition.SiteDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _actions = definition.ActionDefinitions.Where(item => item != null).ToDictionary(item => item.Id, item => item);
        _threatRules = definition.ThreatRules.Where(item => item != null).ToDictionary(item => item.Id, item => item);
    }

    public StrategicWorldDefinition Definition { get; }
    public IReadOnlyDictionary<string, ResourceDefinition> Resources => _resources;
    public IReadOnlyDictionary<string, FacilityDefinition> Facilities => _facilities;
    public IReadOnlyDictionary<string, WorldSiteDefinition> Sites => _sites;
    public IReadOnlyDictionary<string, WorldActionDefinition> Actions => _actions;
    public IReadOnlyDictionary<string, ThreatRuleDefinition> ThreatRules => _threatRules;

    public ResourceDefinition GetResource(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _resources.TryGetValue(id, out ResourceDefinition value) ? value : null;
    }

    public FacilityDefinition GetFacility(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _facilities.TryGetValue(id, out FacilityDefinition value) ? value : null;
    }

    public WorldSiteDefinition GetSite(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _sites.TryGetValue(id, out WorldSiteDefinition value) ? value : null;
    }

    public WorldActionDefinition GetAction(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _actions.TryGetValue(id, out WorldActionDefinition value) ? value : null;
    }

    public ThreatRuleDefinition GetThreatRule(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _threatRules.TryGetValue(id, out ThreatRuleDefinition value) ? value : null;
    }
}
