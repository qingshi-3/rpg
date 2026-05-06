using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class WorldActionDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public WorldActionScope Scope { get; set; } = WorldActionScope.Site;
    public bool AdvancesWorldTick { get; set; }
    public List<WorldConditionDefinition> Conditions { get; set; } = new();
    public List<ResourceAmountDefinition> Costs { get; set; } = new();
    public List<WorldEffectDefinition> Effects { get; set; } = new();
    public string FailureReasonKey { get; set; } = "action_unavailable";
}
