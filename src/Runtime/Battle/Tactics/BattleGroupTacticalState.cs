using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalState
{
    public string BattleGroupId { get; set; } = "";
    public BattleGroupTacticalMode TacticalMode { get; set; } = BattleGroupTacticalMode.PlayerCommanded;
    public BattleGroupEngagementState EngagementState { get; set; } = BattleGroupEngagementState.NotEngaged;
    public BattleTacticalRegionSnapshot SelectedRegion { get; set; }
    public BattleTacticalRegionSnapshot LocalCombatRegion { get; set; }
    public int Version { get; set; }
    public int LastTemporaryRegionRefreshTick { get; set; } = -1;
    public int NoPerceivedHostileTicks { get; set; }
    // Damage and attack triggers keep engagement active long enough for group-level
    // policy to observe the contact instead of immediately returning to region movement.
    public int LastMemberDamageTriggerTick { get; set; } = -1;
    public int LastMemberAttackTriggerTick { get; set; } = -1;
}
