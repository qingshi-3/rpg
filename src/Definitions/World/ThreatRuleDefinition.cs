using System.Collections.Generic;
using Rpg.Domain.World;

namespace Rpg.Definitions.World;

public sealed class ThreatRuleDefinition
{
    public string Id { get; set; } = "";
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public ThreatType ThreatType { get; set; } = ThreatType.Raid;
    public List<WorldConditionDefinition> TriggerConditions { get; set; } = new();
    public int InitialCountdownTicks { get; set; } = 3;
    public string EnemyGroupId { get; set; } = "";
    public List<GarrisonDefinition> EnemyForces { get; set; } = new();
    public List<WorldEffectDefinition> ConsequenceEffects { get; set; } = new();
}
