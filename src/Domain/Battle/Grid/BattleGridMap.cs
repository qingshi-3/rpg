using System.Collections.Generic;

namespace Rpg.Domain.Battle.Grid;

public sealed class BattleGridMap
{
    private readonly Dictionary<GridPosition, GridCell> _cells = new();
    private readonly Dictionary<GridSurfacePosition, GridCellSurface> _surfaces = new();
    private readonly Dictionary<GridSurfacePosition, List<GridSurfaceConnection>> _surfaceConnections = new();
    private readonly Dictionary<GridPosition, GridSurfacePosition> _topSurfacePositions = new();
    private bool _topSurfaceIndexDirty = true;

    public IReadOnlyDictionary<GridPosition, GridCell> Cells => _cells;
    public IReadOnlyDictionary<GridSurfacePosition, GridCellSurface> Surfaces => _surfaces;
    public IReadOnlyDictionary<GridPosition, GridSurfacePosition> TopSurfacePositions
    {
        get
        {
            EnsureTopSurfaceIndex();
            return _topSurfacePositions;
        }
    }

    public GridCell GetOrCreateCell(GridPosition position)
    {
        if (_cells.TryGetValue(position, out GridCell cell))
        {
            return cell;
        }

        cell = new GridCell(position);
        _cells.Add(position, cell);
        return cell;
    }

    public bool TryGetCell(GridPosition position, out GridCell cell)
    {
        return _cells.TryGetValue(position, out cell);
    }

    public GridCellSurface GetOrCreateSurface(GridPosition position, int height)
    {
        var surfacePosition = new GridSurfacePosition(position, height);
        if (_surfaces.TryGetValue(surfacePosition, out GridCellSurface surface))
        {
            return surface;
        }

        surface = new GridCellSurface(position, height);
        _surfaces.Add(surfacePosition, surface);
        _topSurfaceIndexDirty = true;
        return surface;
    }

    public bool TryGetSurface(GridSurfacePosition position, out GridCellSurface surface)
    {
        return _surfaces.TryGetValue(position, out surface);
    }

    public void RebuildTopSurfaceIndex()
    {
        _topSurfacePositions.Clear();

        foreach (GridCellSurface surface in _surfaces.Values)
        {
            if (!surface.HasFoundation)
            {
                continue;
            }

            GridPosition position = surface.Position;
            if (!_topSurfacePositions.TryGetValue(position, out GridSurfacePosition knownTop) ||
                surface.Height > knownTop.Height)
            {
                _topSurfacePositions[position] = surface.SurfacePosition;
            }
        }

        _topSurfaceIndexDirty = false;
    }

    public bool TryGetTopSurfacePosition(GridPosition position, out GridSurfacePosition surfacePosition)
    {
        EnsureTopSurfaceIndex();
        return _topSurfacePositions.TryGetValue(position, out surfacePosition);
    }

    public bool TryGetTopSurface(GridPosition position, out GridCellSurface surface)
    {
        EnsureTopSurfaceIndex();
        if (_topSurfacePositions.TryGetValue(position, out GridSurfacePosition surfacePosition))
        {
            return TryGetSurface(surfacePosition, out surface);
        }

        surface = null;
        return false;
    }

    public bool IsTopSurface(GridSurfacePosition position)
    {
        EnsureTopSurfaceIndex();
        return _topSurfacePositions.TryGetValue(position.Position, out GridSurfacePosition topSurface) &&
               topSurface == position;
    }

    private void EnsureTopSurfaceIndex()
    {
        if (_topSurfaceIndexDirty)
        {
            RebuildTopSurfaceIndex();
        }
    }

    public void AddSurfaceConnection(
        GridSurfacePosition from,
        GridSurfacePosition to,
        int moveCost,
        string connectionId)
    {
        if (!_surfaceConnections.TryGetValue(from, out List<GridSurfaceConnection> connections))
        {
            connections = new List<GridSurfaceConnection>();
            _surfaceConnections.Add(from, connections);
        }

        connections.Add(new GridSurfaceConnection(
            to,
            System.Math.Max(1, moveCost),
            connectionId ?? ""));
    }

    public IReadOnlyList<GridSurfaceConnection> GetSurfaceConnections(GridSurfacePosition position)
    {
        return _surfaceConnections.TryGetValue(position, out List<GridSurfaceConnection> connections)
            ? connections
            : System.Array.Empty<GridSurfaceConnection>();
    }
}
