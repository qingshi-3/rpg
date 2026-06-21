using System.Collections.Generic;

namespace Rpg.Application.World;

public sealed class WorldActionRequest
{
    public string ActionId { get; set; } = "";
    public string ActorFactionId { get; set; } = StrategicWorldIds.FactionPlayer;
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}
