using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class FactionCapabilityDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, int> Values { get; set; } = new();
}
