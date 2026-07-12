using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

public enum BattleGroupTacticalCommandSource
{
    None = 0,
    PlayerCommand = 1,
    SelfCalculated = 2,
    EnemyPolicy = 3
}

public sealed class BattleGroupTacticalState
{
    public string BattleGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public BattleGroupTacticalMode TacticalMode { get; set; } = BattleGroupTacticalMode.PlayerCommanded;
    public BattleTacticalIntentPlanSnapshot TacticalIntentPlan { get; set; } = new();
    public bool AllowPlayerScopedEngagement { get; set; }
    public bool AllowAutonomousFallbackTargeting { get; set; }
    public BattleGroupEngagementState EngagementState { get; set; } = BattleGroupEngagementState.NotEngaged;
    public BattleTacticalRegionSnapshot SelectedRegion { get; set; }
    public BattleGroupTacticalCommandSource SelectedRegionCommandSource { get; set; } = BattleGroupTacticalCommandSource.None;
    public BattleTacticalRegionSnapshot LocalCombatRegion { get; set; }
    // Commander command, plan, beacon, and objective facts live once here.
    // Actor copies are execution caches and are overwritten at decision boundaries.
    public string ActiveCommandId { get; set; } = "";
    public BattleEngagementRule EngagementRule { get; set; } = BattleEngagementRule.AttackFirst;
    public BattleGroupPlanRuntimeState PlanState { get; set; } = BattleGroupPlanRuntimeState.SensingContact;
    public bool HasObjectiveAnchor { get; set; }
    public string ObjectiveZoneId { get; set; } = "";
    public int ObjectiveGridX { get; set; }
    public int ObjectiveGridY { get; set; }
    public int ObjectiveGridHeight { get; set; }
    public int ObjectiveWidth { get; set; } = 1;
    public int ObjectiveHeight { get; set; } = 1;
    public string ActiveDestinationBeaconId { get; set; } = "";
    public int ActiveDestinationBeaconRevision { get; set; }
    public int ActiveDestinationBeaconGridX { get; set; }
    public int ActiveDestinationBeaconGridY { get; set; }
    public int ActiveDestinationBeaconGridHeight { get; set; }
    public string ActiveDestinationBeaconCommandId { get; set; } = "";
    public int Version { get; set; }
    public int LastTemporaryRegionRefreshTick { get; set; } = -1;
    public int NoPerceivedHostileTicks { get; set; }
    // Damage and attack triggers keep engagement active long enough for group-level
    // policy to observe the contact instead of immediately returning to region movement.
    public int LastMemberDamageTriggerTick { get; set; } = -1;
    public int LastMemberAttackTriggerTick { get; set; } = -1;
}
