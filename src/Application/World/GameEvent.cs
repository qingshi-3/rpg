using System.Collections.Generic;

namespace Rpg.Application.World;

public sealed class GameEvent
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "";
    public string SourceSystem { get; set; } = "World";
    public List<string> TargetIds { get; set; } = new();
    public Dictionary<string, string> Payload { get; set; } = new();
    public int Tick { get; set; }
}
