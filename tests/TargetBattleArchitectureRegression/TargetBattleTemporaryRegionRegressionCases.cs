using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleTemporaryRegionRegressionCases
{
    private const string TemporaryCreatedCluster = "temporary_region_created_cluster";

    public static void Register(Action<string, Action> run)
    {
        run("default enemy intent keeps stable fixed region when cluster moves", DefaultEnemyIntentKeepsStableFixedRegionWhenClusterMoves);
        run("explicit enemy cluster pursuit intent may use temporary region", ExplicitEnemyClusterPursuitIntentMayUseTemporaryRegion);
        run("temporary region refresh interval defaults to about two seconds", TemporaryRegionRefreshIntervalDefaultsToAboutTwoSeconds);
        run("player active command blocks autonomous temporary region", PlayerActiveCommandBlocksAutonomousTemporaryRegion);
        run("player completed command creates autonomous temporary region", PlayerCompletedCommandCreatesAutonomousTemporaryRegion);
        run("player autonomous temporary region clears on engagement", PlayerAutonomousTemporaryRegionClearsOnEngagement);
    }

    private static void DefaultEnemyIntentKeepsStableFixedRegionWhenClusterMoves()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTemporaryRegionSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? regionMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1");

        AssertEqual(BattleTacticalRegionKind.FixedTarget, state.SelectedRegion?.Kind, "default enemy intent should retain the stable fixed target");
        AssertEqual("empty_fixed_region", state.SelectedRegion?.RegionId, "default enemy intent should not replace fixed target with volatile cluster");
        AssertEqual("enemy_group", state.SelectedRegion?.OwnerBattleGroupId, "fixed region owner");
        AssertEqual(5, state.SelectedRegion?.CenterCellX, "fixed region center x");
        AssertEqual(0, state.SelectedRegion?.CenterCellY, "fixed region center y");
        AssertTrue(regionMove != null, "enemy should keep moving through the fixed region goal");
        AssertEqual("empty_fixed_region", regionMove!.TargetId, "movement should target the retained fixed region");
        AssertEqual(BattleGroupTacticalReasonCode.RegionFixedAdvance, regionMove.ReasonCode, "stable region movement reason");
    }

    private static void ExplicitEnemyClusterPursuitIntentMayUseTemporaryRegion()
    {
        BattleStartSnapshot snapshot = BuildTemporaryRegionSnapshot();
        AllowVolatileClusterPursuit(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(snapshot);

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? regionMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1");

        AssertEqual(BattleTacticalRegionKind.TemporaryTarget, state.SelectedRegion?.Kind, "explicit cluster pursuit may replace the fixed target");
        AssertEqual("enemy_group", state.SelectedRegion?.OwnerBattleGroupId, "temporary region owner");
        AssertEqual(10, state.SelectedRegion?.CenterCellX, "temporary region center x");
        AssertEqual(0, state.SelectedRegion?.CenterCellY, "temporary region center y");
        AssertEqual(TemporaryCreatedCluster, state.SelectedRegion?.ReasonCode, "temporary region creation reason");
        AssertTrue(regionMove != null, "enemy should move toward generated temporary region");
        AssertEqual(BattleGroupTacticalReasonCode.RegionTemporaryAdvance, regionMove!.ReasonCode, "temporary region movement reason");
    }

    private static void TemporaryRegionRefreshIntervalDefaultsToAboutTwoSeconds()
    {
        BattleStartSnapshot snapshot = BuildTemporaryRegionSnapshot(playerCellX: 100, maxSurfaceX: 102);
        AllowVolatileClusterPursuit(snapshot);

        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(snapshot);

        controller.AdvanceNextTick();
        AssertEqual(100, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, "initial temporary center");

        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        player.GridX = 102;
        player.Position = 102;

        const int ExpectedRefreshTicks = 50;
        AssertEqual(ExpectedRefreshTicks, BattleGroupTacticalPolicySettings.DefaultTemporaryRegionRefreshTicks, "temporary region refresh tuning");

        for (int i = 1; i < ExpectedRefreshTicks; i++)
        {
            controller.AdvanceNextTick();
            AssertEqual(100, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, $"temporary region should be reused on tick {i}");
        }

        controller.AdvanceNextTick();
        AssertEqual(102, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, "temporary region may refresh after about two seconds");
    }

    private static void PlayerActiveCommandBlocksAutonomousTemporaryRegion()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildPlayerAutonomousRegionSnapshot(playerStartsAtCommand: false));

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["player_group"];
        BattleEvent? regionMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_force:1");

        AssertEqual("player_command_region", state.SelectedRegion?.RegionId, "active player command should remain selected");
        AssertEqual(BattleTacticalRegionKind.FixedTarget, state.SelectedRegion?.Kind, "active player command remains a fixed target");
        AssertTrue(regionMove != null, "player group should keep advancing toward its active command");
        AssertEqual("player_command_region", regionMove!.TargetId, "movement should target the player command region");
    }

    private static void PlayerCompletedCommandCreatesAutonomousTemporaryRegion()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildPlayerAutonomousRegionSnapshot(playerStartsAtCommand: true));

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["player_group"];
        BattleEvent? regionMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_force:1");

        AssertEqual(BattleTacticalRegionKind.TemporaryTarget, state.SelectedRegion?.Kind, "completed empty player command should switch to autonomous temporary target");
        AssertEqual("player_group", state.SelectedRegion?.OwnerBattleGroupId, "autonomous temporary target owner");
        AssertEqual(22, state.SelectedRegion?.CenterCellX, "autonomous target should prefer the denser enemy cluster before distance");
        AssertEqual(BattleGroupTacticalReasonCode.PlayerAutonomousTemporaryRegionCreatedCluster, state.SelectedRegion?.ReasonCode, "autonomous temporary target reason");
        AssertTrue(regionMove != null, "player group should move toward generated autonomous target");
        AssertEqual(BattleGroupTacticalReasonCode.RegionTemporaryAdvance, regionMove!.ReasonCode, "autonomous temporary movement reason");
    }

    private static void PlayerAutonomousTemporaryRegionClearsOnEngagement()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildPlayerAutonomousRegionSnapshot(playerStartsAtCommand: true));

        controller.AdvanceNextTick();
        AssertEqual(BattleTacticalRegionKind.TemporaryTarget, controller.State.TacticalStates["player_group"].SelectedRegion?.Kind, "autonomous temporary target should be selected first");

        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_near_force:1");
        enemy.GridX = 1;
        enemy.GridY = 0;

        controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["player_group"];
        AssertEqual(BattleGroupEngagementState.Engaged, state.EngagementState, "near hostile should enter player-scoped engagement");
        AssertTrue(state.SelectedRegion == null, "self-calculated target should clear when combat starts");
    }

    private static BattleStartSnapshot BuildTemporaryRegionSnapshot(
        int playerCellX = 10,
        int fixedRegionX = 5,
        int maxSurfaceX = 12)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_temporary_region",
            BattleId = "battle_temporary_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "enemy_group",
                    FactionId = "enemy",
                    SourceForceId = "enemy_force",
                    HeroId = "enemy_hero",
                    HeroDefinitionId = "enemy_hero_definition",
                    CorpsId = "enemy_corps",
                    CorpsDefinitionId = "enemy_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = 0,
                    CellY = 0,
                    TacticalMode = BattleGroupTacticalMode.EnemyOffense,
                    InitialTacticalRegions =
                    {
                        BuildRegion("empty_fixed_region", "enemy_group", BattleTacticalRegionKind.FixedTarget, fixedRegionX, 0)
                    }
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "player_group",
                    FactionId = "player",
                    SourceForceId = "player_force",
                    HeroId = "player_hero",
                    HeroDefinitionId = "player_hero_definition",
                    CorpsId = "player_corps",
                    CorpsDefinitionId = "player_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = playerCellX,
                    CellY = 0,
                    InitialCorpsCommandId = "HoldLine",
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded
                }
            }
        };

        for (int x = 0; x <= maxSurfaceX; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static void AllowVolatileClusterPursuit(BattleStartSnapshot snapshot)
    {
        snapshot.BattleGroups.Single(item => item.BattleGroupId == "enemy_group").TacticalIntentPlan = new BattleTacticalIntentPlanSnapshot
        {
            IntentId = BattleTacticalIntentIds.AssaultTarget,
            PrimaryTargetSelector = BattleTargetSelectors.RuntimeObservedHostileCluster,
            RetargetPolicyId = BattleRetargetPolicyIds.AllowVolatileObservation
        };
    }

    private static BattleStartSnapshot BuildPlayerAutonomousRegionSnapshot(bool playerStartsAtCommand)
    {
        int commandX = playerStartsAtCommand ? 0 : 5;
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = playerStartsAtCommand
                ? "snapshot_player_autonomous_region"
                : "snapshot_player_active_command_region",
            BattleId = playerStartsAtCommand
                ? "battle_player_autonomous_region"
                : "battle_player_active_command_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "player_group",
                    RuntimeCommanderGroupId = "player_group",
                    FactionId = "player",
                    SourceForceId = "player_force",
                    HeroId = "player_hero",
                    HeroDefinitionId = "player_hero_definition",
                    CorpsId = "player_corps",
                    CorpsDefinitionId = "player_corps_definition",
                    CorpsStrength = 80,
                    MaxHitPoints = 80,
                    AttackDamage = 1,
                    SourceLocationId = "site_1",
                    CellX = 0,
                    CellY = 0,
                    InitialCorpsCommandId = "Assault",
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded,
                    InitialTacticalRegions =
                    {
                        BuildRegion("player_command_region", "player_group", BattleTacticalRegionKind.FixedTarget, commandX, 0)
                    }
                },
                BuildEnemyGroup("enemy_near", "enemy_near_force", 10, 0, 80),
                BuildEnemyGroup("enemy_dense_a", "enemy_dense_a_force", 22, 0, 80),
                BuildEnemyGroup("enemy_dense_b", "enemy_dense_b_force", 22, 1, 80)
            }
        };

        for (int x = 0; x <= 24; x++)
        {
            AddSurface(snapshot, x, 0);
            AddSurface(snapshot, x, 1);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildEnemyGroup(
        string battleGroupId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = battleGroupId,
            FactionId = "enemy",
            SourceForceId = sourceForceId,
            HeroId = $"{battleGroupId}_hero",
            HeroDefinitionId = "enemy_hero_definition",
            CorpsId = $"{battleGroupId}_corps",
            CorpsDefinitionId = "enemy_corps_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = 1,
            SourceLocationId = "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = "HoldLine",
            TacticalMode = BattleGroupTacticalMode.EnemyHoldDefense
        };
    }

    private static BattleTacticalRegionSnapshot BuildRegion(
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
