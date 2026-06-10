using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleNavigationGraph
{
    private const int DefaultBoundsMargin = 8;
    private const int DefaultMoveCost = 1;

    private static readonly BattleGridCoord[] NeighborOffsets =
    {
        new(1, 1),
        new(1, 0),
        new(0, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, 0),
        new(0, -1),
        new(-1, -1)
    };

    private readonly Dictionary<BattleGridCoord, int> _topologyNodeMoveCosts;
    private readonly Dictionary<BattleGridCoord, List<GraphEdge>> _topologyEdges;
    private readonly Dictionary<BattleGridCoord, List<GraphEdge>> _reverseTopologyEdges;
    private readonly Dictionary<GraphCell, int[]> _topologyHeightsByCell;
    private readonly BattleGroupRouteHintCache _routeHintCache;
    private readonly HashSet<string> _routeHintDiagnosticKeys = new();

    private BattleNavigationGraph(
        int minX,
        int maxX,
        int minY,
        int maxY,
        Dictionary<BattleGridCoord, int> topologyNodeMoveCosts = null,
        Dictionary<BattleGridCoord, List<GraphEdge>> topologyEdges = null,
        Dictionary<BattleGridCoord, List<GraphEdge>> reverseTopologyEdges = null)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        _topologyNodeMoveCosts = topologyNodeMoveCosts ?? new Dictionary<BattleGridCoord, int>();
        _topologyEdges = topologyEdges ?? new Dictionary<BattleGridCoord, List<GraphEdge>>();
        _reverseTopologyEdges = reverseTopologyEdges ?? new Dictionary<BattleGridCoord, List<GraphEdge>>();
        _topologyHeightsByCell = BuildTopologyHeightsByCell(_topologyNodeMoveCosts.Keys);
        RouteTopology = BattleRouteTopology.Build(this);
        _routeHintCache = new BattleGroupRouteHintCache(RouteTopology);
    }

    public int MinX { get; }
    public int MaxX { get; }
    public int MinY { get; }
    public int MaxY { get; }
    public bool UsesTopology => _topologyNodeMoveCosts.Count > 0;
    internal BattleRouteTopology RouteTopology { get; }
    public int MaxSearchNodes => UsesTopology
        ? _topologyNodeMoveCosts.Count
        : System.Math.Max(1, (MaxX - MinX + 1) * (MaxY - MinY + 1));

    public string DescribeTopology()
    {
        if (!UsesTopology)
        {
            return $"authored=False bounds=({MinX},{MinY})-({MaxX},{MaxY}) maxSearchNodes={MaxSearchNodes}";
        }

        NavigationTopologySummary summary = BuildTopologySummary();
        int edgeCount = _topologyEdges.Values.Sum(outgoing => outgoing.Count);
        return $"authored=True topologyNodes={_topologyNodeMoveCosts.Count} topologyEdges={edgeCount} components={summary.ComponentCount} largestComponent={summary.LargestComponentSize}";
    }

    public string DescribeRouteTopology()
    {
        return RouteTopology?.Describe() ?? "routeTopology=False";
    }

    internal bool TryGetRouteHintTowardObjective(
        BattleRuntimeActor actor,
        BattleGridCoord targetAnchor,
        int targetWidth,
        int targetHeight,
        out BattleRouteHint hint,
        string battleId = "",
        int tick = -1)
    {
        hint = default;
        if (actor == null || RouteTopology == null)
        {
            return false;
        }

        BattleRouteQuery query = BattleRouteQuery.Create(
            actor.BattleGroupId ?? "",
            string.IsNullOrWhiteSpace(actor.ObjectiveZoneId)
                ? $"{targetAnchor.X},{targetAnchor.Y},{targetAnchor.Height}"
                : actor.ObjectiveZoneId,
            actor,
            targetAnchor,
            targetWidth,
            targetHeight);
        bool resolved = _routeHintCache.TryResolve(query, out hint, out BattleRouteHintResolution resolution);
        LogRouteHintDiagnostic(battleId, tick, actor, query, resolution, resolved ? hint : default);
        return resolved;
    }

    private void LogRouteHintDiagnostic(
        string battleId,
        int tick,
        BattleRuntimeActor actor,
        BattleRouteQuery query,
        BattleRouteHintResolution resolution,
        BattleRouteHint hint)
    {
        string hintAnchor = resolution.Success ? hint.Anchor.ToString() : "none";
        string key = $"{battleId}|{actor?.ActorId}|{query.BattleGroupId}|{query.IntentId}|{query.Profile.Width}x{query.Profile.Height}|{resolution.SourceRegionId}|{resolution.TargetRegionId}|{resolution.Result}|{hintAnchor}";
        if (!_routeHintDiagnosticKeys.Add(key))
        {
            return;
        }

        GameLog.Info(
            nameof(BattleNavigationGraph),
            $"BattleRuntimeRouteHint battle={battleId ?? ""} tick={tick} actor={actor?.ActorId ?? ""} group={query.BattleGroupId ?? ""} intent={query.IntentId ?? ""} source={query.SourceAnchor} target={query.TargetAnchor} targetSize={query.TargetWidth}x{query.TargetHeight} profile={query.Profile.Width}x{query.Profile.Height} sourceRegion={resolution.SourceRegionId} targetRegion={resolution.TargetRegionId} result={resolution.Result} success={resolution.Success} builtThisQuery={resolution.BuiltThisQuery} hint={hintAnchor} corridor={(resolution.Success ? hint.CorridorId : "")} builds={resolution.QueryBuildCount}");
    }

    public string DescribeStaticReachability(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        int attackRange)
    {
        BattleGridCoord start = new(actor?.GridX ?? 0, actor?.GridY ?? 0, actor?.GridHeight ?? 0);
        BattleGridCoord targetAnchor = new(target?.GridX ?? 0, target?.GridY ?? 0, target?.GridHeight ?? 0);
        bool startInGraph = Contains(start);
        bool targetInGraph = Contains(targetAnchor);

        if (!UsesTopology)
        {
            return $"authored=False start={start} target={targetAnchor} startInGraph={startInGraph} targetInGraph={targetInGraph}";
        }

        HashSet<BattleGridCoord> reachable = startInGraph
            ? CollectReachable(start)
            : new HashSet<BattleGridCoord>();
        bool staticTargetReachable = reachable.Contains(targetAnchor);
        int normalizedRange = System.Math.Max(1, attackRange);
        int attackAnchors = 0;
        int reachableAttackAnchors = 0;
        int nearestGap = int.MaxValue;
        BattleGridCoord nearestReachable = default;
        bool hasNearestReachable = false;

        foreach (BattleGridCoord coord in _topologyNodeMoveCosts.Keys)
        {
            int gap = BattleActorFootprint.GetGap(actor, coord, target, targetAnchor);
            if (gap <= normalizedRange)
            {
                attackAnchors++;
                if (reachable.Contains(coord))
                {
                    reachableAttackAnchors++;
                }
            }

            if (reachable.Contains(coord) &&
                (gap < nearestGap ||
                 gap == nearestGap && IsBefore(coord, nearestReachable)))
            {
                nearestGap = gap;
                nearestReachable = coord;
                hasNearestReachable = true;
            }
        }

        string nearest = hasNearestReachable
            ? $"{nearestReachable}:gap={nearestGap}"
            : "none";
        return $"authored=True start={start} target={targetAnchor} startInGraph={startInGraph} targetInGraph={targetInGraph} staticComponentSize={reachable.Count} staticTargetReachable={staticTargetReachable} attackAnchors={attackAnchors} reachableAttackAnchors={reachableAttackAnchors} nearestReachable={nearest}";
    }

    public static BattleNavigationGraph Create(LocationBattleContext context, IEnumerable<BattleRuntimeActor> actors)
    {
        if (context?.NavigationTopology?.HasNodes == true)
        {
            return CreateFromTopology(context.NavigationTopology);
        }

        return CreateFromActors(actors);
    }

    public static BattleNavigationGraph CreateFromActors(IEnumerable<BattleRuntimeActor> actors)
    {
        BattleRuntimeActor[] corps = (actors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(actor => actor?.Kind == BattleRuntimeActorKind.Corps)
            .ToArray();
        if (corps.Length == 0)
        {
            return new BattleNavigationGraph(
                -DefaultBoundsMargin,
                DefaultBoundsMargin,
                -DefaultBoundsMargin,
                DefaultBoundsMargin);
        }

        int minX = corps.Min(actor => actor.GridX) - DefaultBoundsMargin;
        int maxX = corps.Max(actor => actor.GridX) + DefaultBoundsMargin;
        int minY = corps.Min(actor => actor.GridY) - DefaultBoundsMargin;
        int maxY = corps.Max(actor => actor.GridY) + DefaultBoundsMargin;
        return new BattleNavigationGraph(minX, maxX, minY, maxY);
    }

    private static BattleNavigationGraph CreateFromTopology(BattleNavigationTopology topology)
    {
        var nodes = new Dictionary<BattleGridCoord, int>();
        foreach (BattleNavigationNode node in topology?.Nodes ?? Enumerable.Empty<BattleNavigationNode>())
        {
            if (node == null)
            {
                continue;
            }

            BattleGridCoord coord = new(node.X, node.Y, node.Height);
            nodes[coord] = System.Math.Max(DefaultMoveCost, node.MoveCost);
        }

        if (nodes.Count == 0)
        {
            return CreateFromActors(System.Array.Empty<BattleRuntimeActor>());
        }

        var edges = new Dictionary<BattleGridCoord, List<GraphEdge>>();
        var reverseEdges = new Dictionary<BattleGridCoord, List<GraphEdge>>();
        var seenEdges = new HashSet<GraphEdge>();
        foreach (BattleNavigationEdge edge in topology?.Edges ?? Enumerable.Empty<BattleNavigationEdge>())
        {
            if (edge == null)
            {
                continue;
            }

            BattleGridCoord from = new(edge.FromX, edge.FromY, edge.FromHeight);
            BattleGridCoord to = new(edge.ToX, edge.ToY, edge.ToHeight);
            if (nodes.ContainsKey(from) && nodes.ContainsKey(to))
            {
                AddEdge(edges, reverseEdges, seenEdges, from, to, System.Math.Max(DefaultMoveCost, edge.MoveCost));
            }
        }

        return new BattleNavigationGraph(
            nodes.Keys.Min(coord => coord.X),
            nodes.Keys.Max(coord => coord.X),
            nodes.Keys.Min(coord => coord.Y),
            nodes.Keys.Max(coord => coord.Y),
            nodes,
            edges,
            reverseEdges);
    }

    private static void AddEdge(
        Dictionary<BattleGridCoord, List<GraphEdge>> edges,
        Dictionary<BattleGridCoord, List<GraphEdge>> reverseEdges,
        HashSet<GraphEdge> seenEdges,
        BattleGridCoord from,
        BattleGridCoord to,
        int moveCost)
    {
        GraphEdge edge = new(from, to, System.Math.Max(DefaultMoveCost, moveCost));
        if (!seenEdges.Add(edge))
        {
            return;
        }

        if (!edges.TryGetValue(from, out List<GraphEdge> outgoing))
        {
            outgoing = new List<GraphEdge>();
            edges[from] = outgoing;
        }

        outgoing.Add(edge);

        if (!reverseEdges.TryGetValue(to, out List<GraphEdge> incoming))
        {
            incoming = new List<GraphEdge>();
            reverseEdges[to] = incoming;
        }

        incoming.Add(edge);
    }

    public bool Contains(BattleGridCoord anchor)
    {
        if (UsesTopology)
        {
            return _topologyNodeMoveCosts.ContainsKey(anchor);
        }

        // Legacy requests without map context keep a bounded grid so architecture
        // tests and old callers still exercise Runtime without scene ownership.
        return anchor.X >= MinX &&
               anchor.X <= MaxX &&
               anchor.Y >= MinY &&
               anchor.Y <= MaxY;
    }

    public IEnumerable<BattleGridCoord> GetNeighbors(BattleGridCoord anchor)
    {
        if (UsesTopology)
        {
            if (_topologyEdges.TryGetValue(anchor, out List<GraphEdge> outgoing))
            {
                foreach (GraphEdge edge in outgoing)
                {
                    yield return edge.To;
                }
            }

            yield break;
        }

        foreach (BattleGridCoord offset in NeighborOffsets)
        {
            BattleGridCoord candidate = new(anchor.X + offset.X, anchor.Y + offset.Y, anchor.Height);
            if (Contains(candidate))
            {
                yield return candidate;
            }
        }
    }

    public IEnumerable<BattleGridCoord> GetIncomingNeighbors(BattleGridCoord anchor)
    {
        if (UsesTopology)
        {
            if (_reverseTopologyEdges.TryGetValue(anchor, out List<GraphEdge> incoming))
            {
                foreach (GraphEdge edge in incoming)
                {
                    yield return edge.From;
                }
            }

            yield break;
        }

        foreach (BattleGridCoord neighbor in GetNeighbors(anchor))
        {
            yield return neighbor;
        }
    }

    public IEnumerable<BattleGridCoord> GetAnchors()
    {
        if (UsesTopology)
        {
            foreach (BattleGridCoord coord in _topologyNodeMoveCosts.Keys)
            {
                yield return coord;
            }

            yield break;
        }

        for (int y = MinY; y <= MaxY; y++)
        {
            for (int x = MinX; x <= MaxX; x++)
            {
                yield return new BattleGridCoord(x, y, 0);
            }
        }
    }

    public IEnumerable<BattleGridCoord> GetAnchorsInBounds(int minX, int maxX, int minY, int maxY)
    {
        if (UsesTopology)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!_topologyHeightsByCell.TryGetValue(new GraphCell(x, y), out int[] heights))
                    {
                        continue;
                    }

                    foreach (int height in heights)
                    {
                        yield return new BattleGridCoord(x, y, height);
                    }
                }
            }

            yield break;
        }

        int clampedMinX = System.Math.Max(minX, MinX);
        int clampedMaxX = System.Math.Min(maxX, MaxX);
        int clampedMinY = System.Math.Max(minY, MinY);
        int clampedMaxY = System.Math.Min(maxY, MaxY);
        for (int y = clampedMinY; y <= clampedMaxY; y++)
        {
            for (int x = clampedMinX; x <= clampedMaxX; x++)
            {
                yield return new BattleGridCoord(x, y, 0);
            }
        }
    }

    public bool CanPlaceFootprint(BattleRuntimeActor actor, BattleGridCoord anchor)
    {
        // Runtime owns actor-specific footprint legality over topology nodes.
        // Tile layers and height-link authoring were already compiled away.
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (!Contains(cell))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanTraverseStep(BattleRuntimeActor actor, BattleGridCoord from, BattleGridCoord to)
    {
        return CanPlaceFootprint(actor, to);
    }

    public int GetStepCost(BattleGridCoord from, BattleGridCoord to, int baseStepCost)
    {
        int normalizedBaseCost = System.Math.Max(1, baseStepCost);
        // Runtime traversal is uniform over final topology. Authored terrain
        // costs are not a second pathfinding authority in this battle slice.
        return normalizedBaseCost;
    }

    private NavigationTopologySummary BuildTopologySummary()
    {
        var unvisited = new HashSet<BattleGridCoord>(_topologyNodeMoveCosts.Keys);
        int componentCount = 0;
        int largestComponentSize = 0;
        while (unvisited.Count > 0)
        {
            BattleGridCoord start = unvisited.First();
            HashSet<BattleGridCoord> reachable = CollectReachable(start);
            foreach (BattleGridCoord coord in reachable)
            {
                unvisited.Remove(coord);
            }

            componentCount++;
            largestComponentSize = System.Math.Max(largestComponentSize, reachable.Count);
        }

        return new NavigationTopologySummary(componentCount, largestComponentSize);
    }

    private HashSet<BattleGridCoord> CollectReachable(BattleGridCoord start)
    {
        var reachable = new HashSet<BattleGridCoord>();
        if (!Contains(start))
        {
            return reachable;
        }

        var frontier = new Queue<BattleGridCoord>();
        reachable.Add(start);
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            BattleGridCoord current = frontier.Dequeue();
            foreach (BattleGridCoord neighbor in GetNeighbors(current))
            {
                if (reachable.Add(neighbor))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return reachable;
    }

    private static Dictionary<GraphCell, int[]> BuildTopologyHeightsByCell(IEnumerable<BattleGridCoord> anchors)
    {
        return anchors
            .GroupBy(coord => new GraphCell(coord.X, coord.Y))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(coord => coord.Height)
                    .Distinct()
                    .OrderBy(height => height)
                    .ToArray());
    }

    private static bool IsBefore(BattleGridCoord candidate, BattleGridCoord known)
    {
        return candidate.Height < known.Height ||
               candidate.Height == known.Height && candidate.Y < known.Y ||
               candidate.Height == known.Height && candidate.Y == known.Y && candidate.X < known.X;
    }

    private readonly record struct GraphCell(int X, int Y);
    private readonly record struct GraphEdge(BattleGridCoord From, BattleGridCoord To, int MoveCost);
    private readonly record struct NavigationTopologySummary(int ComponentCount, int LargestComponentSize);
}
