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
    public BattleGroupTacticalMode TacticalMode { get; set; } = BattleGroupTacticalMode.PlayerCommanded;
    public bool AllowPlayerScopedEngagement { get; set; }
    public bool AllowAutonomousFallbackTargeting { get; set; }
    public BattleGroupEngagementState EngagementState { get; set; } = BattleGroupEngagementState.NotEngaged;
    public BattleTacticalRegionSnapshot SelectedRegion { get; set; }
    public BattleGroupTacticalCommandSource SelectedRegionCommandSource { get; set; } = BattleGroupTacticalCommandSource.None;
    public BattleTacticalRegionSnapshot LocalCombatRegion { get; set; }
    public int Version { get; set; }
    public int LastTemporaryRegionRefreshTick { get; set; } = -1;
    public int NoPerceivedHostileTicks { get; set; }
    // Damage and attack triggers keep engagement active long enough for group-level
    // policy to observe the contact instead of immediately returning to region movement.
    public int LastMemberDamageTriggerTick { get; set; } = -1;
    public int LastMemberAttackTriggerTick { get; set; } = -1;
}
