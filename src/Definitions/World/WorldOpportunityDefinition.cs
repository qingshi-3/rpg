using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class WorldOpportunityDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string PoolId { get; set; } = "";
    public int Weight { get; set; } = 1;
    public int DurationTicks { get; set; } = 4;
    public string CompletionText { get; set; } = "";
    public List<ResourceAmountDefinition> CompletionRewards { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
