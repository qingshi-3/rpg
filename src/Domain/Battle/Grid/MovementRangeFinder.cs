using System.Collections.Generic;
using System.Linq;

namespace Rpg.Domain.Battle.Grid;

public static class MovementRangeFinder
{
    private static readonly GridPosition[] NeighborOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    public static MovementRangeResult FindReachableCells(
        BattleGridMap gridMap,
        GridSurfacePosition start,
        int maxMoveCost,
        ISet<GridSurfacePosition> blockedSurfaces = null,
        System.Func<GridCellSurface, bool> canEnterSurface = null)
    {
        if (gridMap == null || !gridMap.TryGetSurface(start, out GridCellSurface startSurface))
        {
            return Empty(start, false, false);
        }

        if (!gridMap.IsTopSurface(start))
        {
            return Empty(start, true, false);
        }

        bool startWalkable = CanEnter(startSurface, canEnterSurface);

        var costs = new Dictionary<GridSurfacePosition, int>
        {
            [start] = 0
        };
        var previous = new Dictionary<GridSurfacePosition, GridSurfacePosition>();
        var frontier = new PriorityQueue<GridSurfacePosition, int>();
        frontier.Enqueue(start, 0);

        while (frontier.Count > 0)
        {
            GridSurfacePosition current = frontier.Dequeue();
            int currentCost = costs[current];

            foreach (GridSurfaceConnection edge in EnumerateOutgoingEdges(gridMap, current))
            {
                GridSurfacePosition neighbor = edge.Target;
                bool isExplicitConnection = !string.IsNullOrWhiteSpace(edge.ConnectionId);
                if (blockedSurfaces != null && blockedSurfaces.Contains(neighbor))
                {
                    continue;
                }

                if (!gridMap.TryGetSurface(neighbor, out GridCellSurface nextSurface) ||
                    !gridMap.TryGetSurface(current, out GridCellSurface currentSurface) ||
                    !gridMap.IsTopSurface(neighbor) ||
                    !CanEnter(currentSurface, nextSurface, canEnterSurface, isExplicitConnection))
                {
                    continue;
                }

                int edgeCost = edge.MoveCost > 0 ? edge.MoveCost : nextSurface.MoveCost;
                int nextCost = currentCost + edgeCost;
                if (nextCost > maxMoveCost)
                {
                    continue;
                }

                if (costs.TryGetValue(neighbor, out int knownCost) && knownCost <= nextCost)
                {
                    continue;
                }

                costs[neighbor] = nextCost;
                previous[neighbor] = current;
                frontier.Enqueue(neighbor, nextCost);
            }
        }

        GridSurfacePosition[] destinations = costs
            .Where(entry => entry.Key != start &&
                            gridMap.TryGetSurface(entry.Key, out GridCellSurface surface) &&
                            gridMap.IsTopSurface(entry.Key) &&
                            CanEnter(surface, canEnterSurface))
            .Select(entry => entry.Key)
            .ToArray();

        return new MovementRangeResult(
            start,
            startCellExists: true,
            startWalkable: startWalkable,
            reachableSurfaceCosts: costs,
            previousSurfaces: previous,
            destinationSurfaces: destinations);
    }

    private static MovementRangeResult Empty(
        GridSurfacePosition start,
        bool startCellExists,
        bool startWalkable)
    {
        return new MovementRangeResult(
            start,
            startCellExists,
            startWalkable,
            new Dictionary<GridSurfacePosition, int>(),
            new Dictionary<GridSurfacePosition, GridSurfacePosition>(),
            System.Array.Empty<GridSurfacePosition>());
    }

    private static IEnumerable<GridSurfaceConnection> EnumerateOutgoingEdges(
        BattleGridMap gridMap,
        GridSurfacePosition position)
    {
        foreach (GridPosition offset in NeighborOffsets)
        {
            yield return new GridSurfaceConnection(
                new GridSurfacePosition(position.X + offset.X, position.Y + offset.Y, position.Height),
                0,
                "");
        }

        foreach (GridSurfaceConnection connection in gridMap.GetSurfaceConnections(position))
        {
            yield return connection;
        }
    }

    private static bool CanEnter(
        GridCellSurface currentSurface,
        GridCellSurface nextSurface,
        System.Func<GridCellSurface, bool> canEnterSurface,
        bool isExplicitConnection)
    {
        if (!CanEnter(nextSurface, canEnterSurface))
        {
            return false;
        }

        return currentSurface.Height == nextSurface.Height || isExplicitConnection;
    }

    private static bool CanEnter(GridCellSurface surface, System.Func<GridCellSurface, bool> canEnterSurface)
    {
        if (!surface.IsWalkable || surface.MoveCost <= 0)
        {
            return false;
        }

        return canEnterSurface == null || canEnterSurface(surface);
    }
}
