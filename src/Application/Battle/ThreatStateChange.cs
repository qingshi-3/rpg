using Rpg.Domain.World;

namespace Rpg.Application.Battle;

public sealed class ThreatStateChange
{
    public string ThreatId { get; set; } = "";
    public ThreatStage? Stage { get; set; }
}
