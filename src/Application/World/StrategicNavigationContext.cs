using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicNavigationContext
{
    private const float PointMergeDistanceSquared = 0.001f;
    private const float EndpointTolerance = 8.0f;
    private const float PathValidationSampleDistance = 8.0f;
    private const int TilePathMaxExpandedCells = 20000;
    private static int NextVersion = 1;
    private static readonly Vector2I[] TilePathNeighborOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    private readonly bool _isAvailable;
    private readonly Rid _navigationMap;
    private readonly TileMapLayer _navigationTileLayer;
    private readonly Node2D _mapRoot;
    private readonly string _unavailableReason;

    private StrategicNavigationContext(
        bool isAvailable,
        Rid navigationMap,
        TileMapLayer navigationTileLayer,
        Node2D mapRoot,
        string unavailableReason)
    {
        _isAvailable = isAvailable;
        _navigationMap = navigationMap;
        _navigationTileLayer = navigationTileLayer;
        _mapRoot = mapRoot;
        _unavailableReason = unavailableReason ?? "";
        Version = NextVersion++;
    }

    public int Version { get; }
    public string PrimaryProviderId => _isAvailable ? "godot_navigation" : "unavailable";

    public static StrategicNavigationContext CreateUnavailable(string reason)
    {
        return new StrategicNavigationContext(false, default, null, null, reason);
    }

    public static StrategicNavigationContext CreateGodotNavigation(
        Rid navigationMap,
        TileMapLayer navigationTileLayer,
        Node2D mapRoot)
    {
        return new StrategicNavigationContext(true, navigationMap, navigationTileLayer, mapRoot, "");
    }

    public bool IsPointNavigable(Vector2 mapPoint, out string failureReason)
    {
        failureReason = "";
        if (!_isAvailable)
        {
            failureReason = string.IsNullOrWhiteSpace(_unavailableReason)
                ? "strategic_navigation_unavailable"
                : _unavailableReason;
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

        if (!IsNavigationCellPainted(cell))
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

        if (!_isAvailable)
        {
            failureReason = string.IsNullOrWhiteSpace(_unavailableReason)
                ? "strategic_navigation_unavailable"
                : _unavailableReason;
            return false;
        }

        if (!TryGetNavigationCell(mapPoint, out Vector2I originCell))
        {
            failureReason = "strategic_navigation_tile_layer_missing";
            return false;
        }

        int searchRadius = Mathf.Max(0, maxCellRadius);
        bool hasCandidate = false;
        float bestDistanceSquared = float.PositiveInfinity;
        Vector2 bestPoint = mapPoint;
        for (int y = originCell.Y - searchRadius; y <= originCell.Y + searchRadius; y++)
        {
            for (int x = originCell.X - searchRadius; x <= originCell.X + searchRadius; x++)
            {
                Vector2I cell = new(x, y);
                if (!IsNavigationCellPainted(cell) ||
                    !TryGetNavigationCellMapPoint(cell, out Vector2 candidate))
                {
                    continue;
                }

                float distanceSquared = candidate.DistanceSquaredTo(mapPoint);
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestPoint = candidate;
                hasCandidate = true;
            }
        }

        if (!hasCandidate)
        {
            failureReason = $"nearest_navigable_point_missing origin_cell={originCell} radius={searchRadius}";
            return false;
        }

        navigablePoint = bestPoint;
        return true;
    }

    public bool TryBuildPath(Vector2 start, Vector2 destination, out StrategicNavigationPath path, out string failureReason)
    {
        path = new StrategicNavigationPath { ProviderId = PrimaryProviderId };
        failureReason = "";

        if (!_isAvailable)
        {
            failureReason = string.IsNullOrWhiteSpace(_unavailableReason)
                ? "strategic_navigation_unavailable"
                : _unavailableReason;
            return false;
        }

        if (!IsFinite(start) || !IsFinite(destination))
        {
            failureReason = "invalid_world_position";
            return false;
        }

        if (!IsPointNavigable(start, out failureReason))
        {
            failureReason = $"start_{failureReason}";
            return false;
        }

        if (!IsPointNavigable(destination, out failureReason))
        {
            failureReason = $"destination_{failureReason}";
            return false;
        }

        if (_mapRoot == null)
        {
            failureReason = "strategic_navigation_map_root_missing";
            return false;
        }

        Vector2 globalStart = _mapRoot.ToGlobal(start);
        Vector2 globalDestination = _mapRoot.ToGlobal(destination);
        var globalPath = NavigationServer2D.MapGetPath(_navigationMap, globalStart, globalDestination, true);

        foreach (Vector2 globalPoint in globalPath)
        {
            AddPointIfDistinct(path, _mapRoot.ToLocal(globalPoint));
        }

        if (path.Points.Count == 0)
        {
            failureReason = "godot_navigation_path_empty";
            return TryBuildTileLayerFallbackPath(start, destination, failureReason, out path, out failureReason);
        }

        if (path.Points[0].DistanceTo(start) > EndpointTolerance)
        {
            path.Points.Clear();
            failureReason = "godot_navigation_start_outside_navigation";
            return TryBuildTileLayerFallbackPath(start, destination, failureReason, out path, out failureReason);
        }

        if (path.Points[^1].DistanceTo(destination) > EndpointTolerance)
        {
            path.Points.Clear();
            failureReason = "godot_navigation_destination_outside_navigation";
            return TryBuildTileLayerFallbackPath(start, destination, failureReason, out path, out failureReason);
        }

        if (path.Points.Count <= 1 && start.DistanceSquaredTo(destination) > PointMergeDistanceSquared)
        {
            path.Points.Clear();
            failureReason = "godot_navigation_path_empty";
            return TryBuildTileLayerFallbackPath(start, destination, failureReason, out path, out failureReason);
        }

        if (!IsPathInsideNavigationLayer(path.Points, out failureReason))
        {
            string godotFailureReason = failureReason;
            path.Points.Clear();
            return TryBuildTileLayerFallbackPath(start, destination, godotFailureReason, out path, out failureReason);
        }

        return true;
    }

    private bool TryBuildTileLayerFallbackPath(
        Vector2 start,
        Vector2 destination,
        string godotFailureReason,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        if (TryBuildTileLayerPath(start, destination, out path, out failureReason))
        {
            GameLog.Warn(
                nameof(StrategicNavigationContext),
                $"StrategicTileNavigationFallbackBuilt reason={godotFailureReason} start={start} destination={destination} points={path.Points.Count}");
            return true;
        }

        string tileFailureReason = failureReason;
        failureReason = $"{godotFailureReason}; tile_fallback={tileFailureReason}";
        GameLog.Warn(
            nameof(StrategicNavigationContext),
            $"StrategicTileNavigationFallbackFailed reason={godotFailureReason} tileReason={tileFailureReason} start={start} destination={destination}");
        return false;
    }

    private bool TryBuildTileLayerPath(
        Vector2 start,
        Vector2 destination,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        path = new StrategicNavigationPath { ProviderId = "strategic_tile_layer" };
        failureReason = "";
        if (!TryGetNavigationCell(start, out Vector2I startCell))
        {
            failureReason = "tile_navigation_start_cell_missing";
            return false;
        }

        if (!TryGetNavigationCell(destination, out Vector2I destinationCell))
        {
            failureReason = "tile_navigation_destination_cell_missing";
            return false;
        }

        if (!IsNavigationCellPainted(startCell))
        {
            failureReason = $"tile_navigation_start_cell_unpainted cell={startCell}";
            return false;
        }

        if (!IsNavigationCellPainted(destinationCell))
        {
            failureReason = $"tile_navigation_destination_cell_unpainted cell={destinationCell}";
            return false;
        }

        if (!TryFindTileCellPath(startCell, destinationCell, out List<Vector2I> cells, out failureReason))
        {
            return false;
        }

        AddPointIfDistinct(path, start);
        foreach (Vector2I cell in cells)
        {
            if (!TryGetNavigationCellMapPoint(cell, out Vector2 mapPoint))
            {
                path.Points.Clear();
                failureReason = $"tile_navigation_cell_point_missing cell={cell}";
                return false;
            }

            AddPointIfDistinct(path, mapPoint);
        }

        AddPointIfDistinct(path, destination);
        if (path.Points.Count <= 1 && start.DistanceSquaredTo(destination) > PointMergeDistanceSquared)
        {
            path.Points.Clear();
            failureReason = "tile_navigation_path_empty";
            return false;
        }

        if (!IsPathInsideNavigationLayer(path.Points, out failureReason))
        {
            path.Points.Clear();
            failureReason = $"tile_navigation_{failureReason}";
            return false;
        }

        return true;
    }

    private bool TryFindTileCellPath(
        Vector2I startCell,
        Vector2I destinationCell,
        out List<Vector2I> cells,
        out string failureReason)
    {
        cells = new List<Vector2I>();
        failureReason = "";
        if (startCell == destinationCell)
        {
            cells.Add(startCell);
            return true;
        }

        PriorityQueue<Vector2I, float> frontier = new();
        Dictionary<Vector2I, Vector2I> previousByCell = new();
        Dictionary<Vector2I, int> costByCell = new();
        HashSet<Vector2I> closed = new();
        frontier.Enqueue(startCell, 0.0f);
        costByCell[startCell] = 0;
        int expanded = 0;

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            expanded++;
            if (expanded > TilePathMaxExpandedCells)
            {
                failureReason = $"tile_navigation_search_limit start_cell={startCell} destination_cell={destinationCell} expanded={expanded}";
                return false;
            }

            if (current == destinationCell)
            {
                return ReconstructTileCellPath(startCell, destinationCell, previousByCell, cells, out failureReason);
            }

            foreach (Vector2I offset in TilePathNeighborOffsets)
            {
                Vector2I next = new(current.X + offset.X, current.Y + offset.Y);
                if (!IsNavigationCellPainted(next))
                {
                    continue;
                }

                int nextCost = costByCell[current] + 1;
                if (costByCell.TryGetValue(next, out int existingCost) && nextCost >= existingCost)
                {
                    continue;
                }

                costByCell[next] = nextCost;
                previousByCell[next] = current;
                float priority = nextCost + GetTileCellManhattanDistance(next, destinationCell);
                frontier.Enqueue(next, priority);
            }
        }

        failureReason = $"tile_navigation_path_missing start_cell={startCell} destination_cell={destinationCell} expanded={expanded}";
        return false;
    }

    private static bool ReconstructTileCellPath(
        Vector2I startCell,
        Vector2I destinationCell,
        IReadOnlyDictionary<Vector2I, Vector2I> previousByCell,
        List<Vector2I> cells,
        out string failureReason)
    {
        failureReason = "";
        cells.Clear();
        Vector2I current = destinationCell;
        cells.Add(current);
        while (current != startCell)
        {
            if (!previousByCell.TryGetValue(current, out current))
            {
                failureReason = $"tile_navigation_reconstruct_failed cell={current}";
                cells.Clear();
                return false;
            }

            cells.Add(current);
        }

        cells.Reverse();
        return true;
    }

    private static int GetTileCellManhattanDistance(Vector2I from, Vector2I to)
    {
        return System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y);
    }

    private bool IsPathInsideNavigationLayer(IReadOnlyList<Vector2> points, out string failureReason)
    {
        failureReason = "";
        if (points == null || points.Count == 0)
        {
            failureReason = "godot_navigation_path_empty";
            return false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (!IsPointNavigable(points[i], out failureReason))
            {
                failureReason = $"path_point_{i}_{failureReason}";
                return false;
            }
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            if (!IsPathSegmentInsideNavigationLayer(points[i], points[i + 1], i, out failureReason))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPathSegmentInsideNavigationLayer(Vector2 start, Vector2 end, int segmentIndex, out string failureReason)
    {
        failureReason = "";
        float distance = start.DistanceTo(end);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / PathValidationSampleDistance));
        for (int step = 0; step <= steps; step++)
        {
            float ratio = step / (float)steps;
            Vector2 point = start.Lerp(end, ratio);
            if (IsPointNavigable(point, out _))
            {
                continue;
            }

            TryGetNavigationCell(point, out Vector2I cell);
            failureReason = $"path_segment_left_strategic_navigation_tile_layer segment={segmentIndex} cell={cell} point={point}";
            return false;
        }

        return true;
    }

    private static void AddPointIfDistinct(StrategicNavigationPath path, Vector2 point)
    {
        if (path.Points.Count == 0 || path.Points[^1].DistanceSquaredTo(point) > PointMergeDistanceSquared)
        {
            path.Points.Add(point);
        }
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

    private bool IsNavigationCellPainted(Vector2I cell)
    {
        return _navigationTileLayer != null &&
               _navigationTileLayer.GetCellSourceId(cell) >= 0;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
