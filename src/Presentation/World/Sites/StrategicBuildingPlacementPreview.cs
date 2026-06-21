using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class StrategicBuildingPlacementPreview : Node2D
{
    private readonly List<Vector2[]> _polygons = new();
    private bool _buildable;
    private Texture2D _texture;
    private Rect2 _textureRect;
    private bool _hasTextureRect;

    public override void _Ready()
    {
        ZIndex = 3200;
        Visible = false;
    }

    public void SetPreview(IEnumerable<Vector2[]> globalPolygons, bool buildable, Texture2D texture)
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
        _texture = texture;
        _hasTextureRect = TryBuildTextureRect(_polygons, out _textureRect);
        Visible = _polygons.Count > 0;
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _polygons.Clear();
        _texture = null;
        _hasTextureRect = false;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_polygons.Count == 0)
        {
            return;
        }

        if (_texture != null && _hasTextureRect)
        {
            Color textureTint = _buildable
                ? new Color(1.0f, 1.0f, 1.0f, 0.88f)
                : new Color(1.0f, 0.58f, 0.50f, 0.72f);
            DrawTextureRect(_texture, _textureRect, false, textureTint);
        }
    }

    private static bool TryBuildTextureRect(IReadOnlyList<Vector2[]> polygons, out Rect2 rect)
    {
        rect = default;
        Vector2[] points = (polygons ?? System.Array.Empty<Vector2[]>())
            .SelectMany(polygon => polygon ?? System.Array.Empty<Vector2>())
            .ToArray();
        if (points.Length == 0)
        {
            return false;
        }

        float minX = points.Min(point => point.X);
        float minY = points.Min(point => point.Y);
        float maxX = points.Max(point => point.X);
        float maxY = points.Max(point => point.Y);
        rect = new Rect2(minX, minY, maxX - minX, maxY - minY);
        return rect.Size.X > 0.001f && rect.Size.Y > 0.001f;
    }
}
