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
        run("temporary region builds from player clusters when fixed regions empty", TemporaryRegionBuildsFromPlayerClustersWhenFixedRegionsEmpty);
        run("temporary region refresh interval defaults to five ticks", TemporaryRegionRefreshIntervalDefaultsToFiveTicks);
    }

    private static void TemporaryRegionBuildsFromPlayerClustersWhenFixedRegionsEmpty()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTemporaryRegionSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates["enemy_group"];
        BattleEvent? regionMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1");

        AssertEqual(BattleTacticalRegionKind.TemporaryTarget, state.SelectedRegion?.Kind, "enemy should replace empty fixed region with temporary target");
        AssertEqual("enemy_group", state.SelectedRegion?.OwnerBattleGroupId, "temporary region owner");
        AssertEqual(10, state.SelectedRegion?.CenterCellX, "temporary region center x");
        AssertEqual(0, state.SelectedRegion?.CenterCellY, "temporary region center y");
        AssertEqual(TemporaryCreatedCluster, state.SelectedRegion?.ReasonCode, "temporary region creation reason");
        AssertTrue(regionMove != null, "enemy should move toward generated temporary region");
        AssertEqual(BattleGroupTacticalReasonCode.RegionTemporaryAdvance, regionMove!.ReasonCode, "temporary region movement reason");
    }

    private static void TemporaryRegionRefreshIntervalDefaultsToFiveTicks()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildTemporaryRegionSnapshot());

        controller.AdvanceNextTick();
        AssertEqual(10, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, "initial temporary center");

        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        player.GridX = 12;

        for (int i = 1; i <= 4; i++)
        {
            controller.AdvanceNextTick();
            AssertEqual(10, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, $"temporary region should be reused on tick {i}");
        }

        controller.AdvanceNextTick();
        AssertEqual(12, controller.State.TacticalStates["enemy_group"].SelectedRegion?.CenterCellX, "temporary region may refresh on fifth tick");
    }

    private static BattleStartSnapshot BuildTemporaryRegionSnapshot()
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
                        BuildRegion("empty_fixed_region", "enemy_group", BattleTacticalRegionKind.FixedTarget, 0, 0)
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
                    CellX = 10,
                    CellY = 0,
                    InitialCorpsCommandId = "HoldLine",
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded
                }
            }
        };

        for (int x = 0; x <= 12; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
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
