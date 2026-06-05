namespace Rpg.Runtime.Battle.Tactics;

public static class BattleGroupTacticalReasonCode
{
    public const string RegionAccepted = "region_accepted";
    public const string RegionRejectedMissingOwner = "region_rejected_missing_owner";
    public const string RegionRejectedOwnerMismatch = "region_rejected_owner_mismatch";
    public const string RegionRejectedPlayerPolicyOverwrite = "region_rejected_player_policy_overwrite";
    public const string RegionRejectedInvalidSize = "region_rejected_invalid_size";
    public const string RegionRejectedInvalidRegion = "region_rejected_invalid_region";
    public const string RegionRejectedMissingGroup = "region_rejected_missing_group";
    public const string RegionRejectedDuplicateGroup = "region_rejected_duplicate_group";
    public const string RegionFixedSelectedPlayerDensity = "region_fixed_selected_player_density";
    public const string RegionFixedSelectedPriority = "region_fixed_selected_priority";
    public const string RegionHoldSeededPosture = "region_hold_seeded_posture";
    public const string RegionFixedAdvance = "region_fixed_advance";
    public const string RegionTemporaryAdvance = "region_temporary_advance";
    public const string CombatZoneJoinAdvance = "combat_zone_join_advance";
    public const string TemporaryRegionCreatedCluster = "temporary_region_created_cluster";
    public const string TemporaryRegionReusedRefreshInterval = "temporary_region_reused_refresh_interval";
    public const string TemporaryRegionRefreshedIntervalElapsed = "temporary_region_refreshed_interval_elapsed";
    public const string EngagementEnterGroupPerception = "engagement_enter_group_perception";
    public const string EngagementEnterMemberDamaged = "engagement_enter_member_damaged";
    public const string EngagementEnterMemberAttacked = "engagement_enter_member_attacked";
    public const string EngagementExitNoGroupPerception = "engagement_exit_no_group_perception";
    public const string LocalRegionBuiltPerceptionOverlap = "local_region_built_perception_overlap";
    public const string LocalRegionRejectedOverCap = "local_region_rejected_over_cap";
    public const string LocalRegionDegradeNoReachableSlot = "local_region_degrade_no_reachable_slot";
}
