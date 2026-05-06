using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class BattleModifierDefinition
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string BattleAnchorId { get; set; } = "";
    public int Uses { get; set; } = 1;
    public Dictionary<string, int> Values { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
