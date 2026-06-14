using System.Collections.Generic;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicEvent
{
    public string Kind { get; set; } = "";
    public List<string> TargetIds { get; set; } = new();
    public Dictionary<string, string> Payload { get; set; } = new(System.StringComparer.Ordinal);
}
