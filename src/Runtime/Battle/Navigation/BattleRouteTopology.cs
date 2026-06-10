using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleRouteTopology
{
    private const int DefaultChunkSize = 8;
    private readonly BattleNavigationGraph _graph;
    private readonly int _chunkSize;
    private readonly Dictionary<BattleRouteProfile, ProfileRouteGraph> _profileGraphs = new();

    private BattleRouteTopology(BattleNavigationGraph graph, int chunkSize)
    {
        _graph = graph;
        _chunkSize = System.Math.Max(4, chunkSize);
        Version = graph?.UsesTopology == true ? graph.MaxSearchNodes : 0;
    }

    public int Version { get; }

    public static BattleRouteTopology Build(BattleNavigationGraph graph)
    {
        return new BattleRouteTopology(graph, DefaultChunkSize);
    }

    public BattleRouteRegionId GetRegionId(BattleGridCoord anchor)
    {
        return new BattleRouteRegionId(
            FloorDiv(anchor.X, _chunkSize),
            FloorDiv(anchor.Y, _chunkSize),
            anchor.Height);
    }

    public string Describe()
    {
        return $"routeTopology=True chunkSize={_chunkSize} version={Version} profileGraphs={_profileGraphs.Count}";
    }

    public bool TryFindRoute(BattleRouteQuery query, out BattleRouteHint hint)
    {
        hint = default;
        if (_graph == null ||
            !_graph.Contains(query.SourceAnchor) ||
            !_graph.Contains(query.TargetAnchor))
        {
            return false;
        }

        ProfileRouteGraph profileGraph = GetProfileGraph(query.Profile);
        BattleRouteRegionId sourceRegion = GetRegionId(query.SourceAnchor);
        BattleRouteRegionId targetRegion = GetRegionId(query.TargetAnchor);
        if (!profileGraph.LegalAnchors.Contains(query.SourceAnchor) ||
            !profileGraph.LegalAnchors.Contains(query.TargetAnchor) ||
            !profileGraph.RegionAnchors.ContainsKey(sourceRegion) ||
            !profileGraph.RegionAnchors.ContainsKey(targetRegion))
        {
            return false;
        }

        if (sourceRegion.Equals(targetRegion))
        {
            hint = BattleRouteHint.Create(
                query.TargetAnchor,
                BuildCorridorId(query, sourceRegion, targetRegion),
                sourceRegion,
                targetRegion,
                Version);
            return true;
        }

        return TrySearchProfileRoute(query, profileGraph, sourceRegion, targetRegion, out hint);
    }

    private ProfileRouteGraph GetProfileGraph(BattleRouteProfile profile)
    {
        if (_profileGraphs.TryGetValue(profile, out ProfileRouteGraph cached))
        {
            return cached;
        }

        ProfileRouteGraph built = BuildProfileGraph(profile);
        _profileGraphs[profile] = built;
        return built;
    }

    private ProfileRouteGraph BuildProfileGraph(BattleRouteProfile profile)
    {
        BattleRuntimeActor footprintActor = new()
        {
            FootprintWidth = profile.Width,
            FootprintHeight = profile.Height
        };
        HashSet<BattleGridCoord> legalAnchors = new();
        Dictionary<BattleRouteRegionId, List<BattleGridCoord>> regionAnchors = new();
        foreach (BattleGridCoord anchor in _graph.GetAnchors())
        {
            if (!_graph.CanPlaceFootprint(footprintActor, anchor))
            {
                continue;
            }

            legalAnchors.Add(anchor);
            BattleRouteRegionId region = GetRegionId(anchor);
            if (!regionAnchors.TryGetValue(region, out List<BattleGridCoord> anchors))
            {
                anchors = new List<BattleGridCoord>();
                regionAnchors[region] = anchors;
            }

            anchors.Add(anchor);
        }

        Dictionary<BattleRouteRegionId, List<BattleRouteEdge>> edgesByRegion = new();
        HashSet<BattleRouteEdgeKey> seenEdges = new();
        foreach (BattleGridCoord anchor in legalAnchors)
        {
            BattleRouteRegionId fromRegion = GetRegionId(anchor);
            foreach (BattleGridCoord neighbor in _graph.GetNeighbors(anchor))
            {
                if (!legalAnchors.Contains(neighbor) ||
                    !BattlePathStepRules.CanUseStaticStep(footprintActor, anchor, neighbor, _graph))
                {
                    continue;
                }

                BattleRouteRegionId toRegion = GetRegionId(neighbor);
                if (fromRegion.Equals(toRegion))
                {
                    continue;
                }

                BattleRouteEdge edge = new(fromRegion, toRegion, anchor, neighbor, GetAnchorDistance(anchor, neighbor));
                BattleRouteEdgeKey key = new(fromRegion, toRegion, anchor, neighbor);
                if (!seenEdges.Add(key))
                {
                    continue;
                }

                if (!edgesByRegion.TryGetValue(fromRegion, out List<BattleRouteEdge> edges))
                {
                    edges = new List<BattleRouteEdge>();
                    edgesByRegion[fromRegion] = edges;
                }

                edges.Add(edge);
            }
        }

        foreach (List<BattleRouteEdge> edges in edgesByRegion.Values)
        {
            edges.Sort(BattleRouteEdgeComparer.Instance);
        }

        return new ProfileRouteGraph(footprintActor, legalAnchors, regionAnchors, edgesByRegion);
    }

    private bool TrySearchProfileRoute(
        BattleRouteQuery query,
        ProfileRouteGraph profileGraph,
        BattleRouteRegionId sourceRegion,
        BattleRouteRegionId targetRegion,
        out BattleRouteHint hint)
    {
        hint = default;
        // Region id alone is not a complete route state: the same chunk can be
        // cheap or expensive depending on which portal the actor entered from.
        // Keep entry anchors in the search so a nearby backward exit cannot beat
        // a forward corridor by ignoring in-region travel.
        BattleRouteSearchNode sourceNode = new(sourceRegion, query.SourceAnchor);
        Dictionary<BattleRouteSearchNode, int> bestCost = new();
        Dictionary<BattleRouteSearchNode, BattleRouteSearchStep> previousStep = new();
        PriorityQueue<BattleRouteSearchNode, int> frontier = new();
        BattleRouteSearchNode bestTargetNode = default;
        bool hasTargetNode = false;
        int bestTargetCost = int.MaxValue;
        bestCost[sourceNode] = 0;
        frontier.Enqueue(sourceNode, 0);

        while (frontier.TryDequeue(out BattleRouteSearchNode currentNode, out int queuedCost))
        {
            if (!bestCost.TryGetValue(currentNode, out int currentCost) ||
                queuedCost != currentCost)
            {
                continue;
            }

            if (currentCost >= bestTargetCost)
            {
                break;
            }

            if (currentNode.Region.Equals(targetRegion))
            {
                if (TryGetPortalTransitionCost(
                        profileGraph,
                        targetRegion,
                        currentNode.EntryAnchor,
                        query.TargetAnchor,
                        out int targetPenalty))
                {
                    int totalCost = currentCost + targetPenalty;
                    if (!hasTargetNode || totalCost < bestTargetCost)
                    {
                        hasTargetNode = true;
                        bestTargetNode = currentNode;
                        bestTargetCost = totalCost;
                    }
                }

                continue;
            }

            if (!profileGraph.EdgesByRegion.TryGetValue(currentNode.Region, out List<BattleRouteEdge> edges))
            {
                continue;
            }

            foreach (BattleRouteEdge edge in edges)
            {
                if (!TryGetPortalTransitionCost(
                        profileGraph,
                        currentNode.Region,
                        currentNode.EntryAnchor,
                        edge.FromAnchor,
                        out int regionTravelCost))
                {
                    continue;
                }

                BattleRouteSearchNode nextNode = new(edge.ToRegion, edge.ToAnchor);
                int nextCost = currentCost + regionTravelCost + edge.Cost;
                if (bestCost.TryGetValue(nextNode, out int known) && known <= nextCost)
                {
                    continue;
                }

                bestCost[nextNode] = nextCost;
                previousStep[nextNode] = new BattleRouteSearchStep(currentNode, edge);
                frontier.Enqueue(nextNode, nextCost);
            }
        }

        if (!hasTargetNode)
        {
            return false;
        }

        BattleRouteEdge firstEdge = default;
        bool hasFirstEdge = false;
        BattleRouteSearchNode cursor = bestTargetNode;
        while (previousStep.TryGetValue(cursor, out BattleRouteSearchStep step))
        {
            firstEdge = step.Edge;
            hasFirstEdge = true;
            cursor = step.PreviousNode;
        }

        if (!hasFirstEdge)
        {
            return false;
        }

        hint = BattleRouteHint.Create(
            firstEdge.ToAnchor,
            BuildCorridorId(query, sourceRegion, targetRegion),
            sourceRegion,
            targetRegion,
            Version);
        return true;
    }

    private bool TryGetPortalTransitionCost(
        ProfileRouteGraph profileGraph,
        BattleRouteRegionId region,
        BattleGridCoord entryAnchor,
        BattleGridCoord exitAnchor,
        out int cost)
    {
        cost = 0;
        if (entryAnchor == exitAnchor)
        {
            return true;
        }

        if (!profileGraph.LegalAnchors.Contains(entryAnchor) ||
            !profileGraph.LegalAnchors.Contains(exitAnchor) ||
            !GetRegionId(entryAnchor).Equals(region) ||
            !GetRegionId(exitAnchor).Equals(region))
        {
            return false;
        }

        BattleRouteRegionTravelKey key = new(region, entryAnchor, exitAnchor);
        if (profileGraph.RegionTravelCosts.TryGetValue(key, out int cached))
        {
            cost = cached;
            return true;
        }

        Dictionary<BattleGridCoord, int> bestCost = new();
        PriorityQueue<BattleGridCoord, int> frontier = new();
        bestCost[entryAnchor] = 0;
        frontier.Enqueue(entryAnchor, 0);

        while (frontier.TryDequeue(out BattleGridCoord current, out int queuedCost))
        {
            if (!bestCost.TryGetValue(current, out int currentCost) ||
                queuedCost != currentCost)
            {
                continue;
            }

            if (current == exitAnchor)
            {
                profileGraph.RegionTravelCosts[key] = currentCost;
                cost = currentCost;
                return true;
            }

            foreach (BattleGridCoord neighbor in _graph.GetNeighbors(current))
            {
                if (!profileGraph.LegalAnchors.Contains(neighbor) ||
                    !GetRegionId(neighbor).Equals(region) ||
                    !BattlePathStepRules.CanUseStaticStep(profileGraph.FootprintActor, current, neighbor, _graph))
                {
                    continue;
                }

                int nextCost = currentCost + GetAnchorDistance(current, neighbor);
                if (bestCost.TryGetValue(neighbor, out int known) && known <= nextCost)
                {
                    continue;
                }

                bestCost[neighbor] = nextCost;
                frontier.Enqueue(neighbor, nextCost);
            }
        }

        return false;
    }

    private static string BuildCorridorId(
        BattleRouteQuery query,
        BattleRouteRegionId sourceRegion,
        BattleRouteRegionId targetRegion)
    {
        return $"{query.BattleGroupId}:{query.IntentId}:{query.Profile.Width}x{query.Profile.Height}:{sourceRegion}->{targetRegion}";
    }

    private static int GetAnchorDistance(BattleGridCoord first, BattleGridCoord second)
    {
        int dx = System.Math.Abs(first.X - second.X);
        int dy = System.Math.Abs(first.Y - second.Y);
        int dh = System.Math.Abs(first.Height - second.Height);
        return System.Math.Max(dx, dy) * BattlePathCostPolicy.StepCost +
               System.Math.Min(dx, dy) * (BattlePathCostPolicy.DiagonalStepCost - BattlePathCostPolicy.StepCost) +
               dh * BattlePathCostPolicy.StepCost * 4;
    }

    private static int FloorDiv(int value, int divisor)
    {
        return value >= 0
            ? value / divisor
            : -((-value + divisor - 1) / divisor);
    }

    private sealed class ProfileRouteGraph
    {
        public ProfileRouteGraph(
            BattleRuntimeActor footprintActor,
            HashSet<BattleGridCoord> legalAnchors,
            Dictionary<BattleRouteRegionId, List<BattleGridCoord>> regionAnchors,
            Dictionary<BattleRouteRegionId, List<BattleRouteEdge>> edgesByRegion)
        {
            FootprintActor = footprintActor;
            LegalAnchors = legalAnchors;
            RegionAnchors = regionAnchors;
            EdgesByRegion = edgesByRegion;
        }

        public BattleRuntimeActor FootprintActor { get; }
        public HashSet<BattleGridCoord> LegalAnchors { get; }
        public Dictionary<BattleRouteRegionId, List<BattleGridCoord>> RegionAnchors { get; }
        public Dictionary<BattleRouteRegionId, List<BattleRouteEdge>> EdgesByRegion { get; }
        public Dictionary<BattleRouteRegionTravelKey, int> RegionTravelCosts { get; } = new();
    }

    private readonly record struct BattleRouteSearchNode(
        BattleRouteRegionId Region,
        BattleGridCoord EntryAnchor);

    private readonly record struct BattleRouteSearchStep(
        BattleRouteSearchNode PreviousNode,
        BattleRouteEdge Edge);

    private readonly record struct BattleRouteRegionTravelKey(
        BattleRouteRegionId Region,
        BattleGridCoord FromAnchor,
        BattleGridCoord ToAnchor);

    private readonly record struct BattleRouteEdge(
        BattleRouteRegionId FromRegion,
        BattleRouteRegionId ToRegion,
        BattleGridCoord FromAnchor,
        BattleGridCoord ToAnchor,
        int Cost);

    private readonly record struct BattleRouteEdgeKey(
        BattleRouteRegionId FromRegion,
        BattleRouteRegionId ToRegion,
        BattleGridCoord FromAnchor,
        BattleGridCoord ToAnchor);

    private sealed class BattleRouteEdgeComparer : IComparer<BattleRouteEdge>
    {
        public static readonly BattleRouteEdgeComparer Instance = new();

        public int Compare(BattleRouteEdge first, BattleRouteEdge second)
        {
            int fromCompare = CompareCoord(first.FromAnchor, second.FromAnchor);
            return fromCompare != 0 ? fromCompare : CompareCoord(first.ToAnchor, second.ToAnchor);
        }

        private static int CompareCoord(BattleGridCoord first, BattleGridCoord second)
        {
            int height = first.Height.CompareTo(second.Height);
            if (height != 0)
            {
                return height;
            }

            int y = first.Y.CompareTo(second.Y);
            return y != 0 ? y : first.X.CompareTo(second.X);
        }
    }
}

internal readonly record struct BattleRouteProfile(int Width, int Height)
{
    public static BattleRouteProfile FromActor(BattleRuntimeActor actor)
    {
        return new BattleRouteProfile(
            BattleActorFootprint.NormalizeSize(actor?.FootprintWidth ?? 1),
            BattleActorFootprint.NormalizeSize(actor?.FootprintHeight ?? 1));
    }
}

internal readonly record struct BattleRouteRegionId(int X, int Y, int Height)
{
    public override string ToString()
    {
        return $"{X},{Y},{Height}";
    }
}

internal readonly record struct BattleRouteQuery(
    string BattleGroupId,
    string IntentId,
    BattleGridCoord SourceAnchor,
    BattleGridCoord TargetAnchor,
    int TargetWidth,
    int TargetHeight,
    BattleRouteProfile Profile)
{
    public static BattleRouteQuery Create(
        string battleGroupId,
        string intentId,
        BattleRuntimeActor actor,
        BattleGridCoord targetAnchor,
        int targetWidth,
        int targetHeight)
    {
        BattleGridCoord sourceAnchor = new(actor?.GridX ?? 0, actor?.GridY ?? 0, actor?.GridHeight ?? 0);
        return new BattleRouteQuery(
            battleGroupId ?? "",
            intentId ?? "",
            sourceAnchor,
            targetAnchor,
            System.Math.Max(1, targetWidth),
            System.Math.Max(1, targetHeight),
            BattleRouteProfile.FromActor(actor));
    }
}

internal readonly record struct BattleRouteHint(
    BattleGridCoord Anchor,
    string CorridorId,
    string SourceRegionId,
    string TargetRegionId,
    int TopologyVersion)
{
    public static BattleRouteHint Create(
        BattleGridCoord anchor,
        string corridorId,
        BattleRouteRegionId sourceRegion,
        BattleRouteRegionId targetRegion,
        int topologyVersion)
    {
        return new BattleRouteHint(
            anchor,
            corridorId ?? "",
            sourceRegion.ToString(),
            targetRegion.ToString(),
            topologyVersion);
    }
}
