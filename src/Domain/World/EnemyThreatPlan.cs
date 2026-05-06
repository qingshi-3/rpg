namespace Rpg.Domain.World;

public sealed class EnemyThreatPlan
{
    public string Id { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public string WorldArmyId { get; set; } = "";
    public ThreatType ThreatType { get; set; } = ThreatType.Raid;
    public ThreatStage Stage { get; set; } = ThreatStage.Marching;
    public int InitialCountdownTicks { get; set; }
    public int CountdownTicks { get; set; }
    public string EnemyGroupId { get; set; } = "";
    public int VisibleIntelLevel { get; set; }
    public int CreatedTick { get; set; }
}
