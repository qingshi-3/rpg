using System;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static partial class TargetBattleEventOrderGoldenRegressionCases
{
    internal static void RegisterTd002SliceCGoldens(Action<string, Action> run)
    {
        run("runtime td002 region refresh golden locks engagement before local region", RuntimeTd002RegionRefreshGoldenLocksEngagementBeforeLocalRegion);
        run("runtime td002 temporary region golden locks selection before movement", RuntimeTd002TemporaryRegionGoldenLocksSelectionBeforeMovement);
        run("runtime td002 engagement enter exit golden locks perception transition order", RuntimeTd002EngagementEnterExitGoldenLocksPerceptionTransitionOrder);
        run("runtime td002 engagement exit golden clears only exited group target locks", RuntimeTd002EngagementExitGoldenClearsOnlyExitedGroupTargetLocks);
        run("runtime td002 post attack hold defense golden locks attacked branch before movement", RuntimeTd002PostAttackHoldDefenseGoldenLocksAttackedBranchBeforeMovement);
    }

    internal static void RuntimeTd002RegionRefreshGoldenLocksEngagementBeforeLocalRegion()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildTd002RegionRefreshGoldenSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_td002_region_refresh_golden:tick_0:group_td002_enemy_region:engagement:engagement_enter_group_perception",
            "battle_td002_region_refresh_golden:group_td002_enemy_region:group_td002_enemy_region:local:0:0:tick_0:local_region_built_perception_single:1",
            "battle_td002_region_refresh_golden:tick_0:td002_enemy_region:1:move_start",
            "battle_td002_region_refresh_golden:tick_0:group_td002_enemy_region:plan:MovingToAttackSlot"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupEngagementStateChanged:->:engagement_enter_group_perception",
            "-1:BattleGroupLocalCombatRegionChanged:->:local_region_built_perception_single",
            "0:MovementStarted:td002_enemy_region:1->td002_player_region:1:join_recent_damage",
            "0:BattleGroupPlanStateChanged:->:moving_to_attack_slot"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "td002 region refresh event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "td002 region refresh stable projection order golden");
        AssertTrue(
            IndexOfKind(tick.Events, BattleEventKind.BattleGroupEngagementStateChanged) <
            IndexOfKind(tick.Events, BattleEventKind.BattleGroupLocalCombatRegionChanged),
            "engagement enter should be appended before local combat region refresh");
    }

    internal static void RuntimeTd002TemporaryRegionGoldenLocksSelectionBeforeMovement()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildTd002TemporaryRegionGoldenSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_td002_temporary_region_golden:group_td002_enemy_temp:group_td002_enemy_temp:temporary:10:0:td002_player_temp_1:temporary_region_created_cluster:2",
            "battle_td002_temporary_region_golden:tick_0:td002_enemy_temp:1:move_start",
            "battle_td002_temporary_region_golden:tick_0:group_td002_enemy_temp:plan:AdvancingToObjective"
        };
        string[] expectedStableProjection =
        {
            "-1:BattleGroupTemporaryRegionSelected:->:temporary_region_created_cluster",
            "0:MovementStarted:td002_enemy_temp:1->group_td002_enemy_temp:temporary:10:0:td002_player_temp_1:region_temporary_advance",
            "0:BattleGroupPlanStateChanged:->:region_temporary_advance"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "td002 temporary region event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "td002 temporary region stable projection order golden");
        AssertTrue(
            IndexOfKind(tick.Events, BattleEventKind.BattleGroupTemporaryRegionSelected) <
            IndexOfKind(tick.Events, BattleEventKind.MovementStarted),
            "temporary region selection should be appended before region movement starts");
    }

    internal static void RuntimeTd002EngagementEnterExitGoldenLocksPerceptionTransitionOrder()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTd002EngagementEnterExitGoldenSnapshot());

        BattleRuntimeAdvanceResult enterTick = controller.AdvanceNextTick();
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "td002_player_exit:1");
        player.GridX = 20;
        player.GridY = 0;

        BattleRuntimeAdvanceResult exitTick = controller.AdvanceNextTick();
        BattleEvent[] engagementEvents = enterTick.Events
            .Concat(exitTick.Events)
            .Where(item => item.Kind == BattleEventKind.BattleGroupEngagementStateChanged)
            .ToArray();

        string[] expectedEventIds =
        {
            "battle_td002_engagement_enter_exit_golden:tick_0:group_td002_enemy_exit:engagement:engagement_enter_group_perception",
            "battle_td002_engagement_enter_exit_golden:tick_1:group_td002_enemy_exit:engagement:engagement_exit_no_group_perception"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupEngagementStateChanged:->:engagement_enter_group_perception",
            "1:BattleGroupEngagementStateChanged:->:engagement_exit_no_group_perception"
        };

        AssertSequence(expectedEventIds, engagementEvents.Select(item => item.EventId).ToArray(), "td002 engagement enter exit event id order golden");
        AssertSequence(expectedStableProjection, engagementEvents.Select(ToStableProjection).ToArray(), "td002 engagement enter exit stable projection order golden");
    }

    internal static void RuntimeTd002EngagementExitGoldenClearsOnlyExitedGroupTargetLocks()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTd002EngagementExitTargetLockGoldenSnapshot());

        controller.AdvanceNextTick();
        BattleRuntimeActor playerExit = controller.State.Actors.Single(item => item.ActorId == "td002_player_exit:1");
        playerExit.GridX = 30;
        playerExit.GridY = 0;

        BattleRuntimeAdvanceResult exitTick = controller.AdvanceNextTick();
        BattleRuntimeActor exitedEnemy = controller.State.Actors.Single(item => item.ActorId == "td002_enemy_exit:1");
        BattleRuntimeActor keptEnemy = controller.State.Actors.Single(item => item.ActorId == "td002_enemy_keep:1");

        string[] expectedEventIds =
        {
            "battle_td002_engagement_exit_target_lock_golden:tick_1:td002_enemy_exit:1:move_complete",
            "battle_td002_engagement_exit_target_lock_golden:tick_1:td002_enemy_keep:1:move_complete",
            "battle_td002_engagement_exit_target_lock_golden:tick_1:group_td002_enemy_exit:engagement:engagement_exit_no_group_perception",
            "battle_td002_engagement_exit_target_lock_golden:group_td002_enemy_keep:group_td002_enemy_keep:local:11:0:tick_1:local_region_built_perception_single:4",
            "battle_td002_engagement_exit_target_lock_golden:tick_1:td002_enemy_keep:1:move_start"
        };
        string[] expectedStableProjection =
        {
            "1:MovementCompleted:td002_enemy_exit:1->td002_player_exit:1:movement_committed",
            "1:MovementCompleted:td002_enemy_keep:1->td002_player_keep:1:movement_committed",
            "1:BattleGroupEngagementStateChanged:->:engagement_exit_no_group_perception",
            "-1:BattleGroupLocalCombatRegionChanged:->:local_region_built_perception_single",
            "1:MovementStarted:td002_enemy_keep:1->td002_player_keep:1:join_recent_damage"
        };

        AssertSequence(expectedEventIds, exitTick.Events.Select(item => item.EventId).ToArray(), "td002 engagement exit target lock event id order golden");
        AssertSequence(expectedStableProjection, exitTick.Events.Select(ToStableProjection).ToArray(), "td002 engagement exit target lock stable projection order golden");
        AssertTrue(exitedEnemy.TargetActorId == "", "engagement exit should clear the exited group's stale target lock");
        AssertTrue(keptEnemy.TargetActorId == "td002_player_keep:1", "engagement exit should not clear another engaged group's target lock");
    }

    internal static void RuntimeTd002PostAttackHoldDefenseGoldenLocksAttackedBranchBeforeMovement()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildTd002PostAttackHoldDefenseAttackedGoldenSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_td002_hold_defense_attacked_golden:tick_0:td002_enemy_hold_attack:1:attack:td002_player_attack_target:1",
            "battle_td002_hold_defense_attacked_golden:tick_0:group_td002_enemy_hold_attack:engagement:engagement_enter_member_attacked",
            "battle_td002_hold_defense_attacked_golden:tick_0:td002_player_mover:1:move_start",
            "battle_td002_hold_defense_attacked_golden:tick_0:group_td002_enemy_hold_attack:plan:Attacking",
            "battle_td002_hold_defense_attacked_golden:tick_0:group_td002_player_mover:plan:MovingToAttackSlot"
        };
        string[] expectedStableProjection =
        {
            "0:DamageApplied:td002_enemy_hold_attack:1->td002_player_attack_target:1:auto_attack",
            "0:BattleGroupEngagementStateChanged:td002_enemy_hold_attack:1->td002_player_attack_target:1:engagement_enter_member_attacked",
            "0:MovementStarted:td002_player_mover:1->td002_enemy_live:1:auto_advance",
            "0:BattleGroupPlanStateChanged:->:attacking",
            "0:BattleGroupPlanStateChanged:->:moving_to_attack_slot"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "td002 post attack hold defense event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "td002 post attack hold defense stable projection order golden");
        AssertTrue(
            tick.Events.Any(item =>
                item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
                item.ReasonCode == BattleGroupTacticalReasonCode.EngagementEnterMemberAttacked),
            "hold-defense activation should use the attack-trigger branch");
        AssertTrue(
            IndexOfKind(tick.Events, BattleEventKind.BattleGroupEngagementStateChanged) <
            IndexOfKind(tick.Events, BattleEventKind.MovementStarted),
            "post-attack engagement trigger should be appended before movement starts");
    }

    private static BattleStartSnapshot BuildTd002RegionRefreshGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_region_refresh_golden",
            BattleId = "battle_td002_region_refresh_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup("group_td002_enemy_region", "enemy", "td002_enemy_region", 0, 0, hitPoints: 80, damage: 1, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGoldenGroup("group_td002_player_region", "player", "td002_player_region", 3, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 3, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTd002TemporaryRegionGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_temporary_region_golden",
            BattleId = "battle_td002_temporary_region_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup(
                    "group_td002_enemy_temp",
                    "enemy",
                    "td002_enemy_temp",
                    0,
                    0,
                    hitPoints: 80,
                    damage: 1,
                    tacticalMode: BattleGroupTacticalMode.EnemyOffense,
                    initialRegion: BuildGoldenRegion("td002_empty_fixed_region", "group_td002_enemy_temp", BattleTacticalRegionKind.FixedTarget, 0, 0)),
                BuildGoldenGroup("group_td002_player_temp", "player", "td002_player_temp", 10, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine")
            }
        };

        snapshot.BattleGroups.Single(item => item.BattleGroupId == "group_td002_enemy_temp").TacticalIntentPlan = new BattleTacticalIntentPlanSnapshot
        {
            IntentId = BattleTacticalIntentIds.AssaultTarget,
            PrimaryTargetSelector = BattleTargetSelectors.RuntimeObservedHostileCluster,
            RetargetPolicyId = BattleRetargetPolicyIds.AllowVolatileObservation
        };
        AddRectSurfaces(snapshot, 0, 0, 10, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTd002EngagementEnterExitGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_engagement_enter_exit_golden",
            BattleId = "battle_td002_engagement_enter_exit_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup(
                    "group_td002_enemy_exit",
                    "enemy",
                    "td002_enemy_exit",
                    0,
                    0,
                    hitPoints: 80,
                    damage: 1,
                    tacticalMode: BattleGroupTacticalMode.EnemyOffense,
                    initialRegion: BuildGoldenRegion("td002_enemy_exit_fixed", "group_td002_enemy_exit", BattleTacticalRegionKind.FixedTarget, 20, 0)),
                BuildGoldenGroup("group_td002_player_exit", "player", "td002_player_exit", 3, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 20, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTd002EngagementExitTargetLockGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_engagement_exit_target_lock_golden",
            BattleId = "battle_td002_engagement_exit_target_lock_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup(
                    "group_td002_enemy_exit",
                    "enemy",
                    "td002_enemy_exit",
                    0,
                    0,
                    hitPoints: 80,
                    damage: 1,
                    tacticalMode: BattleGroupTacticalMode.EnemyOffense,
                    initialRegion: BuildGoldenRegion("td002_enemy_exit_fixed", "group_td002_enemy_exit", BattleTacticalRegionKind.FixedTarget, 30, 0)),
                BuildGoldenGroup("group_td002_player_exit", "player", "td002_player_exit", 3, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine"),
                BuildGoldenGroup("group_td002_enemy_keep", "enemy", "td002_enemy_keep", 10, 0, hitPoints: 80, damage: 1, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGoldenGroup("group_td002_player_keep", "player", "td002_player_keep", 13, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 30, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTd002PostAttackHoldDefenseAttackedGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_td002_hold_defense_attacked_golden",
            BattleId = "battle_td002_hold_defense_attacked_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup(
                    "group_td002_enemy_hold_attack",
                    "enemy",
                    "td002_enemy_hold_attack",
                    0,
                    0,
                    hitPoints: 80,
                    damage: 1,
                    attackRange: 6,
                    initialCommandId: "HoldLine",
                    tacticalMode: BattleGroupTacticalMode.EnemyHoldDefense,
                    initialRegion: BuildGoldenRegion("td002_enemy_hold_seed", "group_td002_enemy_hold_attack", BattleTacticalRegionKind.Hold, 0, 0)),
                BuildGoldenGroup("group_td002_player_attack_target", "player", "td002_player_attack_target", 6, 0, hitPoints: 80, damage: 1, initialCommandId: "HoldLine"),
                BuildGoldenGroup("group_td002_player_mover", "player", "td002_player_mover", 0, 8, hitPoints: 80, damage: 1),
                BuildGoldenGroup("group_td002_enemy_live", "enemy", "td002_enemy_live", 4, 8, hitPoints: 80, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 6, 0);
        AddRectSurfaces(snapshot, 0, 8, 4, 8);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleTacticalRegionSnapshot BuildGoldenRegion(
        string regionId,
        string ownerBattleGroupId,
        BattleTacticalRegionKind kind,
        int centerX,
        int centerY)
    {
        return new BattleTacticalRegionSnapshot
        {
            RegionId = regionId,
            OwnerBattleGroupId = ownerBattleGroupId,
            Kind = kind,
            CenterCellX = centerX,
            CenterCellY = centerY,
            CenterCellHeight = 0,
            Width = 1,
            Height = 1
        };
    }

}
