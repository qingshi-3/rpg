using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class FactionDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public List<FactionCapabilityDefinition> Capabilities { get; set; } = new();
}
