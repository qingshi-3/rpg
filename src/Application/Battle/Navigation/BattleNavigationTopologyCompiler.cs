using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.Battle.Navigation;

public static class BattleNavigationTopologyCompiler
{
    private const int DefaultMoveCost = 1;

    private static readonly NavigationCoord[] NeighborOffsets =
    {
        new(1, 1, 0),
        new(1, 0, 0),
        new(0, 1, 0),
        new(1, -1, 0),
        new(-1, 1, 0),
        new(-1, 0, 0),
        new(0, -1, 0),
        new(-1, -1, 0)
    };

    public static BattleNavigationTopology Compile(
        IEnumerable<BattleNavigationSurfaceSnapshot> surfaces,
        IEnumerable<BattleNavigationConnectionSnapshot> connections)
    {
        var topology = new BattleNavigationTopology();
        var nodes = new HashSet<NavigationCoord>();
        foreach (BattleNavigationSurfaceSnapshot surface in surfaces ?? Enumerable.Empty<BattleNavigationSurfaceSnapshot>())
        {
            if (surface == null)
            {
                continue;
            }

            NavigationCoord coord = new(surface.X, surface.Y, surface.Height);
            if (!nodes.Add(coord))
            {
                continue;
            }

            topology.Nodes.Add(new BattleNavigationNode
            {
                X = coord.X,
                Y = coord.Y,
                Height = coord.Height,
                MoveCost = DefaultMoveCost
            });
        }

        var seenEdges = new HashSet<NavigationEdgeKey>();
        foreach (NavigationCoord from in nodes.OrderBy(item => item.Height).ThenBy(item => item.Y).ThenBy(item => item.X))
        {
            foreach (NavigationCoord offset in NeighborOffsets)
            {
                NavigationCoord to = new(from.X + offset.X, from.Y + offset.Y, from.Height);
                if (nodes.Contains(to) && AllowsGeneratedSameLevelEdge(from, to, nodes))
                {
                    AddEdge(topology, seenEdges, from, to, BattleNavigationEdgeKind.GeneratedSameLevel);
                }
            }
        }

        foreach (BattleNavigationConnectionSnapshot connection in connections ?? Enumerable.Empty<BattleNavigationConnectionSnapshot>())
        {
            if (connection == null)
            {
                continue;
            }

            NavigationCoord from = new(connection.FromX, connection.FromY, connection.FromHeight);
            NavigationCoord to = new(connection.ToX, connection.ToY, connection.ToHeight);
            if (nodes.Contains(from) && nodes.Contains(to))
            {
                AddEdge(topology, seenEdges, from, to, BattleNavigationEdgeKind.AuthoredConnection);
            }
        }

        return topology;
    }

    private static bool AllowsGeneratedSameLevelEdge(
        NavigationCoord from,
        NavigationCoord to,
        ISet<NavigationCoord> nodes)
    {
        if (from.X == to.X || from.Y == to.Y)
        {
            return true;
        }

        NavigationCoord horizontalSide = new(to.X, from.Y, from.Height);
        NavigationCoord verticalSide = new(from.X, to.Y, from.Height);
        return nodes.Contains(horizontalSide) && nodes.Contains(verticalSide);
    }

    private static void AddEdge(
        BattleNavigationTopology topology,
        HashSet<NavigationEdgeKey> seenEdges,
        NavigationCoord from,
        NavigationCoord to,
        BattleNavigationEdgeKind kind)
    {
        NavigationEdgeKey key = new(from, to, kind);
        if (!seenEdges.Add(key))
        {
            return;
        }

        // The compiler is the static topology boundary. Runtime receives only
        // final, normalized graph edges and layers dynamic occupancy on top.
        topology.Edges.Add(new BattleNavigationEdge
        {
            FromX = from.X,
            FromY = from.Y,
            FromHeight = from.Height,
            ToX = to.X,
            ToY = to.Y,
            ToHeight = to.Height,
            MoveCost = DefaultMoveCost,
            Kind = kind
        });
    }

    private readonly record struct NavigationCoord(int X, int Y, int Height);
    private readonly record struct NavigationEdgeKey(NavigationCoord From, NavigationCoord To, BattleNavigationEdgeKind Kind);
}
