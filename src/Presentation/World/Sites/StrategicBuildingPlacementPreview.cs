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
    private Vector2[] _framePolygon = System.Array.Empty<Vector2>();

    [ExportGroup("Footprint Frame")]
    [Export]
    public Color BuildableFrameColor { get; set; } = new(1f, 1f, 1f, 0.92f);

    [Export]
    public Color BlockedFrameColor { get; set; } = new(1f, 0.18f, 0.10f, 0.95f);

    [Export(PropertyHint.Range, "1,8,0.25")]
    public float FrameWidth { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0.02,0.45,0.01")]
    public float CornerLengthRatio { get; set; } = 0.22f;

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
        _framePolygon = TryBuildFootprintFramePolygon(_polygons, out Vector2[] framePolygon)
            ? framePolygon
            : System.Array.Empty<Vector2>();
        Visible = _polygons.Count > 0;
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _polygons.Clear();
        _texture = null;
        _hasTextureRect = false;
        _framePolygon = System.Array.Empty<Vector2>();
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

        if (_framePolygon.Length >= 3)
        {
            DrawPlacementFootprintFrame(_framePolygon);
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

    private static bool TryBuildFootprintFramePolygon(IReadOnlyList<Vector2[]> polygons, out Vector2[] framePolygon)
    {
        framePolygon = BuildConvexHull(
            (polygons ?? System.Array.Empty<Vector2[]>())
            .SelectMany(polygon => polygon ?? System.Array.Empty<Vector2>()));
        return framePolygon.Length >= 3;
    }

    private static Vector2[] BuildConvexHull(IEnumerable<Vector2> points)
    {
        Vector2[] uniquePoints = (points ?? Enumerable.Empty<Vector2>())
            .GroupBy(point => (X: Mathf.RoundToInt(point.X * 1000f), Y: Mathf.RoundToInt(point.Y * 1000f)))
            .Select(group => group.First())
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToArray();
        if (uniquePoints.Length <= 3)
        {
            return uniquePoints;
        }

        List<Vector2> lower = new();
        foreach (Vector2 point in uniquePoints)
        {
            while (lower.Count >= 2 && Cross(lower[^1] - lower[^2], point - lower[^1]) <= 0.001f)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(point);
        }

        List<Vector2> upper = new();
        for (int index = uniquePoints.Length - 1; index >= 0; index--)
        {
            Vector2 point = uniquePoints[index];
            while (upper.Count >= 2 && Cross(upper[^1] - upper[^2], point - upper[^1]) <= 0.001f)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        return lower.Concat(upper).ToArray();
    }

    private static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    private void DrawPlacementFootprintFrame(Vector2[] polygon)
    {
        float cornerLengthRatio = Mathf.Clamp(CornerLengthRatio, 0.02f, 0.45f);
        Color color = _buildable ? BuildableFrameColor : BlockedFrameColor;
        float width = Mathf.Max(0.5f, FrameWidth);

        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 corner = polygon[index];
            Vector2 previous = polygon[(index - 1 + polygon.Length) % polygon.Length];
            Vector2 next = polygon[(index + 1) % polygon.Length];

            DrawPlacementCornerSegment(corner, corner.Lerp(previous, cornerLengthRatio), color, width);
            DrawPlacementCornerSegment(corner, corner.Lerp(next, cornerLengthRatio), color, width);
        }
    }

    private void DrawPlacementCornerSegment(Vector2 start, Vector2 end, Color color, float width)
    {
        DrawLine(start, end, color, width, antialiased: true);
    }
}
