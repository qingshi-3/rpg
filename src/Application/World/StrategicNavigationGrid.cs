using System.Collections.Generic;
using Godot;

namespace Rpg.Application.World;

public sealed class StrategicNavigationGrid
{
    private static readonly Vector2I[] NeighborOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1)
    };

    private readonly HashSet<Vector2I> _walkableCells;

    public StrategicNavigationGrid(IEnumerable<Vector2I> walkableCells)
    {
        _walkableCells = walkableCells == null
            ? new HashSet<Vector2I>()
            : new HashSet<Vector2I>(walkableCells);
    }

    public int CellCount => _walkableCells.Count;

    public bool Contains(Vector2I cell)
    {
        return _walkableCells.Contains(cell);
    }

    public bool TryBuildCellPath(
        Vector2I start,
        Vector2I destination,
        out IReadOnlyList<Vector2I> cells,
        out string failureReason)
    {
        cells = System.Array.Empty<Vector2I>();
        failureReason = "";
        if (!Contains(start))
        {
            failureReason = $"start_cell_outside_strategic_navigation_grid cell={start}";
            return false;
        }

        if (!Contains(destination))
        {
            failureReason = $"destination_cell_outside_strategic_navigation_grid cell={destination}";
            return false;
        }

        if (start == destination)
        {
            cells = new[] { start };
            return true;
        }

        PriorityQueue<Vector2I, float> open = new();
        Dictionary<Vector2I, Vector2I> cameFrom = new();
        Dictionary<Vector2I, float> bestCost = new()
        {
            [start] = 0.0f
        };
        open.Enqueue(start, EstimateCost(start, destination));

        while (open.Count > 0)
        {
            Vector2I current = open.Dequeue();
            if (current == destination)
            {
                cells = ReconstructPath(cameFrom, current);
                return true;
            }

            foreach (Vector2I neighbor in EnumerateNeighbors(current))
            {
                float nextCost = bestCost[current] + MovementCost(current, neighbor);
                if (bestCost.TryGetValue(neighbor, out float knownCost) && nextCost >= knownCost)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                bestCost[neighbor] = nextCost;
                open.Enqueue(neighbor, nextCost + EstimateCost(neighbor, destination));
            }
        }

        failureReason = "strategic_grid_path_missing";
        return false;
    }

    private IEnumerable<Vector2I> EnumerateNeighbors(Vector2I cell)
    {
        foreach (Vector2I offset in NeighborOffsets)
        {
            Vector2I neighbor = cell + offset;
            if (!Contains(neighbor))
            {
                continue;
            }

            if (offset.X != 0 && offset.Y != 0 &&
                (!Contains(cell + new Vector2I(offset.X, 0)) ||
                 !Contains(cell + new Vector2I(0, offset.Y))))
            {
                continue;
            }

            yield return neighbor;
        }
    }

    private static IReadOnlyList<Vector2I> ReconstructPath(
        IReadOnlyDictionary<Vector2I, Vector2I> cameFrom,
        Vector2I current)
    {
        List<Vector2I> path = new() { current };
        while (cameFrom.TryGetValue(current, out Vector2I previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static float MovementCost(Vector2I from, Vector2I to)
    {
        return from.X != to.X && from.Y != to.Y ? 1.4142135f : 1.0f;
    }

    private static float EstimateCost(Vector2I from, Vector2I to)
    {
        int dx = Mathf.Abs(from.X - to.X);
        int dy = Mathf.Abs(from.Y - to.Y);
        int diagonal = Mathf.Min(dx, dy);
        int straight = Mathf.Max(dx, dy) - diagonal;
        return diagonal * 1.4142135f + straight;
    }
}
