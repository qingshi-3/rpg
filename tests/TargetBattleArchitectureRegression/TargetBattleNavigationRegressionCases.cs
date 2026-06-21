using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleNavigationRegressionCases
{
    public static void BattleGridMapReaderDoesNotConsumeComplexTileSetNavigationData()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Battle", "GridMapReader.cs"));
        AssertTrue(!source.Contains("MoveCostCustomData", StringComparison.Ordinal), "grid map reader should not read MoveCost custom data");
        AssertTrue(!source.Contains("CanStandOnCustomData", StringComparison.Ordinal), "grid map reader should not read CanStandOn custom data");
        AssertTrue(!source.Contains("IsObstacleCustomData", StringComparison.Ordinal), "grid map reader should not read IsObstacle custom data");
        AssertTrue(source.Contains("WalkableCustomData", StringComparison.Ordinal), "grid map reader should still read Walkable custom data");
        AssertTrue(source.Contains("TerrainTagCustomData", StringComparison.Ordinal), "grid map reader may keep TerrainTag custom data");
    }

    public static void BattleTileSetsOnlyExposeWalkableNavigationCustomData()
    {
        string ground = File.ReadAllText(Path.Combine(ProjectRoot(), "assets", "tilesets", "battle", "bone", "ground.tres"));
        string objects = File.ReadAllText(Path.Combine(ProjectRoot(), "assets", "tilesets", "battle", "bone", "objects.tres"));

        AssertTrue(!ground.Contains("custom_data_layer_2/name = \"MoveCost\"", StringComparison.Ordinal), "ground tileset should not expose MoveCost layer");
        AssertTrue(!ground.Contains("custom_data_layer_3/name = \"CanStandOn\"", StringComparison.Ordinal), "ground tileset should not expose CanStandOn layer");
        AssertTrue(!ground.Contains("custom_data_layer_4/name = \"IsObstacle\"", StringComparison.Ordinal), "ground tileset should not expose IsObstacle layer");
        AssertTrue(ground.Contains("custom_data_layer_0/name = \"Walkable\"", StringComparison.Ordinal) ||
                   ground.Contains("custom_data_layer_1/name = \"Walkable\"", StringComparison.Ordinal),
            "ground tileset should still expose Walkable layer");

        AssertTrue(!objects.Contains("custom_data_layer_0/name = \"IsObstacle\"", StringComparison.Ordinal), "object tileset should not expose IsObstacle layer");
    }

    public static void BattleNavigationSnapshotBuilderExportsUniformCostForWalkableSurfaces()
    {
        BattleGridMap gridMap = new();
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(2, 3), 0);
        surface.AddLayer(new GridCellLayerData(
            layerName: "Foundation",
            role: LayerRole.Foundation,
            height: 0,
            affectsWalkability: true,
            affectsLineOfSight: false,
            isHeightTransitionLayer: false,
            isVisualOnly: false,
            walkable: true,
            moveCost: 7,
            canStandOn: true,
            isObstacle: false,
            terrainTag: "road",
            sourceId: 1,
            atlasX: 0,
            atlasY: 0,
            alternativeTile: 0));

        BattleStartRequest request = new();
        BattleNavigationSnapshotBuilder.ApplyToRequest(request, gridMap);

        BattleNavigationSurfaceSnapshot? exported = request.NavigationSurfaces.SingleOrDefault(item =>
            item.X == 2 &&
            item.Y == 3 &&
            item.Height == 0);
        AssertTrue(exported != null, "builder should export the authored walkable top surface");
        AssertEqual(1, exported!.MoveCost, "runtime navigation surfaces should use uniform traversal cost");
    }

    public static void BattleNavigationSnapshotBuilderExcludesUndergroundWaterFromTopology()
    {
        BattleGridMap gridMap = new();
        AddFoundationSurface(gridMap, 0, 0, -1, "water", walkable: true);
        AddFoundationSurface(gridMap, 0, 0, 0, "land", walkable: true);
        AddFoundationSurface(gridMap, 1, 0, -1, "water", walkable: true);

        BattleStartRequest request = new();
        BattleNavigationSnapshotBuilder.ApplyToRequest(request, gridMap);

        AssertTrue(
            request.NavigationSurfaces.Any(item => item.X == 0 && item.Y == 0 && item.Height == 0),
            "land top surface should be exported");
        AssertTrue(
            request.NavigationSurfaces.All(item => item.Height >= 0),
            "runtime navigation snapshot should not export underground water as a fallback surface");
        AssertTrue(
            request.NavigationTopology.Nodes.All(item => item.Height >= 0),
            "compiled topology should not contain underground water nodes");
    }

    public static void BattleNavigationSnapshotBuilderExcludesNegativeHeightFallbackSurfaces()
    {
        BattleGridMap gridMap = new();
        AddFoundationSurface(gridMap, 0, 0, -1, "", walkable: true);
        AddFoundationSurface(gridMap, 0, 0, 0, "land", walkable: true);
        AddFoundationSurface(gridMap, 1, 0, -1, "", walkable: true);

        BattleStartRequest request = new();
        BattleNavigationSnapshotBuilder.ApplyToRequest(request, gridMap);

        AssertTrue(
            request.NavigationSurfaces.Any(item => item.X == 0 && item.Y == 0 && item.Height == 0),
            "land top surface should remain the runtime navigation export");
        AssertTrue(
            request.NavigationSurfaces.All(item => item.Height >= 0),
            "runtime navigation export should not treat untagged negative-height foundations as fallback land");
        AssertTrue(
            request.NavigationTopology.Nodes.All(item => item.Height >= 0),
            "compiled topology should not contain untagged negative-height fallback nodes");
        AssertTrue(
            request.NavigationTopology.Nodes.All(item => item.X != 1 || item.Y != 0),
            "a cell with only underground foundation should not become runtime land topology");
    }

    public static void BattleNavigationTopologyCompilerProducesFinalEdgesBeforeRuntime()
    {
        BattleNavigationTopology topology = BattleNavigationTopologyCompiler.Compile(
            new[]
            {
                new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 9 },
                new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 0, MoveCost = 1 },
                new BattleNavigationSurfaceSnapshot { X = 0, Y = 1, Height = 0, MoveCost = 1 },
                new BattleNavigationSurfaceSnapshot { X = 1, Y = 1, Height = 0, MoveCost = 1 },
                new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 1, MoveCost = 1 }
            },
            new[]
            {
                new BattleNavigationConnectionSnapshot
                {
                    FromX = 0,
                    FromY = 0,
                    FromHeight = 0,
                    ToX = 1,
                    ToY = 0,
                    ToHeight = 1,
                    MoveCost = 4
                }
            });

        AssertTrue(
            topology.Nodes.Any(item => item.X == 0 && item.Y == 0 && item.Height == 0 && item.MoveCost == 1),
            "topology compiler should normalize node traversal cost before Runtime receives it");
        AssertTrue(
            topology.Edges.Any(item =>
                item.FromX == 0 &&
                item.FromY == 0 &&
                item.FromHeight == 0 &&
                item.ToX == 1 &&
                item.ToY == 0 &&
                item.ToHeight == 0 &&
                item.Kind == BattleNavigationEdgeKind.GeneratedSameLevel),
            "topology compiler should generate same-level edges before Runtime pathfinding starts");
        AssertTrue(
            topology.Edges.Any(item =>
                item.FromX == 0 &&
                item.FromY == 0 &&
                item.FromHeight == 0 &&
                item.ToX == 1 &&
                item.ToY == 0 &&
                item.ToHeight == 1 &&
                item.Kind == BattleNavigationEdgeKind.AuthoredConnection &&
                item.MoveCost == 1),
            "topology compiler should turn authored height links into normalized explicit topology edges");
    }

    public static void RuntimeNavigationGraphConsumesTopologyDataLayerOnly()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Runtime", "Battle", "Navigation", "BattleNavigationGraph.cs"));
        AssertTrue(!source.Contains("BattleNavigationSurfaceSnapshot", StringComparison.Ordinal), "runtime graph should not compile raw surface snapshots");
        AssertTrue(!source.Contains("BattleNavigationConnectionSnapshot", StringComparison.Ordinal), "runtime graph should not compile raw connection snapshots");
        AssertTrue(!source.Contains("NavigationSurfaces", StringComparison.Ordinal), "runtime graph should not read raw navigation surface lists");
        AssertTrue(!source.Contains("NavigationConnections", StringComparison.Ordinal), "runtime graph should not read raw navigation connection lists");
        AssertTrue(source.Contains("BattleNavigationTopology", StringComparison.Ordinal), "runtime graph should consume the explicit topology data layer");
    }

    public static void RuntimeNavigationMainLoopUsesLocalNeighborPlannerInsteadOfActorAStar()
    {
        string root = ProjectRoot();
        string tickResolver = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeTickResolver.cs"));
        string decisionContextBuilder = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeDecisionContextBuilder.cs"));
        string combatZoneJoinRetargeting = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleCombatZoneJoinRetargeting.cs"));
        string movementContinuation = string.Join("\n", Directory.GetFiles(
                Path.Combine(root, "src", "Runtime", "Battle"),
                "BattleMovementController*.cs",
                SearchOption.TopDirectoryOnly)
            .OrderBy(item => item, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        string navigationSource = string.Join("\n", Directory.GetFiles(
                Path.Combine(root, "src", "Runtime", "Battle", "Navigation"),
                "*.cs",
                SearchOption.TopDirectoryOnly)
            .OrderBy(item => item, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        string mainMovementLoopSource = string.Join("\n", tickResolver, decisionContextBuilder, combatZoneJoinRetargeting, movementContinuation);

        AssertTrue(
            !mainMovementLoopSource.Contains("BattlePathfinder.TryFindNextStepTowardAttackRange", StringComparison.Ordinal),
            "runtime main movement loop must not use per-actor A* as the primary battle navigation path");
        AssertTrue(
            decisionContextBuilder.Contains("BuildTargetMovementProposalContext", StringComparison.Ordinal) &&
            movementContinuation.Contains("BuildTargetMovementProposalContext", StringComparison.Ordinal),
            "runtime main movement loop should route target/local-combat proposal construction through the actor movement controller");
        AssertTrue(
            !tickResolver.Contains("BattleCrowdMovementPlanner", StringComparison.Ordinal) &&
            !decisionContextBuilder.Contains("BattleCrowdMovementPlanner", StringComparison.Ordinal) &&
            !combatZoneJoinRetargeting.Contains("BattleCrowdMovementPlanner", StringComparison.Ordinal) &&
            movementContinuation.Contains("BattleCrowdMovementPlanner.FindNextStepCandidatesTowardTarget", StringComparison.Ordinal) &&
            movementContinuation.Contains("BattleCrowdMovementPlanner.FindNextStepCandidatesTowardCombatSlot", StringComparison.Ordinal),
            "low-level crowd planner usage should live behind BattleMovementController after H2");
        AssertTrue(
            navigationSource.Contains("FindGreedyNextStepCandidatesTowardTarget", StringComparison.Ordinal) &&
            navigationSource.Contains("BattleCombatSlotAllocator", StringComparison.Ordinal),
            "battle navigation should use local neighbor movement over target-local combat slots");
    }

    public static void BattleNavigationTopologyDiagnosticsPrintNodesEdgesAndPlacements()
    {
        BattleNavigationTopology topology = BattleNavigationTopologyCompiler.Compile(
            new[]
            {
                new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
                new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 0, MoveCost = 1 }
            },
            new[]
            {
                new BattleNavigationConnectionSnapshot
                {
                    FromX = 0,
                    FromY = 0,
                    FromHeight = 0,
                    ToX = 1,
                    ToY = 0,
                    ToHeight = 0,
                    MoveCost = 1
                }
            });
        BattleStartRequest request = new()
        {
            RequestId = "request_debug_topology",
            ContextId = "battle_debug_topology"
        };
        request.NavigationTopology = topology;
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "player_force",
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { PlacementId = "hero", CellX = 0, CellY = 0, CellHeight = 0 }
            }
        });
        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "enemy_force",
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { PlacementId = "enemy", CellX = 5, CellY = 0, CellHeight = 0 }
            }
        });

        string dump = BattleNavigationTopologyDiagnostics.DescribeRequestTopology(request, "unit_test");

        AssertTrue(dump.Contains("stage=unit_test", StringComparison.Ordinal), "diagnostic should identify boundary stage");
        AssertTrue(dump.Contains("nodes=2", StringComparison.Ordinal), "diagnostic should print node count");
        AssertTrue(dump.Contains("edges=", StringComparison.Ordinal), "diagnostic should print edge count");
        AssertTrue(dump.Contains("nodeHeights=h=0:2", StringComparison.Ordinal), "diagnostic should summarize topology node heights");
        AssertTrue(dump.Contains("nodeSample=(0,0,h=0);(1,0,h=0)", StringComparison.Ordinal), "diagnostic should print node sample");
        AssertTrue(dump.Contains("edgeSample=(0,0,h=0)->(1,0,h=0):GeneratedSameLevel", StringComparison.Ordinal), "diagnostic should print edge sample");
        AssertTrue(dump.Contains("placement=player_force/hero@(0,0,h=0):inTopology=True", StringComparison.Ordinal), "diagnostic should confirm valid placement node");
        AssertTrue(dump.Contains("placement=enemy_force/enemy@(5,0,h=0):inTopology=False", StringComparison.Ordinal), "diagnostic should expose actor starts outside topology");
    }

    public static void RuntimeNavigationConsumesAuthoredSurfaceSnapshot()
    {
        BattleStartSnapshot snapshot = BuildLayeredSnapshot("battle_authored_surface_blocked");

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? playerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(
            playerMove == null ||
            playerMove.ToGridX != 1 ||
            playerMove.ToGridY != 0 ||
            playerMove.ToGridHeight != 0,
            "authored navigation should not let runtime walk onto a covered low surface that is absent from the snapshot");
    }

    public static void RuntimeNavigationChangesHeightOnlyThroughAuthoredConnections()
    {
        BattleStartSnapshot snapshot = BuildLayeredSnapshot("battle_authored_surface_connection");
        snapshot.LocationContext.NavigationConnections.Add(new BattleNavigationConnectionSnapshot
        {
            FromX = 0,
            FromY = 0,
            FromHeight = 0,
            ToX = 1,
            ToY = 0,
            ToHeight = 1,
            MoveCost = 1
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? playerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(playerMove != null, "runtime should use an authored height connection when one exists");
        AssertEqual(0, playerMove!.FromGridHeight, "movement source height");
        AssertEqual(1, playerMove.ToGridX, "connected destination x");
        AssertEqual(0, playerMove.ToGridY, "connected destination y");
        AssertEqual(1, playerMove.ToGridHeight, "connected destination height");
    }

    public static void RuntimeNavigationDiagnosticsExplainUnreachableAdvance()
    {
        const string battleId = "battle_unreachable_navigation_diagnostic";
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 0, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 2, 0, 0)
            }
        };
        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 0, Height = 0, MoveCost = 1 }
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        _ = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertTrue(File.Exists(GameLog.CurrentLogPath), "navigation diagnostics should write to the current game log");
        string log = File.ReadAllText(GameLog.CurrentLogPath);
        AssertTrue(
            log.Contains($"BattleRuntimeAdvanceDiagnostic battle={battleId}", StringComparison.Ordinal) &&
            log.Contains("BattleRuntimeNavigationActorStarts", StringComparison.Ordinal) &&
            log.Contains("reason=path_not_found", StringComparison.Ordinal) &&
            log.Contains("startInGraph=True", StringComparison.Ordinal) &&
            log.Contains("targetInGraph=True", StringComparison.Ordinal) &&
            log.Contains("staticTargetReachable=False", StringComparison.Ordinal) &&
            log.Contains("reachableAttackAnchors=0", StringComparison.Ordinal),
            "unreachable navigation should log static graph reachability and attack-anchor counts");
    }

    public static void RuntimeNavigationRejectsDiagonalCornerCutting()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_diagonal_corner_cut",
            BattleId = "battle_diagonal_corner_cut",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 0, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 2, 2, 0)
            }
        };
        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 1, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 2, Height = 0, MoveCost = 1 }
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? tickZeroPlayerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementStarted &&
            item.EventId.Contains(":tick_0:", StringComparison.Ordinal));
        AssertTrue(
            tickZeroPlayerMove == null ||
            tickZeroPlayerMove.ToGridX != 1 ||
            tickZeroPlayerMove.ToGridY != 1,
            "runtime should not diagonal-step through blocked orthogonal sides");
    }

    public static void RuntimeNavigationRejectsProjectedDiagonalCornerCutting()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_projected_diagonal_corner_cut",
            BattleId = "battle_projected_diagonal_corner_cut",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 0, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 3, 2, 0)
            }
        };
        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 1, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 1, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 1, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 3, Y = 2, Height = 0, MoveCost = 1 }
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? tickZeroPlayerMove = result.EventStream.Events.FirstOrDefault(item =>
            item.ActorId == "a_player:1" &&
            item.Kind == BattleEventKind.MovementStarted &&
            item.EventId.Contains(":tick_0:", StringComparison.Ordinal));
        AssertTrue(
            tickZeroPlayerMove == null ||
            tickZeroPlayerMove.ToGridX != 1 ||
            tickZeroPlayerMove.ToGridY != 0,
            "runtime should not commit the shorter first step when that projected route reaches attack range only by diagonal corner cutting");
    }

    public static void RuntimeNavigationKeepsTopCorridorInsteadOfDippingIntoLowerProtrusion()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_top_corridor",
            BattleId = "battle_top_corridor",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 2, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 9, 2, 0)
            }
        };
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 80;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        AddLandRows(snapshot, new[]
        {
            "1111111111",
            "1100110011",
            "1100110011"
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] middleProtrusionMoves = result.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementCompleted &&
                item.ToGridY > 0 &&
                item.ToGridX is >= 2 and <= 7)
            .ToArray();

        AssertEqual(
            0,
            middleProtrusionMoves.Length,
            $"units should use the top corridor instead of dipping into lower protrusions: {DescribeMoves(middleProtrusionMoves)}");
    }

    public static void RuntimeNavigationKeepsSecondAllyOnTopCorridorInsteadOfLowerProtrusion()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_top_corridor_two_allies",
            BattleId = "battle_top_corridor_two_allies",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_top", "player", "a_player_top", 0, 0, 0),
                BuildGroup("group_player_bottom", "player", "b_player_bottom", 0, 2, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 9, 2, 0)
            }
        };
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 80;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        AddLandRows(snapshot, new[]
        {
            "1111111111",
            "1100110011",
            "1100110011"
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] lowerProtrusionMoves = result.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementCompleted &&
                item.ActorId.EndsWith(":1", StringComparison.Ordinal) &&
                item.ActorId.Contains("player", StringComparison.Ordinal) &&
                item.ToGridY > 0 &&
                item.ToGridX is >= 2 and <= 7)
            .ToArray();

        AssertEqual(
            0,
            lowerProtrusionMoves.Length,
            $"allies should queue through the top corridor instead of dipping into lower protrusions: {DescribeMoves(lowerProtrusionMoves)}");
    }

    private static BattleStartSnapshot BuildLayeredSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "a_player", 0, 0, 0),
                BuildGroup("group_enemy", "enemy", "z_enemy", 2, 0, 1)
            }
        };

        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 1, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 0, Height = 1, MoveCost = 1 }
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static void AddLandRows(BattleStartSnapshot snapshot, IReadOnlyList<string> rows)
    {
        for (int y = 0; y < rows.Count; y++)
        {
            string row = rows[y] ?? "";
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x] != '1')
                {
                    continue;
                }

                snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }
    }

    private static string DescribeMoves(IEnumerable<BattleEvent> moves)
    {
        return string.Join(";",
            (moves ?? Enumerable.Empty<BattleEvent>())
                .Select(item => $"{item.ActorId}:{item.FromGridX},{item.FromGridY}->{item.ToGridX},{item.ToGridY}@tick{item.RuntimeTick}"));
    }

    private static GridCellSurface AddFoundationSurface(
        BattleGridMap gridMap,
        int x,
        int y,
        int height,
        string terrainTag,
        bool walkable)
    {
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(x, y), height);
        surface.AddLayer(new GridCellLayerData(
            layerName: $"Foundation_{terrainTag}_{height}",
            role: LayerRole.Foundation,
            height: height,
            affectsWalkability: true,
            affectsLineOfSight: false,
            isHeightTransitionLayer: false,
            isVisualOnly: false,
            walkable: walkable,
            moveCost: walkable ? 1 : 0,
            canStandOn: walkable,
            isObstacle: !walkable,
            terrainTag: terrainTag,
            sourceId: 1,
            atlasX: 0,
            atlasY: 0,
            alternativeTile: 0));
        return surface;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int cellHeight)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = 80,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            CellHeight = cellHeight
        };
    }

    private static string ProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
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
