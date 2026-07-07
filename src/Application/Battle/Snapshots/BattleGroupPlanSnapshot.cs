namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleGroupPlanSnapshot
{
    public string BattleGroupId { get; set; } = "";
    public string ObjectiveZoneId { get; set; } = "";
    public BattleEngagementRule EngagementRule { get; set; } = BattleEngagementRule.AttackFirst;
    public string InitialFormationId { get; set; } = "";
    public bool HasObjectiveAnchor { get; set; }
    public int ObjectiveCellX { get; set; }
    public int ObjectiveCellY { get; set; }
    public int ObjectiveCellHeight { get; set; }
    public int ObjectiveWidth { get; set; } = 1;
    public int ObjectiveHeight { get; set; } = 1;
    public bool HasInitialDestinationBeacon { get; set; }
    public int InitialDestinationCellX { get; set; }
    public int InitialDestinationCellY { get; set; }
    public int InitialDestinationCellHeight { get; set; }
}
