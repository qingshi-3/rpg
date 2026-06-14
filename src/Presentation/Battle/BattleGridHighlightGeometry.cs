using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

internal sealed class BattleGridHighlightGeometry
{
    private readonly Node2D _owner;
    private readonly Func<BattleMapLayer> _resolveCoordinateLayer;

    public BattleGridHighlightGeometry(Node2D owner, Func<BattleMapLayer> resolveCoordinateLayer)
    {
        _owner = owner;
        _resolveCoordinateLayer = resolveCoordinateLayer;
    }

    public Vector2 BuildCellCenter(GridPosition cell)
    {
        BattleMapLayer coordinateLayer = ResolveCoordinateLayer();
        var origin = new Vector2I(cell.X, cell.Y);
        return _owner.ToLocal(coordinateLayer.ToGlobal(coordinateLayer.MapToLocal(origin)));
    }

    public float GetCellHalfExtent(GridPosition cell)
    {
        Vector2 center = BuildCellCenter(cell);
        Vector2 right = BuildCellCenter(new GridPosition(cell.X + 1, cell.Y));
        Vector2 down = BuildCellCenter(new GridPosition(cell.X, cell.Y + 1));
        return Mathf.Min((right - center).Length(), (down - center).Length()) * 0.5f;
    }

    public Vector2[] BuildCellPolygon(GridPosition cell)
    {
        BattleMapLayer coordinateLayer = ResolveCoordinateLayer();
        var origin = new Vector2I(cell.X, cell.Y);
        Vector2 center = coordinateLayer.MapToLocal(origin);
        Vector2 stepX = coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => _owner.ToLocal(coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    public IEnumerable<(Vector2 Start, Vector2 End)> BuildBoundarySegments(HashSet<GridPosition> cells)
    {
        foreach (GridPosition cell in cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            Vector2[] polygon = BuildCellPolygon(cell);

            if (!cells.Contains(new GridPosition(cell.X, cell.Y - 1)))
            {
                yield return (polygon[0], polygon[1]);
            }

            if (!cells.Contains(new GridPosition(cell.X + 1, cell.Y)))
            {
                yield return (polygon[1], polygon[2]);
            }

            if (!cells.Contains(new GridPosition(cell.X, cell.Y + 1)))
            {
                yield return (polygon[2], polygon[3]);
            }

            if (!cells.Contains(new GridPosition(cell.X - 1, cell.Y)))
            {
                yield return (polygon[3], polygon[0]);
            }
        }
    }

    public Vector2[] BuildTargetLockFramePolygon(HashSet<GridPosition> cells)
    {
        // Skill target selection shares hover's footprint geometry so the
        // selectable target hint matches the unit under the cursor exactly.
        return BuildHoverFramePolygon(cells);
    }

    public Vector2[] BuildHoverFramePolygon(IEnumerable<GridPosition> cells)
    {
        GridPosition[] hoverCells = cells?.Distinct().ToArray() ?? Array.Empty<GridPosition>();
        if (hoverCells.Length == 0)
        {
            return Array.Empty<Vector2>();
        }

        if (hoverCells.Length == 1)
        {
            return BuildCellPolygon(hoverCells[0]);
        }

        // Explicit hover is used by rectangular deployment footprints; one outer frame preserves the existing hover language without tile fills.
        int minX = hoverCells.Min(cell => cell.X);
        int maxX = hoverCells.Max(cell => cell.X);
        int minY = hoverCells.Min(cell => cell.Y);
        int maxY = hoverCells.Max(cell => cell.Y);
        return BuildCellBlockPolygon(minX, minY, maxX, maxY);
    }

    public static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private Vector2[] BuildCellBlockPolygon(int minX, int minY, int maxX, int maxY)
    {
        BattleMapLayer coordinateLayer = ResolveCoordinateLayer();
        var topLeftOrigin = new Vector2I(minX, minY);
        Vector2 topLeftCenter = coordinateLayer.MapToLocal(topLeftOrigin);
        Vector2 topRightCenter = coordinateLayer.MapToLocal(new Vector2I(maxX, minY));
        Vector2 bottomRightCenter = coordinateLayer.MapToLocal(new Vector2I(maxX, maxY));
        Vector2 bottomLeftCenter = coordinateLayer.MapToLocal(new Vector2I(minX, maxY));
        Vector2 stepX = coordinateLayer.MapToLocal(new Vector2I(minX + 1, minY)) - topLeftCenter;
        Vector2 stepY = coordinateLayer.MapToLocal(new Vector2I(minX, minY + 1)) - topLeftCenter;

        Vector2[] localPoints =
        {
            topLeftCenter - (stepX + stepY) * 0.5f,
            topRightCenter + (stepX - stepY) * 0.5f,
            bottomRightCenter + (stepX + stepY) * 0.5f,
            bottomLeftCenter + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => _owner.ToLocal(coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    private BattleMapLayer ResolveCoordinateLayer()
    {
        return _resolveCoordinateLayer?.Invoke();
    }
}
