using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleGroupEngagementRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("enemy group enters engagement when any member perceives player", EnemyGroupEntersEngagementWhenAnyMemberPerceivesPlayer);
        run("hold defense activates whole group on perception", HoldDefenseActivatesWholeGroupOnPerception);
        run("hold defense activates whole group on damage", HoldDefenseActivatesWholeGroupOnDamage);
        run("hold defense activates whole group on attack", HoldDefenseActivatesWholeGroupOnAttack);
        run("enemy group stays engaged while any member perceives player", EnemyGroupStaysEngagedWhileAnyMemberPerceivesPlayer);
        run("enemy group stays engaged during active combat action without perception", EnemyGroupStaysEngagedDuringActiveCombatActionWithoutPerception);
        run("enemy group exits engagement when whole group loses perception", EnemyGroupExitsEngagementWhenWholeGroupLosesPerception);
    }

    private static void EnemyGroupEntersEngagementWhenAnyMemberPerceivesPlayer()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildGroupPerceptionSnapshot())
            .AdvanceNextTick();

        BattleEvent? engagement = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group");

        AssertTrue(engagement != null, "perceived player should emit group engagement event");
        AssertEqual(BattleGroupTacticalReasonCode.EngagementEnterGroupPerception, engagement!.ReasonCode, "engagement reason");
    }

    private static void HoldDefenseActivatesWholeGroupOnPerception()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildHoldDefensePerceptionSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? engagement = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group");

        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "hold perception should engage the group");
        AssertEqual(BattleGroupTacticalMode.EnemyActiveDefense, state.TacticalMode, "activated hold defense should become active assault policy");
        AssertEqual(BattleGroupTacticalReasonCode.EngagementEnterGroupPerception, engagement?.ReasonCode, "hold activation reason");
    }

    private static void HoldDefenseActivatesWholeGroupOnDamage()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildHoldDefenseDamageSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? engagement = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group");

        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "damaged hold defender should engage the group");
        AssertEqual(BattleGroupTacticalMode.EnemyActiveDefense, state.TacticalMode, "damaged hold defense should become active assault policy");
        AssertEqual(BattleGroupTacticalReasonCode.EngagementEnterMemberDamaged, engagement?.ReasonCode, "damage activation reason");
    }

    private static void HoldDefenseActivatesWholeGroupOnAttack()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildHoldDefenseAttackSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? engagement = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group");

        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "attacking hold defender should engage the group");
        AssertEqual(BattleGroupTacticalMode.EnemyActiveDefense, state.TacticalMode, "attacking hold defense should become active assault policy");
        AssertEqual(BattleGroupTacticalReasonCode.EngagementEnterMemberAttacked, engagement?.ReasonCode, "attack activation reason");
    }

    private static void EnemyGroupExitsEngagementWhenWholeGroupLosesPerception()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildEngagementExitSnapshot());

        controller.AdvanceNextTick();
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        player.GridX = 20;
        player.GridY = 0;

        BattleRuntimeAdvanceResult exitTick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_force:1");
        BattleEvent? exit = exitTick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group" &&
            item.ReasonCode == BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception);

        AssertEqual(BattleGroupEngagementState.NotEngaged, state.EngagementState, "whole-group perception loss should exit engagement");
        AssertEqual("", enemy.TargetActorId, "exiting engagement clears stale target lock");
        AssertTrue(exit != null, "exit should emit engagement state event");

        BattleRuntimeAdvanceResult regionTick = controller.AdvanceNextTick();
        BattleEvent? regionMove = regionTick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1" &&
            item.ReasonCode == BattleGroupTacticalReasonCode.RegionFixedAdvance);

        AssertTrue(regionMove != null, "exited enemy group should resume region movement");
    }

    private static void EnemyGroupStaysEngagedWhileAnyMemberPerceivesPlayer()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildSplitPerceptionSnapshot());

        controller.AdvanceNextTick();
        BattleGroupTacticalState entered = controller.State.TacticalStates["enemy_group"];
        AssertEqual(BattleGroupEngagementState.Engaged, entered.EngagementState, "first member perception should engage group");

        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        player.GridX = 13;
        player.GridY = 0;

        BattleRuntimeAdvanceResult shiftedPerception = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? exit = shiftedPerception.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group" &&
            item.ReasonCode == BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception);

        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "second member perception should keep group engaged");
        AssertTrue(exit == null, "group should not exit while any member still perceives a hostile");
    }

    private static void EnemyGroupStaysEngagedDuringActiveCombatActionWithoutPerception()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildEngagementExitSnapshot());

        controller.AdvanceNextTick();
        BattleGroupTacticalState entered = controller.State.TacticalStates["enemy_group"];
        AssertEqual(BattleGroupEngagementState.Engaged, entered.EngagementState, "fixture should enter engagement first");

        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_force:1");
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        enemy.TargetActorId = player.ActorId;
        enemy.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;
        enemy.Phase = BattleRuntimeActorPhase.AttackRecovery;
        enemy.ActionReadyAtSeconds = 1000;
        player.GridX = 20;
        player.GridY = 0;

        BattleRuntimeAdvanceResult shiftedPerception = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? exit = shiftedPerception.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
            item.BattleGroupId == "enemy_group" &&
            item.ReasonCode == BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception);

        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "active combat action should bridge a short perception gap");
        AssertEqual(player.ActorId, enemy.TargetActorId, "engagement retention should not clear the live target lock");
        AssertTrue(exit == null, "group should not emit no-perception exit while a member is still executing combat");
    }

    private static BattleStartSnapshot BuildGroupPerceptionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_group_engagement",
            BattleId = "battle_group_engagement",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy_force", "enemy", BattleGroupTacticalMode.EnemyOffense, 0, 0),
                BuildGroup("player_group", "player_force", "player", BattleGroupTacticalMode.PlayerCommanded, 3, 0)
            }
        };

        for (int x = 0; x <= 5; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSplitPerceptionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_split_perception",
            BattleId = "battle_split_perception",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy_force_a", "enemy", BattleGroupTacticalMode.EnemyOffense, 0, 0),
                BuildGroup("enemy_group", "enemy_force_b", "enemy", BattleGroupTacticalMode.EnemyOffense, 10, 0),
                BuildGroup("player_group", "player_force", "player", BattleGroupTacticalMode.PlayerCommanded, 3, 0)
            }
        };

        for (int x = 0; x <= 13; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildHoldDefensePerceptionSnapshot()
    {
        BattleStartSnapshot snapshot = BuildGroupPerceptionSnapshot();
        BattleGroupSnapshot enemy = snapshot.BattleGroups.Single(item => item.BattleGroupId == "enemy_group");
        enemy.TacticalMode = BattleGroupTacticalMode.EnemyHoldDefense;
        enemy.InitialCorpsCommandId = "HoldLine";
        enemy.InitialTacticalRegions.Add(BuildRegion("enemy_hold_seed", "enemy_group", BattleTacticalRegionKind.Hold));
        return snapshot;
    }

    private static BattleStartSnapshot BuildHoldDefenseDamageSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_hold_damage",
            BattleId = "battle_hold_damage",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    "enemy_force",
                    "enemy",
                    BattleGroupTacticalMode.EnemyHoldDefense,
                    0,
                    0,
                    BuildRegion("enemy_hold_seed", "enemy_group", BattleTacticalRegionKind.Hold),
                    initialCommandId: "HoldLine"),
                BuildGroup(
                    "player_group",
                    "player_force",
                    "player",
                    BattleGroupTacticalMode.PlayerCommanded,
                    6,
                    0,
                    attackRange: 6,
                    attackDamage: 3)
            }
        };

        for (int x = 0; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildHoldDefenseAttackSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_hold_attack",
            BattleId = "battle_hold_attack",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    "enemy_force",
                    "enemy",
                    BattleGroupTacticalMode.EnemyHoldDefense,
                    0,
                    0,
                    BuildRegion("enemy_hold_seed", "enemy_group", BattleTacticalRegionKind.Hold),
                    initialCommandId: "HoldLine",
                    attackRange: 6,
                    attackDamage: 3),
                BuildGroup("player_group", "player_force", "player", BattleGroupTacticalMode.PlayerCommanded, 6, 0)
            }
        };

        for (int x = 0; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildEngagementExitSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_engagement_exit",
            BattleId = "battle_engagement_exit",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    "enemy_force",
                    "enemy",
                    BattleGroupTacticalMode.EnemyOffense,
                    0,
                    0,
                    BuildRegion("enemy_fixed_player_fallback", "enemy_group", BattleTacticalRegionKind.FixedTarget, centerX: 20, centerY: 0)),
                BuildGroup("player_group", "player_force", "player", BattleGroupTacticalMode.PlayerCommanded, 3, 0, initialCommandId: "HoldLine")
            }
        };

        for (int y = 0; y <= 5; y++)
        {
            for (int x = 0; x <= 20; x++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string sourceForceId,
        string factionId,
        BattleGroupTacticalMode tacticalMode,
        int x,
        int y,
        BattleTacticalRegionSnapshot? initialRegion = null,
        string initialCommandId = "",
        int attackRange = 1,
        int attackDamage = 1)
    {
        BattleGroupSnapshot group = new()
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{groupId}_hero",
            HeroDefinitionId = $"{groupId}_hero_definition",
            CorpsId = $"{groupId}_corps",
            CorpsDefinitionId = $"{groupId}_corps_definition",
            CorpsStrength = 80,
            MaxHitPoints = 80,
            AttackDamage = attackDamage,
            AttackRange = attackRange,
            SourceLocationId = "site_1",
            CellX = x,
            CellY = y,
            InitialCorpsCommandId = initialCommandId,
            TacticalMode = tacticalMode
        };
        if (initialRegion != null)
        {
            group.InitialTacticalRegions.Add(initialRegion);
        }

        return group;
    }

    private static BattleTacticalRegionSnapshot BuildRegion(
        string regionId,
        string ownerBattleGroupId,
        BattleTacticalRegionKind kind,
        int centerX = 0,
        int centerY = 0)
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

    private static void AddSurface(BattleStartSnapshot snapshot, int x, int y)
    {
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
        {
            X = x,
            Y = y,
            Height = 0,
            MoveCost = 1
        });
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
