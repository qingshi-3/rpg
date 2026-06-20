using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class StrategicBuildingPlacementPreview : Node2D
{
    private static readonly Color BuildableFill = new(0.12f, 0.92f, 0.52f, 0.26f);
    private static readonly Color BuildableBorder = new(0.42f, 1.0f, 0.72f, 0.92f);
    private static readonly Color BlockedFill = new(1.0f, 0.12f, 0.08f, 0.30f);
    private static readonly Color BlockedBorder = new(1.0f, 0.38f, 0.24f, 0.96f);

    private readonly List<Vector2[]> _polygons = new();
    private bool _buildable;

    public override void _Ready()
    {
        ZIndex = 3200;
        Visible = false;
    }

    public void SetPreview(IEnumerable<Vector2[]> globalPolygons, bool buildable)
    {
        _polygons.Clear();
        foreach (Vector2[] polygon in globalPolygons ?? Enumerable.Empty<Vector2[]>())
        {
            if (polygon == null || polygon.Length < 3)
            {
                continue;
            }

            _polygons.Add(polygon.Select(ToLocal).ToArray());
        }

        _buildable = buildable;
        Visible = _polygons.Count > 0;
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _polygons.Clear();
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_polygons.Count == 0)
        {
            return;
        }

        Color fill = _buildable ? BuildableFill : BlockedFill;
        Color border = _buildable ? BuildableBorder : BlockedBorder;
        foreach (Vector2[] polygon in _polygons)
        {
            DrawColoredPolygon(polygon, fill);
            DrawPolyline(ClosePolygon(polygon), border, 2.2f, true);
        }
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length == 0)
        {
            return System.Array.Empty<Vector2>();
        }

        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }
}
