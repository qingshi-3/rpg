using System.Collections.Generic;
using System.Linq;

namespace Rpg.Domain.Battle.Grid;

public sealed class MovementRangeResult
{
    public MovementRangeResult(
        GridSurfacePosition startSurface,
        bool startCellExists,
        bool startWalkable,
        IReadOnlyDictionary<GridSurfacePosition, int> reachableSurfaceCosts,
        IReadOnlyDictionary<GridSurfacePosition, GridSurfacePosition> previousSurfaces,
        IReadOnlyCollection<GridSurfacePosition> destinationSurfaces)
    {
        StartSurface = startSurface;
        Start = startSurface.Position;
        StartCellExists = startCellExists;
        StartWalkable = startWalkable;
        ReachableSurfaceCosts = reachableSurfaceCosts;
        PreviousSurfaces = previousSurfaces;
        DestinationSurfaces = destinationSurfaces;
        ReachableCosts = BuildCellCostView(reachableSurfaceCosts);
        DestinationCells = destinationSurfaces
            .Select(surface => surface.Position)
            .Distinct()
            .ToArray();
    }

    public GridSurfacePosition StartSurface { get; }
    public GridPosition Start { get; }
    public bool StartCellExists { get; }
    public bool StartWalkable { get; }
    public IReadOnlyDictionary<GridSurfacePosition, int> ReachableSurfaceCosts { get; }
    public IReadOnlyDictionary<GridSurfacePosition, GridSurfacePosition> PreviousSurfaces { get; }
    public IReadOnlyCollection<GridSurfacePosition> DestinationSurfaces { get; }
    public IReadOnlyDictionary<GridPosition, int> ReachableCosts { get; }
    public IReadOnlyCollection<GridPosition> DestinationCells { get; }
    public bool HasValidStart => StartCellExists && StartWalkable;

    public bool TryGetBestDestinationSurface(GridPosition position, out GridSurfacePosition surfacePosition)
    {
        GridSurfacePosition best = default;
        int bestCost = int.MaxValue;

        foreach (GridSurfacePosition candidate in DestinationSurfaces)
        {
            if (candidate.Position != position ||
                !ReachableSurfaceCosts.TryGetValue(candidate, out int cost) ||
                cost >= bestCost)
            {
                continue;
            }

            best = candidate;
            bestCost = cost;
        }

        surfacePosition = best;
        return bestCost != int.MaxValue;
    }

    public bool TryBuildPathTo(GridPosition position, out IReadOnlyList<GridSurfacePosition> path)
    {
        if (!TryGetBestDestinationSurface(position, out GridSurfacePosition surfacePosition))
        {
            path = System.Array.Empty<GridSurfacePosition>();
            return false;
        }

        return TryBuildPathTo(surfacePosition, out path);
    }

    public bool TryBuildPathTo(GridSurfacePosition destination, out IReadOnlyList<GridSurfacePosition> path)
    {
        if (!ReachableSurfaceCosts.ContainsKey(destination))
        {
            path = System.Array.Empty<GridSurfacePosition>();
            return false;
        }

        var reversedPath = new List<GridSurfacePosition>();
        GridSurfacePosition current = destination;
        reversedPath.Add(current);

        while (current != StartSurface)
        {
            if (!PreviousSurfaces.TryGetValue(current, out GridSurfacePosition previous))
            {
                path = System.Array.Empty<GridSurfacePosition>();
                return false;
            }

            current = previous;
            reversedPath.Add(current);
        }

        reversedPath.Reverse();
        path = reversedPath;
        return true;
    }

    private static IReadOnlyDictionary<GridPosition, int> BuildCellCostView(
        IReadOnlyDictionary<GridSurfacePosition, int> reachableSurfaceCosts)
    {
        var costs = new Dictionary<GridPosition, int>();

        foreach ((GridSurfacePosition surface, int cost) in reachableSurfaceCosts)
        {
            GridPosition position = surface.Position;
            if (!costs.TryGetValue(position, out int knownCost) || cost < knownCost)
            {
                costs[position] = cost;
            }
        }

        return costs;
    }
}
