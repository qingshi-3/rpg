using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Presentation.World.Sites;

public partial class BattleObjectiveMapPreview : Control
{
    private static readonly Color BackgroundColor = new(0.035f, 0.045f, 0.045f, 1.0f);
    private static readonly Color LandColor = new(0.35f, 0.43f, 0.31f, 1.0f);
    private static readonly Color BlockedLandColor = new(0.20f, 0.24f, 0.21f, 1.0f);
    private static readonly Color WaterColor = new(0.12f, 0.28f, 0.38f, 1.0f);
    private static readonly Color PlayerDeploymentFillColor = new(0.30f, 0.78f, 0.38f, 0.34f);
    private static readonly Color PlayerDeploymentBorderColor = new(0.58f, 1.0f, 0.64f, 0.90f);
    private static readonly Color ObjectiveFillColor = new(0.95f, 0.34f, 0.24f, 0.40f);
    private static readonly Color ObjectiveBorderColor = new(1.0f, 0.72f, 0.48f, 0.95f);
    private static readonly Color SelectedObjectiveFillColor = new(1.0f, 0.76f, 0.22f, 0.50f);
    private static readonly Color SelectedObjectiveBorderColor = new(1.0f, 0.93f, 0.48f, 1.0f);
    private static readonly Color GridLineColor = new(1.0f, 1.0f, 1.0f, 0.06f);

    private IReadOnlyList<BattleObjectiveMapCell> _cells = System.Array.Empty<BattleObjectiveMapCell>();
    private IReadOnlyList<BattleObjectiveZoneSnapshot> _zones = System.Array.Empty<BattleObjectiveZoneSnapshot>();
    private IReadOnlyList<BattleObjectiveMapRegion> _regions = System.Array.Empty<BattleObjectiveMapRegion>();
    private readonly Dictionary<string, Rect2> _zoneRects = new(System.StringComparer.Ordinal);
    private readonly HashSet<string> _selectableZoneIds = new(System.StringComparer.Ordinal);
    private string _selectedObjectiveZoneId = "";
    private Rect2 _mapRect = new(Vector2.Zero, Vector2.One);
    private int _minX;
    private int _maxX;
    private int _minY;
    private int _maxY;

    public event System.Action<string> ObjectiveZoneSelected;

    public void SetData(
        IReadOnlyList<BattleObjectiveMapCell> cells,
        IReadOnlyList<BattleObjectiveZoneSnapshot> zones,
        string selectedObjectiveZoneId,
        IReadOnlyList<BattleObjectiveMapRegion> regions = null)
    {
        _cells = cells ?? System.Array.Empty<BattleObjectiveMapCell>();
        _zones = zones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>();
        _regions = regions ?? BuildRegionsFromZones(_zones);
        _selectedObjectiveZoneId = selectedObjectiveZoneId ?? "";
        _selectableZoneIds.Clear();
        foreach (BattleObjectiveMapRegion region in _regions.Where(region => region?.Selectable == true))
        {
            _selectableZoneIds.Add(region.RegionId ?? "");
        }
        RebuildBounds();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor, filled: true);
        if (_cells.Count == 0)
        {
            DrawEmptyState();
            return;
        }

        ResolveMapRect();
        DrawMapCells();
        DrawObjectiveZones();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouseButton)
        {
            return;
        }

        string zoneId = ResolveObjectiveAt(mouseButton.Position);
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return;
        }

        ObjectiveZoneSelected?.Invoke(zoneId);
        AcceptEvent();
    }

    private void RebuildBounds()
    {
        IEnumerable<int> xs = _cells.Select(cell => cell.X)
            .Concat(_regions.SelectMany(region => new[] { region.CellX, region.CellX + System.Math.Max(1, region.Width) - 1 }));
        IEnumerable<int> ys = _cells.Select(cell => cell.Y)
            .Concat(_regions.SelectMany(region => new[] { region.CellY, region.CellY + System.Math.Max(1, region.Height) - 1 }));

        _minX = xs.Any() ? xs.Min() : 0;
        _maxX = xs.Any() ? xs.Max() : 0;
        _minY = ys.Any() ? ys.Min() : 0;
        _maxY = ys.Any() ? ys.Max() : 0;
        if (_maxX < _minX)
        {
            _maxX = _minX;
        }

        if (_maxY < _minY)
        {
            _maxY = _minY;
        }
    }

    private void ResolveMapRect()
    {
        float widthCells = System.Math.Max(1, _maxX - _minX + 1);
        float heightCells = System.Math.Max(1, _maxY - _minY + 1);
        float padding = 18.0f;
        Vector2 available = new(System.Math.Max(1.0f, Size.X - padding * 2.0f), System.Math.Max(1.0f, Size.Y - padding * 2.0f));
        float scale = System.Math.Min(available.X / widthCells, available.Y / heightCells);
        Vector2 mapSize = new(widthCells * scale, heightCells * scale);
        Vector2 origin = (Size - mapSize) * 0.5f;
        _mapRect = new Rect2(origin, mapSize);
    }

    private void DrawMapCells()
    {
        foreach (BattleObjectiveMapCell cell in _cells)
        {
            Rect2 rect = CellRect(cell.X, cell.Y, 1, 1).Grow(-0.5f);
            Color color = cell.IsWater ? WaterColor : cell.IsWalkable ? LandColor : BlockedLandColor;
            DrawRect(rect, color, filled: true);
            DrawRect(rect, GridLineColor, filled: false, width: 1.0f);
        }
    }

    private void DrawObjectiveZones()
    {
        _zoneRects.Clear();
        Font font = GetThemeDefaultFont();
        int fontSize = System.Math.Max(12, GetThemeDefaultFontSize());

        foreach (BattleObjectiveMapRegion region in _regions.OrderBy(region => region?.Priority ?? int.MaxValue))
        {
            if (region == null || string.IsNullOrWhiteSpace(region.RegionId))
            {
                continue;
            }

            Rect2 rect = CellRect(region.CellX, region.CellY, System.Math.Max(1, region.Width), System.Math.Max(1, region.Height)).Grow(2.0f);
            bool selected = string.Equals(region.RegionId, _selectedObjectiveZoneId, System.StringComparison.Ordinal);
            bool playerSide = string.Equals(region.DeploymentSide, "Player", System.StringComparison.OrdinalIgnoreCase);
            Color fill = selected ? SelectedObjectiveFillColor : playerSide ? PlayerDeploymentFillColor : ObjectiveFillColor;
            Color border = selected ? SelectedObjectiveBorderColor : playerSide ? PlayerDeploymentBorderColor : ObjectiveBorderColor;
            DrawRect(rect, fill, filled: true);
            DrawRect(rect, border, filled: false, width: selected ? 3.0f : 2.0f);
            if (region.Selectable)
            {
                _zoneRects[region.RegionId] = rect;
            }

            string label = string.IsNullOrWhiteSpace(region.DisplayName) ? "部署区" : region.DisplayName.Trim();
            Vector2 labelPosition = rect.Position + new Vector2(6.0f, System.Math.Max(16.0f, fontSize + 2.0f));
            DrawString(font, labelPosition, label, HorizontalAlignment.Left, rect.Size.X - 12.0f, fontSize, Colors.White);
        }
    }

    private Rect2 CellRect(int x, int y, int width, int height)
    {
        float widthCells = System.Math.Max(1, _maxX - _minX + 1);
        float heightCells = System.Math.Max(1, _maxY - _minY + 1);
        float cellWidth = _mapRect.Size.X / widthCells;
        float cellHeight = _mapRect.Size.Y / heightCells;
        float left = _mapRect.Position.X + (x - _minX) * cellWidth;
        float top = _mapRect.Position.Y + (y - _minY) * cellHeight;
        return new Rect2(left, top, cellWidth * width, cellHeight * height);
    }

    private string ResolveObjectiveAt(Vector2 position)
    {
        foreach (KeyValuePair<string, Rect2> pair in _zoneRects)
        {
            if (pair.Value.HasPoint(position))
            {
                return _selectableZoneIds.Contains(pair.Key) ? pair.Key : "";
            }
        }

        return "";
    }

    private static IReadOnlyList<BattleObjectiveMapRegion> BuildRegionsFromZones(
        IReadOnlyList<BattleObjectiveZoneSnapshot> zones)
    {
        return (zones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>())
            .Where(zone => zone != null && !string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
            .Select(zone => new BattleObjectiveMapRegion
            {
                RegionId = zone.ObjectiveZoneId,
                DisplayName = zone.DisplayName,
                DeploymentSide = zone.DeploymentSide,
                Priority = zone.Priority,
                CellX = zone.CellX,
                CellY = zone.CellY,
                Width = zone.Width,
                Height = zone.Height,
                Selectable = true
            })
            .ToArray();
    }

    private void DrawEmptyState()
    {
        Font font = GetThemeDefaultFont();
        int fontSize = System.Math.Max(14, GetThemeDefaultFontSize());
        DrawString(
            font,
            Size * 0.5f - new Vector2(110.0f, 0.0f),
            "没有可绘制的地图数据",
            HorizontalAlignment.Left,
            240.0f,
            fontSize,
            new Color(0.8f, 0.88f, 0.86f, 1.0f));
    }
}
