using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleCenteredRegionRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("battle entry fixed target region stores deployment zone center", BattleEntryFixedTargetRegionStoresDeploymentZoneCenter);
        run("enemy region movement prioritizes deployment zone center", EnemyRegionMovementPrioritizesDeploymentZoneCenter);
    }

    private static void BattleEntryFixedTargetRegionStoresDeploymentZoneCenter()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_fixed_region_center");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "player_deployment_wide",
            ObjectiveRole = "player_deployment",
            DeploymentSide = "Player",
            FactionId = "player",
            CellX = 10,
            CellY = 4,
            Width = 5,
            Height = 3
        });
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (12, 5)));
        request.EnemyForces.Add(BuildForce("enemy_attacker", "enemy", count: 1, (14, 0)));

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_attacker");
        BattleTacticalRegionSnapshot fixedRegion = enemy.InitialTacticalRegions.Single();
        AssertEqual(12, fixedRegion.CenterCellX, "fixed target region should store the deployment zone center x");
        AssertEqual(5, fixedRegion.CenterCellY, "fixed target region should store the deployment zone center y");
        AssertEqual(5, fixedRegion.Width, "fixed target region should preserve zone width");
        AssertEqual(3, fixedRegion.Height, "fixed target region should preserve zone height");
    }

    private static void EnemyRegionMovementPrioritizesDeploymentZoneCenter()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildEnemyCenteredRegionMovementSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_force:1");

        AssertTrue(move != null, "non-engaged enemy should move toward the fixed deployment region");
        AssertEqual("enemy_fixed_centered", move!.TargetId, "region movement event target id");
        AssertEqual(14, move.FromGridX, "enemy start x");
        AssertEqual(0, move.FromGridY, "enemy start y");
        AssertEqual(13, move.ToGridX, "region movement should step toward the center cell instead of the nearest edge");
        AssertEqual(1, move.ToGridY, "region movement should still advance into the deployment zone");
    }

    private static BattleStartSnapshot BuildEnemyCenteredRegionMovementSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_enemy_centered_region_movement",
            BattleId = "battle_enemy_centered_region_movement",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    BattleGroupTacticalMode.EnemyOffense,
                    new BattleTacticalRegionSnapshot
                    {
                        RegionId = "enemy_fixed_centered",
                        OwnerBattleGroupId = "enemy_group",
                        Kind = BattleTacticalRegionKind.FixedTarget,
                        CenterCellX = 12,
                        CenterCellY = 5,
                        CenterCellHeight = 0,
                        Width = 5,
                        Height = 3
                    }),
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
                    CellX = 12,
                    CellY = 5,
                    TacticalMode = BattleGroupTacticalMode.PlayerCommanded
                }
            }
        };

        snapshot.BattleGroups[0].SourceForceId = "enemy_force";
        snapshot.BattleGroups[0].CellX = 14;
        snapshot.BattleGroups[0].CellY = 0;
        for (int y = 0; y <= 7; y++)
        {
            for (int x = 9; x <= 15; x++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        BattleGroupTacticalMode tacticalMode,
        BattleTacticalRegionSnapshot? initialRegion = null)
    {
        BattleGroupSnapshot group = new()
        {
            BattleGroupId = groupId,
            FactionId = groupId.StartsWith("enemy", StringComparison.Ordinal) ? "enemy" : "player",
            SourceForceId = $"{groupId}_force",
            HeroId = $"{groupId}_hero",
            HeroDefinitionId = $"{groupId}_hero_definition",
            CorpsId = $"{groupId}_corps",
            CorpsDefinitionId = $"{groupId}_corps_definition",
            CorpsStrength = 80,
            MaxHitPoints = 80,
            AttackDamage = 1,
            SourceLocationId = "site_1",
            CellX = groupId.StartsWith("enemy", StringComparison.Ordinal) ? 1 : 0,
            CellY = 0,
            TacticalMode = tacticalMode
        };
        if (initialRegion != null)
        {
            group.InitialTacticalRegions.Add(initialRegion);
        }

        return group;
    }

    private static BattleStartRequest BuildBattleEntryRequest(string id)
    {
        return new BattleStartRequest
        {
            RequestId = id,
            ContextId = id,
            TargetSiteId = "site_1",
            BattleKind = BattleKind.AssaultSite
        };
    }

    private static BattleForceRequest BuildForce(
        string forceId,
        string factionId,
        int count,
        params (int X, int Y)[] placements)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = $"{forceId}_unit",
            FactionId = factionId,
            Count = count,
            FootprintWidth = 1,
            FootprintHeight = 1,
            MaxHitPoints = 80,
            AttackDamage = 1
        };

        foreach ((int x, int y) in placements)
        {
            force.PreferredPlacements.Add(new BattleForcePlacementRequest
            {
                CellX = x,
                CellY = y
            });
        }

        return force;
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
