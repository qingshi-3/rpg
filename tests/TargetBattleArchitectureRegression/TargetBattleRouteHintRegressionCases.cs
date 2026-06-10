using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleRouteHintRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime route hint guides objective around long static barrier", RuntimeRouteHintGuidesObjectiveAroundLongStaticBarrier);
        run("runtime route hint prefers forward corridor across source boundary", RuntimeRouteHintPrefersForwardCorridorAcrossSourceBoundary);
        run("route topology scores portal corridor travel cost", RouteTopologyScoresPortalCorridorTravelCost);
        run("route topology source shape tracks footprint clearance profiles", RouteTopologySourceShapeTracksFootprintClearanceProfiles);
        run("group route hint cache is scoped by group and region", GroupRouteHintCacheIsScopedByGroupAndRegion);
        run("runtime route hint source shape does not revive flow fields or actor pathfinder", RuntimeRouteHintSourceShapeDoesNotReviveFlowFieldsOrActorPathfinder);
    }

    public static void RuntimeRouteHintGuidesObjectiveAroundLongStaticBarrier()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildLongRiverRouteHintSnapshot());
        List<(int X, int Y)> startedMoves = new();

        for (int i = 0; i < 96 && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
            foreach (BattleEvent move in tick.Events.Where(item =>
                         item.Kind == BattleEventKind.MovementStarted &&
                         item.ActorId == "force_player:1"))
            {
                startedMoves.Add((move.ToGridX, move.ToGridY));
            }
        }

        string summary = string.Join(";", startedMoves.Select(item => $"({item.X},{item.Y})"));
        AssertTrue(
            startedMoves.Count > 0 && startedMoves[0].X < 0,
            $"group route hint should allow the first step to move away from the final objective toward the real passage: moves={summary}");
        AssertTrue(
            startedMoves.Any(item => item.X > 0),
            $"group route hint should move the actor through the northern passage instead of exhausting local obstacle-follow along the river: moves={summary}");
    }

    public static void RouteTopologySourceShapeTracksFootprintClearanceProfiles()
    {
        string root = ProjectRoot();
        string routeTopology = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleRouteTopology.cs"));

        AssertTrue(routeTopology.Contains("BattleRouteProfile", StringComparison.Ordinal), "route topology should key route data by footprint profile");
        AssertTrue(routeTopology.Contains("CanUseStaticStep", StringComparison.Ordinal), "route clearance must reuse Runtime static step validation");
        AssertTrue(routeTopology.Contains("FootprintWidth", StringComparison.Ordinal) || routeTopology.Contains("Width", StringComparison.Ordinal), "route profile should include footprint width");
        AssertTrue(routeTopology.Contains("FootprintHeight", StringComparison.Ordinal) || routeTopology.Contains("Height", StringComparison.Ordinal), "route profile should include footprint height");
    }

    public static void RouteTopologyScoresPortalCorridorTravelCost()
    {
        string root = ProjectRoot();
        string routeTopology = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleRouteTopology.cs"));

        AssertTrue(
            routeTopology.Contains("entryAnchor", StringComparison.Ordinal) &&
            routeTopology.Contains("GetPortalTransitionCost", StringComparison.Ordinal),
            "route topology search should score travel from the current region entry to the next portal instead of selecting the nearest first edge only");
        AssertTrue(
            routeTopology.Contains("targetPenalty", StringComparison.Ordinal),
            "route topology search should score the final portal-to-target travel cost so long detours do not beat the forward corridor");
    }

    public static void GroupRouteHintCacheIsScopedByGroupAndRegion()
    {
        string root = ProjectRoot();
        string groupCache = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleGroupRouteHintCache.cs"));
        string navigationGraph = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleNavigationGraph.cs"));

        AssertTrue(groupCache.Contains("BattleGroupRouteHintCache", StringComparison.Ordinal), "route hints should have a group-scoped cache");
        AssertTrue(groupCache.Contains("BattleGroupId", StringComparison.Ordinal), "cache key should include battle group identity");
        AssertTrue(groupCache.Contains("SourceRegionId", StringComparison.Ordinal), "cache key should include source route region");
        AssertTrue(navigationGraph.Contains("BattleGroupRouteHintCache", StringComparison.Ordinal), "runtime graph should own the group route hint cache next to immutable route topology");
    }

    public static void RuntimeRouteHintSourceShapeDoesNotReviveFlowFieldsOrActorPathfinder()
    {
        string root = ProjectRoot();
        string navigationRoot = Path.Combine(root, "src", "Runtime", "Battle", "Navigation");
        string routeTopology = File.ReadAllText(Path.Combine(navigationRoot, "BattleRouteTopology.cs"));
        string groupCache = File.ReadAllText(Path.Combine(navigationRoot, "BattleGroupRouteHintCache.cs"));

        AssertTrue(routeTopology.Contains("BattleRouteTopology", StringComparison.Ordinal), "static route topology should have its own runtime navigation type");
        AssertTrue(groupCache.Contains("BattleGroupRouteHintCache", StringComparison.Ordinal), "route hints should have a group-scoped cache");
        AssertTrue(!File.Exists(Path.Combine(navigationRoot, "BattleFlowFieldBuilder.cs")), "route hints must not restore flow-field builders");
        AssertTrue(!File.Exists(Path.Combine(navigationRoot, "BattlePathfinder.cs")), "route hints must not restore the per-actor pathfinder");
    }

    public static void RuntimeRouteHintPrefersForwardCorridorAcrossSourceBoundary()
    {
        string previousLog = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildBoundaryCorridorRouteHintSnapshot());
        List<BattleEvent> moves = new();

        for (int i = 0; i < 8 && !controller.IsComplete; i++)
        {
            BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
            moves.AddRange(tick.Events.Where(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId == "force_player:1"));
        }

        string log = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        string newLog = log.Length >= previousLog.Length
            ? log[previousLog.Length..]
            : log;
        string[] badRouteHintLines = newLog
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(item =>
                item.Contains("BattleRuntimeRouteHint", StringComparison.Ordinal) &&
                item.Contains("objective_east", StringComparison.Ordinal) &&
                item.Contains("sourceRegion=3,4,0", StringComparison.Ordinal) &&
                item.Contains("targetRegion=7,4,0", StringComparison.Ordinal) &&
                RouteHintX(item) < 25)
            .ToArray();

        AssertTrue(moves.Count > 0, "boundary-corridor route hint fixture should move the eastbound actor");
        AssertTrue(
            moves[0].ToGridX > moves[0].FromGridX,
            $"boundary-corridor route hint should not make an eastbound actor step west on its first movement: from=({moves[0].FromGridX},{moves[0].FromGridY}) to=({moves[0].ToGridX},{moves[0].ToGridY})");
        AssertTrue(
            badRouteHintLines.Length == 0,
            "boundary-corridor route hint should not bounce an eastbound actor back into the west neighbor region: " + string.Join(" | ", badRouteHintLines));
    }

    private static BattleStartSnapshot BuildLongRiverRouteHintSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_route_hint_long_river",
            BattleId = "battle_route_hint_long_river",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    0,
                    0,
                    200,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_gate",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 35,
                        ObjectiveCellY = 0,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 40, 0, 200, initialCommandId: "HoldLine")
            }
        };

        for (int x = -5; x <= 0; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        for (int y = -5; y <= 0; y++)
        {
            AddSurface(snapshot, -5, y);
        }

        for (int x = -5; x <= 35; x++)
        {
            AddSurface(snapshot, x, -5);
        }

        for (int y = -5; y <= 0; y++)
        {
            AddSurface(snapshot, 35, y);
        }

        for (int x = 35; x <= 40; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildBoundaryCorridorRouteHintSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_route_hint_boundary_corridor",
            BattleId = "battle_route_hint_boundary_corridor",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    25,
                    35,
                    200,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "objective_east",
                        EngagementRule = BattleEngagementRule.MoveFirst,
                        HasObjectiveAnchor = true,
                        ObjectiveCellX = 63,
                        ObjectiveCellY = 35,
                        ObjectiveCellHeight = 0,
                        ObjectiveWidth = 1,
                        ObjectiveHeight = 1
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 70, 35, 200, initialCommandId: "HoldLine")
            }
        };

        for (int x = 24; x <= 70; x++)
        {
            AddSurface(snapshot, x, 35);
        }

        for (int y = 31; y <= 35; y++)
        {
            AddSurface(snapshot, 23, y);
            AddSurface(snapshot, 63, y);
        }

        for (int x = 23; x <= 63; x++)
        {
            AddSurface(snapshot, x, 31);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "",
        BattleGroupPlanSnapshot plan = null)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            RuntimeCommanderGroupId = groupId,
            FactionId = factionId,
            HeroId = $"{groupId}_hero",
            CorpsId = $"{groupId}_corps",
            HeroDefinitionId = $"{groupId}_hero_def",
            CorpsDefinitionId = $"{groupId}_corps_def",
            SourceForceId = sourceForceId,
            SourceLocationId = "site_1",
            CellX = cellX,
            CellY = cellY,
            CellHeight = 0,
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = 20,
            InitialCorpsCommandId = initialCommandId,
            Plan = plan
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

    private static string ProjectRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "rpg.csproj")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? "";
        }

        throw new InvalidOperationException("project root not found");
    }

    private static int RouteHintX(string line)
    {
        const string marker = " hint=(";
        int start = line.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return int.MaxValue;
        }

        start += marker.Length;
        int comma = line.IndexOf(',', start);
        if (comma <= start)
        {
            return int.MaxValue;
        }

        return int.TryParse(line[start..comma], out int x)
            ? x
            : int.MaxValue;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
