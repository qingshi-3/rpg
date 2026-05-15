using System.Collections.Generic;
using Godot;

namespace Rpg.Application.World;

public sealed class StrategicNavigationContext : IStrategicNavigationContext
{
    private const float PointMergeDistanceSquared = 0.001f;
    private readonly bool _isAvailable;
    private readonly TileMapLayer _navigationTileLayer;
    private readonly Node2D _mapRoot;
    private readonly StrategicNavigationGrid _grid;
    private readonly string _unavailableReason;

    private StrategicNavigationContext(
        bool isAvailable,
        TileMapLayer navigationTileLayer,
        Node2D mapRoot,
        StrategicNavigationGrid grid,
        string unavailableReason,
        int version)
    {
        _isAvailable = isAvailable;
        _navigationTileLayer = navigationTileLayer;
        _mapRoot = mapRoot;
        _grid = grid ?? new StrategicNavigationGrid(null);
        _unavailableReason = unavailableReason ?? "";
        Version = version;
    }

    public int Version { get; }
    public string PrimaryProviderId => _isAvailable ? "strategic_grid" : "unavailable";

    public bool IsSynchronized(out string failureReason)
    {
        failureReason = "";
        if (_isAvailable)
        {
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(_unavailableReason)
            ? "strategic_navigation_unavailable"
            : _unavailableReason;
        return false;
    }

    public static StrategicNavigationContext CreateUnavailable(string reason)
    {
        return new StrategicNavigationContext(false, null, null, null, reason, 0);
    }

    public static StrategicNavigationContext CreateStrategicGrid(
        TileMapLayer navigationTileLayer,
        Node2D mapRoot)
    {
        StrategicNavigationGrid grid = BuildGrid(navigationTileLayer);
        return new StrategicNavigationContext(
            true,
            navigationTileLayer,
            mapRoot,
            grid,
            "",
            ComputeNavigationDataVersion(navigationTileLayer));
    }

    public bool IsPointNavigable(Vector2 mapPoint, out string failureReason)
    {
        failureReason = "";
        if (!IsAvailable(out failureReason))
        {
            return false;
        }

        if (!IsFinite(mapPoint))
        {
            failureReason = "invalid_world_position";
            return false;
        }

        if (!TryGetNavigationCell(mapPoint, out Vector2I cell))
        {
            failureReason = "strategic_navigation_tile_layer_missing";
            return false;
        }

        if (!_grid.Contains(cell))
        {
            failureReason = $"point_outside_strategic_navigation_tile_layer cell={cell}";
            return false;
        }

        return true;
    }

    public bool TryGetNearestNavigablePoint(
        Vector2 mapPoint,
        int maxCellRadius,
        out Vector2 navigablePoint,
        out string failureReason)
    {
        navigablePoint = mapPoint;
        failureReason = "";
        if (IsPointNavigable(mapPoint, out _))
        {
            return true;
        }

        if (!IsAvailable(out failureReason))
        {
            return false;
        }

        if (!TryGetNavigationCell(mapPoint, out Vector2I originCell))
        {
            failureReason = "strategic_navigation_tile_layer_missing";
            return false;
        }

        if (!TryFindNearestPaintedCell(originCell, mapPoint, maxCellRadius, out Vector2I bestCell, out navigablePoint))
        {
            failureReason = $"nearest_navigable_point_missing origin_cell={originCell} radius={Mathf.Max(0, maxCellRadius)}";
            return false;
        }

        return TryGetNavigationCellMapPoint(bestCell, out navigablePoint);
    }

    public bool TryGetNearestReachableNavigablePoint(
        Vector2 start,
        Vector2 preferredPoint,
        int maxCellRadius,
        out Vector2 navigablePoint,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        navigablePoint = preferredPoint;
        path = null;
        failureReason = "";
        if (!IsPointNavigable(start, out failureReason))
        {
            failureReason = $"start_{failureReason}";
            return false;
        }

        if (!IsAvailable(out failureReason))
        {
            return false;
        }

        if (!TryGetNavigationCell(preferredPoint, out Vector2I originCell))
        {
            failureReason = "strategic_navigation_tile_layer_missing";
            return false;
        }

        List<(Vector2I Cell, Vector2 Point, float DistanceSquared)> candidates = new();
        int searchRadius = Mathf.Max(0, maxCellRadius);
        for (int y = originCell.Y - searchRadius; y <= originCell.Y + searchRadius; y++)
        {
            for (int x = originCell.X - searchRadius; x <= originCell.X + searchRadius; x++)
            {
                Vector2I cell = new(x, y);
                if (!_grid.Contains(cell) || !TryGetNavigationCellMapPoint(cell, out Vector2 point))
                {
                    continue;
                }

                candidates.Add((cell, point, point.DistanceSquaredTo(preferredPoint)));
            }
        }

        candidates.Sort((left, right) =>
        {
            int distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
            if (distance != 0)
            {
                return distance;
            }

            int y = left.Cell.Y.CompareTo(right.Cell.Y);
            return y != 0 ? y : left.Cell.X.CompareTo(right.Cell.X);
        });

        foreach ((_, Vector2 candidate, _) in candidates)
        {
            if (!TryBuildPath(start, candidate, out path, out _))
            {
                continue;
            }

            navigablePoint = candidate;
            return true;
        }

        failureReason = $"nearest_reachable_navigable_point_missing origin_cell={originCell} radius={searchRadius}";
        return false;
    }

    public bool TryBuildPath(Vector2 start, Vector2 destination, out StrategicNavigationPath path, out string failureReason)
    {
        path = new StrategicNavigationPath { ProviderId = PrimaryProviderId };
        failureReason = "";
        if (!IsAvailable(out failureReason))
        {
            return false;
        }

        if (!IsFinite(start) || !IsFinite(destination))
        {
            failureReason = "invalid_world_position";
            return false;
        }

        if (!TryGetNavigationCell(start, out Vector2I startCell) ||
            !TryGetNavigationCell(destination, out Vector2I destinationCell))
        {
            failureReason = "strategic_navigation_tile_layer_missing";
            return false;
        }

        if (!_grid.TryBuildCellPath(startCell, destinationCell, out IReadOnlyList<Vector2I> cells, out failureReason))
        {
            return false;
        }

        AddPointIfDistinct(path, start);
        if (cells.Count == 1)
        {
            AddPointIfDistinct(path, destination);
            return true;
        }

        for (int index = 1; index < cells.Count - 1; index++)
        {
            if (TryGetNavigationCellMapPoint(cells[index], out Vector2 point))
            {
                AddPointIfDistinct(path, point);
            }
        }

        AddPointIfDistinct(path, destination);
        return true;
    }

    private bool IsAvailable(out string failureReason)
    {
        failureReason = "";
        if (_isAvailable && _navigationTileLayer != null && _mapRoot != null && _grid.CellCount > 0)
        {
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(_unavailableReason)
            ? "strategic_navigation_unavailable"
            : _unavailableReason;
        return false;
    }

    private bool TryFindNearestPaintedCell(
        Vector2I originCell,
        Vector2 mapPoint,
        int maxCellRadius,
        out Vector2I bestCell,
        out Vector2 bestPoint)
    {
        bestCell = default;
        bestPoint = mapPoint;
        int searchRadius = Mathf.Max(0, maxCellRadius);
        bool hasCandidate = false;
        float bestDistanceSquared = float.PositiveInfinity;
        for (int y = originCell.Y - searchRadius; y <= originCell.Y + searchRadius; y++)
        {
            for (int x = originCell.X - searchRadius; x <= originCell.X + searchRadius; x++)
            {
                Vector2I cell = new(x, y);
                if (!_grid.Contains(cell) || !TryGetNavigationCellMapPoint(cell, out Vector2 candidate))
                {
                    continue;
                }

                float distanceSquared = candidate.DistanceSquaredTo(mapPoint);
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestCell = cell;
                bestPoint = candidate;
                hasCandidate = true;
            }
        }

        return hasCandidate;
    }

    private bool TryGetNavigationCell(Vector2 mapPoint, out Vector2I cell)
    {
        cell = default;
        if (_navigationTileLayer == null || _mapRoot == null)
        {
            return false;
        }

        Vector2 globalPoint = _mapRoot.ToGlobal(mapPoint);
        Vector2 layerLocalPoint = _navigationTileLayer.ToLocal(globalPoint);
        cell = _navigationTileLayer.LocalToMap(layerLocalPoint);
        return true;
    }

    private bool TryGetNavigationCellMapPoint(Vector2I cell, out Vector2 mapPoint)
    {
        mapPoint = default;
        if (_navigationTileLayer == null || _mapRoot == null)
        {
            return false;
        }

        Vector2 layerLocalPoint = _navigationTileLayer.MapToLocal(cell);
        Vector2 globalPoint = _navigationTileLayer.ToGlobal(layerLocalPoint);
        mapPoint = _mapRoot.ToLocal(globalPoint);
        return IsFinite(mapPoint);
    }

    private static void AddPointIfDistinct(StrategicNavigationPath path, Vector2 point)
    {
        if (path.Points.Count == 0 || path.Points[^1].DistanceSquaredTo(point) > PointMergeDistanceSquared)
        {
            path.Points.Add(point);
        }
    }

    private static StrategicNavigationGrid BuildGrid(TileMapLayer navigationTileLayer)
    {
        List<Vector2I> cells = new();
        if (navigationTileLayer == null)
        {
            return new StrategicNavigationGrid(cells);
        }

        foreach (Vector2I cell in navigationTileLayer.GetUsedCells())
        {
            if (navigationTileLayer.GetCellSourceId(cell) >= 0)
            {
                cells.Add(cell);
            }
        }

        return new StrategicNavigationGrid(cells);
    }

    private static int ComputeNavigationDataVersion(TileMapLayer navigationTileLayer)
    {
        if (navigationTileLayer == null)
        {
            return 0;
        }

        List<Vector2I> cells = new();
        foreach (Vector2I cell in navigationTileLayer.GetUsedCells())
        {
            cells.Add(cell);
        }

        cells.Sort((left, right) =>
        {
            int y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        });

        unchecked
        {
            int hash = 17;
            foreach (Vector2I cell in cells)
            {
                Vector2I atlas = navigationTileLayer.GetCellAtlasCoords(cell);
                hash = hash * 31 + cell.X;
                hash = hash * 31 + cell.Y;
                hash = hash * 31 + navigationTileLayer.GetCellSourceId(cell);
                hash = hash * 31 + atlas.X;
                hash = hash * 31 + atlas.Y;
                hash = hash * 31 + navigationTileLayer.GetCellAlternativeTile(cell);
            }

            return hash == 0 ? 1 : hash;
        }
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
