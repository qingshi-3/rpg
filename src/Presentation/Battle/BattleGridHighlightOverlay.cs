using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay : Node2D
{
    [ExportGroup("Hover")]

    [Export]
    public bool HoverEnabled { get; set; } = true;

    [Export]
    public Color HoverFillColor { get; set; } = new(1f, 1f, 1f, 0f);

    [Export]
    public Color HoverBorderColor { get; set; } = new(1f, 1f, 1f, 0.9f);

    [Export]
    public float HoverBorderWidth { get; set; } = 2.5f;

    [Export]
    public float HoverCornerLengthRatio { get; set; } = 0.22f;

    [ExportGroup("Range Colors")]

    [Export]
    public Color MoveColor { get; set; } = new(0.2f, 0.65f, 1f, 0.24f);

    [Export]
    public Color PathColor { get; set; } = new(1f, 0.82f, 0.18f, 0.34f);

    [Export]
    public Color ThreatColor { get; set; } = new(1f, 0.36f, 0.12f, 0.18f);

    [Export]
    public Color AttackColor { get; set; } = new(1f, 0.22f, 0.18f, 0.24f);

    [Export]
    public Color SkillColor { get; set; } = new(0.7f, 0.42f, 1f, 0.22f);

    [Export]
    public Color TargetColor { get; set; } = new(1f, 0.84f, 0.22f, 0.22f);

    [Export]
    public Color SelectedColor { get; set; } = new(0.35f, 1f, 0.55f, 0.22f);

    [Export]
    public Color InvalidColor { get; set; } = new(1f, 0.04f, 0.02f, 0.38f);

    [Export]
    public float RangeBorderWidth { get; set; } = 1.5f;

    [ExportGroup("Path Arrows")]

    [Export]
    public Color PathArrowColor { get; set; } = new(1f, 0.94f, 0.35f, 0.86f);

    [Export]
    public float PathArrowWidth { get; set; } = 2.2f;

    [Export]
    public float PathArrowHeadLength { get; set; } = 6f;

    [Export]
    public float PathArrowHeadAngleDegrees { get; set; } = 34f;

    [Export]
    public float PathArrowCellPaddingRatio { get; set; } = 0.28f;

    private readonly Dictionary<BattleGridHighlightKind, HashSet<GridPosition>> _cellsByKind = new();
    private readonly List<GridPosition> _pathCells = new();

    private WorldSiteRoot _siteRoot;
    private BattleMapView _battleMapView;
    private BattleGridMap _gridMap;
    private BattleMapLayer _coordinateLayer;
    private GridPosition? _hoverCell;

    public override void _Ready()
    {
        ZIndex = 100;
        SetProcess(HoverEnabled);

        _siteRoot = FindWorldSiteRoot();
        if (_siteRoot == null)
        {
            GD.PushWarning("BattleGridHighlightOverlay could not find WorldSiteRoot.");
            return;
        }

        _siteRoot.SiteMapLoaded += OnSiteMapLoaded;

        if (_siteRoot.ActiveSiteMap != null)
        {
            OnSiteMapLoaded(_siteRoot.ActiveSiteMap);
        }
    }

    public override void _Process(double delta)
    {
        if (!HoverEnabled || _battleMapView == null || _gridMap == null || _coordinateLayer == null)
        {
            SetHoverCell(null);
            return;
        }

        Vector2 mouseGlobal = _battleMapView.GetGlobalMousePosition();
        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(mouseGlobal));
        var position = new GridPosition(tilePosition.X, tilePosition.Y);

        SetHoverCell(_gridMap.TryGetCell(position, out _) ? position : null);
    }

    public void SetCells(BattleGridHighlightKind kind, IEnumerable<GridPosition> cells)
    {
        if (kind == BattleGridHighlightKind.Hover)
        {
            GridPosition[] hoverCells = cells.Take(1).ToArray();
            SetHoverCell(hoverCells.Length == 0 ? null : hoverCells[0]);
            return;
        }

        if (kind == BattleGridHighlightKind.Path)
        {
            SetPath(cells);
            return;
        }

        _cellsByKind[kind] = cells.ToHashSet();
        Rebuild();
    }

    public void SetPath(IEnumerable<GridPosition> cells)
    {
        GridPosition[] orderedCells = cells.ToArray();
        _pathCells.Clear();
        _pathCells.AddRange(orderedCells);
        _cellsByKind[BattleGridHighlightKind.Path] = orderedCells.ToHashSet();
        Rebuild();
    }

    public void ClearCells(BattleGridHighlightKind kind)
    {
        if (kind == BattleGridHighlightKind.Hover)
        {
            SetHoverCell(null);
            return;
        }

        if (kind == BattleGridHighlightKind.Path)
        {
            _pathCells.Clear();
        }

        if (_cellsByKind.Remove(kind))
        {
            Rebuild();
        }
    }

    public void ClearAll()
    {
        _cellsByKind.Clear();
        _pathCells.Clear();
        _hoverCell = null;
        Rebuild();
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        _battleMapView = activeSiteMap as BattleMapView;
        _battleMapView?.EnsureRuntimeData();
        _gridMap = _siteRoot?.ActiveGridMap ?? _battleMapView?.GridMap;
        _coordinateLayer = _battleMapView?.CoordinateLayer;
        _hoverCell = null;
        _pathCells.Clear();
        Rebuild();
    }

    private void SetHoverCell(GridPosition? position)
    {
        if (_hoverCell == position)
        {
            return;
        }

        _hoverCell = position;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        if (_coordinateLayer == null)
        {
            return;
        }

        foreach (BattleGridHighlightKind kind in GetRangeDrawOrder())
        {
            if (!_cellsByKind.TryGetValue(kind, out HashSet<GridPosition> cells))
            {
                continue;
            }

            foreach (GridPosition cell in cells)
            {
                AddCellHighlight(kind, cell);
            }
        }

        AddPathArrows();

        if (_hoverCell.HasValue)
        {
            AddCellHighlight(BattleGridHighlightKind.Hover, _hoverCell.Value);
        }
    }

    private void AddCellHighlight(BattleGridHighlightKind kind, GridPosition cell)
    {
        Vector2[] polygon = BuildCellPolygon(cell);

        if (kind == BattleGridHighlightKind.Hover)
        {
            AddHoverFrame(polygon);
            return;
        }

        (Color fill, Color border, float borderWidth) = GetStyle(kind);

        var fillNode = new Polygon2D
        {
            Polygon = polygon,
            Color = fill,
            ZIndex = (int)kind * 2
        };
        AddChild(fillNode);

        var borderNode = new Line2D
        {
            Points = ClosePolygon(polygon),
            Width = borderWidth,
            DefaultColor = border,
            Closed = true,
            ZIndex = (int)kind * 2 + 1
        };
        AddChild(borderNode);
    }

    private void AddHoverFrame(Vector2[] polygon)
    {
        if (HoverFillColor.A > 0f)
        {
            AddChild(new Polygon2D
            {
                Polygon = polygon,
                Color = HoverFillColor,
                ZIndex = (int)BattleGridHighlightKind.Hover * 2
            });
        }

        float cornerLengthRatio = Mathf.Clamp(HoverCornerLengthRatio, 0.02f, 0.45f);

        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 corner = polygon[index];
            Vector2 previous = polygon[(index - 1 + polygon.Length) % polygon.Length];
            Vector2 next = polygon[(index + 1) % polygon.Length];

            AddHoverCornerSegment(corner, corner.Lerp(previous, cornerLengthRatio));
            AddHoverCornerSegment(corner, corner.Lerp(next, cornerLengthRatio));
        }
    }

    private void AddHoverCornerSegment(Vector2 start, Vector2 end)
    {
        AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = HoverBorderWidth,
            DefaultColor = HoverBorderColor,
            ZIndex = (int)BattleGridHighlightKind.Hover * 2 + 1
        });
    }

    private void AddPathArrows()
    {
        if (_pathCells.Count < 2)
        {
            return;
        }

        for (int index = 0; index < _pathCells.Count - 1; index++)
        {
            AddPathArrow(_pathCells[index], _pathCells[index + 1]);
        }
    }

    private void AddPathArrow(GridPosition from, GridPosition to)
    {
        Vector2 fromCenter = BuildCellCenter(from);
        Vector2 toCenter = BuildCellCenter(to);
        Vector2 delta = toCenter - fromCenter;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        Vector2 direction = delta / length;
        float padding = Mathf.Min(
            length * 0.35f,
            GetCellHalfExtent(from) * Mathf.Clamp(PathArrowCellPaddingRatio, 0f, 0.45f));
        Vector2 start = fromCenter + direction * padding;
        Vector2 end = toCenter - direction * padding;

        if ((end - start).Length() <= 1f)
        {
            start = fromCenter;
            end = toCenter;
        }

        int zIndex = (int)BattleGridHighlightKind.Hover * 2 - 1;
        AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = PathArrowWidth,
            DefaultColor = PathArrowColor,
            ZIndex = zIndex
        });

        float headLength = Mathf.Min(PathArrowHeadLength, length * 0.32f);
        float headAngle = Mathf.DegToRad(PathArrowHeadAngleDegrees);
        Vector2 back = -direction;
        AddPathArrowHeadSegment(end, end + back.Rotated(headAngle) * headLength, zIndex);
        AddPathArrowHeadSegment(end, end + back.Rotated(-headAngle) * headLength, zIndex);
    }

    private void AddPathArrowHeadSegment(Vector2 start, Vector2 end, int zIndex)
    {
        AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = PathArrowWidth,
            DefaultColor = PathArrowColor,
            ZIndex = zIndex
        });
    }

    private Vector2 BuildCellCenter(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        return ToLocal(_coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(origin)));
    }

    private float GetCellHalfExtent(GridPosition cell)
    {
        Vector2 center = BuildCellCenter(cell);
        Vector2 right = BuildCellCenter(new GridPosition(cell.X + 1, cell.Y));
        Vector2 down = BuildCellCenter(new GridPosition(cell.X, cell.Y + 1));
        return Mathf.Min((right - center).Length(), (down - center).Length()) * 0.5f;
    }

    private Vector2[] BuildCellPolygon(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        Vector2 center = _coordinateLayer.MapToLocal(origin);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => ToLocal(_coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private (Color fill, Color border, float borderWidth) GetStyle(BattleGridHighlightKind kind)
    {
        return kind switch
        {
            BattleGridHighlightKind.Move => (MoveColor, WithAlpha(MoveColor, 0.55f), RangeBorderWidth),
            BattleGridHighlightKind.Path => (PathColor, WithAlpha(PathColor, 0.72f), RangeBorderWidth + 0.35f),
            BattleGridHighlightKind.Threat => (ThreatColor, WithAlpha(ThreatColor, 0.46f), RangeBorderWidth),
            BattleGridHighlightKind.Attack => (AttackColor, WithAlpha(AttackColor, 0.58f), RangeBorderWidth),
            BattleGridHighlightKind.Skill => (SkillColor, WithAlpha(SkillColor, 0.56f), RangeBorderWidth),
            BattleGridHighlightKind.Target => (TargetColor, WithAlpha(TargetColor, 0.58f), RangeBorderWidth),
            BattleGridHighlightKind.Selected => (SelectedColor, WithAlpha(SelectedColor, 0.62f), RangeBorderWidth),
            BattleGridHighlightKind.Invalid => (InvalidColor, WithAlpha(InvalidColor, 0.45f), RangeBorderWidth),
            BattleGridHighlightKind.Hover => (HoverFillColor, HoverBorderColor, HoverBorderWidth),
            _ => (HoverFillColor, HoverBorderColor, HoverBorderWidth)
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    private static IEnumerable<BattleGridHighlightKind> GetRangeDrawOrder()
    {
        yield return BattleGridHighlightKind.Move;
        yield return BattleGridHighlightKind.Path;
        yield return BattleGridHighlightKind.Threat;
        yield return BattleGridHighlightKind.Skill;
        yield return BattleGridHighlightKind.Attack;
        yield return BattleGridHighlightKind.Target;
        yield return BattleGridHighlightKind.Selected;
        yield return BattleGridHighlightKind.Invalid;
    }

    private WorldSiteRoot FindWorldSiteRoot()
    {
        Node current = this;

        while (current != null)
        {
            if (current is WorldSiteRoot siteRoot)
            {
                return siteRoot;
            }

            current = current.GetParent();
        }

        return null;
    }
}
