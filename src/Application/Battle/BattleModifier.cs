using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleModifier
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string BattleAnchorId { get; set; } = "";
    public int Uses { get; set; } = 1;
    public Dictionary<string, int> Values { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
