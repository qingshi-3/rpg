using System.Collections.Generic;

namespace Rpg.Application.World;

public sealed class WorldTickResult
{
    public int WorldTick { get; set; }
    public List<GameEvent> Events { get; set; } = new();
    public List<string> Messages { get; set; } = new();
    public List<string> AttackingThreatIds { get; set; } = new();
}
