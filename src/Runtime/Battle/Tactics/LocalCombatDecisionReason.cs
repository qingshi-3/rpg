namespace Rpg.Runtime.Battle.Tactics;

internal static class LocalCombatDecisionReason
{
    public const string JoinRecentDamage = "join_recent_damage";
    public const string JoinBlocksObjectiveRoute = "join_blocks_objective_route";
    public const string HoldSupportAttackSlotsFull = "hold_support_attack_slots_full";
    public const string ReturnObjectiveThreatClear = "return_objective_threat_clear";
    public const string RejectOutsideLeash = "reject_outside_leash";
    public const string RejectJoinBudgetFull = "reject_join_budget_full";
    public const string RejectNoReachableSlot = "reject_no_reachable_slot";
}
