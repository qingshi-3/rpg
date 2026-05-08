using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class OpportunitySpawnRuleDefinition
{
    public string Id { get; set; } = "";
    public string PoolId { get; set; } = "";
    public int MinWorldTick { get; set; } = 1;
    public int CheckIntervalTicks { get; set; } = 2;
    public int SpawnChancePermille { get; set; } = 450;
    public int CooldownTicks { get; set; } = 3;
    public int MaxActiveCount { get; set; } = 2;
    public float PositionJitterRadius { get; set; } = 36.0f;
    public List<string> SpawnPointIds { get; set; } = new();
}
