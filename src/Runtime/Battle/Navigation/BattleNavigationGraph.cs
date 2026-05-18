using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

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

    private readonly Dictionary<BattleGridCoord, int> _authoredSurfaceMoveCosts;
    private readonly Dictionary<BattleGridCoord, List<BattleNavigationEdge>> _authoredEdges;

    private BattleNavigationGraph(
        int minX,
        int maxX,
        int minY,
        int maxY,
        Dictionary<BattleGridCoord, int> authoredSurfaceMoveCosts = null,
        Dictionary<BattleGridCoord, List<BattleNavigationEdge>> authoredEdges = null)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        _authoredSurfaceMoveCosts = authoredSurfaceMoveCosts ?? new Dictionary<BattleGridCoord, int>();
        _authoredEdges = authoredEdges ?? new Dictionary<BattleGridCoord, List<BattleNavigationEdge>>();
    }

    public int MinX { get; }
    public int MaxX { get; }
    public int MinY { get; }
    public int MaxY { get; }
    public bool UsesAuthoredSurfaces => _authoredSurfaceMoveCosts.Count > 0;
    public int MaxSearchNodes => UsesAuthoredSurfaces
        ? _authoredSurfaceMoveCosts.Count
        : System.Math.Max(1, (MaxX - MinX + 1) * (MaxY - MinY + 1));

    public static BattleNavigationGraph Create(LocationBattleContext context, IEnumerable<BattleRuntimeActor> actors)
    {
        if (context?.NavigationSurfaces?.Count > 0)
        {
            return CreateFromLocationContext(context);
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

    private static BattleNavigationGraph CreateFromLocationContext(LocationBattleContext context)
    {
        var surfaces = new Dictionary<BattleGridCoord, int>();
        foreach (BattleNavigationSurfaceSnapshot surface in context.NavigationSurfaces ?? Enumerable.Empty<BattleNavigationSurfaceSnapshot>())
        {
            if (surface == null)
            {
                continue;
            }

            BattleGridCoord coord = new(surface.X, surface.Y, surface.Height);
            surfaces[coord] = System.Math.Max(DefaultMoveCost, surface.MoveCost);
        }

        if (surfaces.Count == 0)
        {
            return CreateFromActors(System.Array.Empty<BattleRuntimeActor>());
        }

        var edges = new Dictionary<BattleGridCoord, List<BattleNavigationEdge>>();
        var seenEdges = new HashSet<BattleNavigationEdge>();
        foreach (BattleGridCoord surface in surfaces.Keys)
        {
            foreach (BattleGridCoord offset in NeighborOffsets)
            {
                BattleGridCoord neighbor = new(surface.X + offset.X, surface.Y + offset.Y, surface.Height);
                if (surfaces.TryGetValue(neighbor, out int neighborMoveCost))
                {
                    AddEdge(edges, seenEdges, surface, neighbor, neighborMoveCost);
                }
            }
        }

        foreach (BattleNavigationConnectionSnapshot connection in context.NavigationConnections ?? Enumerable.Empty<BattleNavigationConnectionSnapshot>())
        {
            if (connection == null)
            {
                continue;
            }

            BattleGridCoord from = new(connection.FromX, connection.FromY, connection.FromHeight);
            BattleGridCoord to = new(connection.ToX, connection.ToY, connection.ToHeight);
            if (surfaces.ContainsKey(from) && surfaces.ContainsKey(to))
            {
                AddEdge(edges, seenEdges, from, to, System.Math.Max(DefaultMoveCost, connection.MoveCost));
            }
        }

        return new BattleNavigationGraph(
            surfaces.Keys.Min(coord => coord.X),
            surfaces.Keys.Max(coord => coord.X),
            surfaces.Keys.Min(coord => coord.Y),
            surfaces.Keys.Max(coord => coord.Y),
            surfaces,
            edges);
    }

    private static void AddEdge(
        Dictionary<BattleGridCoord, List<BattleNavigationEdge>> edges,
        HashSet<BattleNavigationEdge> seenEdges,
        BattleGridCoord from,
        BattleGridCoord to,
        int moveCost)
    {
        BattleNavigationEdge edge = new(from, to, System.Math.Max(DefaultMoveCost, moveCost));
        if (!seenEdges.Add(edge))
        {
            return;
        }

        if (!edges.TryGetValue(from, out List<BattleNavigationEdge> outgoing))
        {
            outgoing = new List<BattleNavigationEdge>();
            edges[from] = outgoing;
        }

        outgoing.Add(edge);
    }

    public bool Contains(BattleGridCoord anchor)
    {
        if (UsesAuthoredSurfaces)
        {
            return _authoredSurfaceMoveCosts.ContainsKey(anchor);
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
        if (UsesAuthoredSurfaces)
        {
            if (_authoredEdges.TryGetValue(anchor, out List<BattleNavigationEdge> outgoing))
            {
                foreach (BattleNavigationEdge edge in outgoing)
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

    public int GetStepCost(BattleGridCoord from, BattleGridCoord to, int baseStepCost)
    {
        int normalizedBaseCost = System.Math.Max(1, baseStepCost);
        if (!UsesAuthoredSurfaces)
        {
            return normalizedBaseCost;
        }

        if (_authoredEdges.TryGetValue(from, out List<BattleNavigationEdge> outgoing))
        {
            foreach (BattleNavigationEdge edge in outgoing)
            {
                if (edge.To == to)
                {
                    return normalizedBaseCost * System.Math.Max(DefaultMoveCost, edge.MoveCost);
                }
            }
        }

        return _authoredSurfaceMoveCosts.TryGetValue(to, out int moveCost)
            ? normalizedBaseCost * System.Math.Max(DefaultMoveCost, moveCost)
            : normalizedBaseCost;
    }

    private readonly record struct BattleNavigationEdge(BattleGridCoord From, BattleGridCoord To, int MoveCost);
}
