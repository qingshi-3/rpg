using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattlePreparationDestinationGuideOverlay : Node2D
{
    private const int DefaultArcPoints = 8;

    [Export]
    public NodePath ArcBodyPath { get; set; } = new("ArcBody");

    [Export]
    public NodePath ArrowHeadLeftPath { get; set; } = new("ArrowHeadLeft");

    [Export]
    public NodePath ArrowHeadRightPath { get; set; } = new("ArrowHeadRight");

    [Export(PropertyHint.Range, "4,24,1")]
    public int ArcPoints { get; set; } = DefaultArcPoints;

    [Export(PropertyHint.Range, "12,48,1")]
    public float ArrowLength { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "8,32,1")]
    public float ArrowHalfWidth { get; set; } = 10.0f;

    [Export]
    public Color ValidColor { get; set; } = new(0.18f, 0.95f, 0.72f, 0.90f);

    [Export]
    public Color InvalidColor { get; set; } = new(1.0f, 0.20f, 0.12f, 0.88f);

    private Line2D _arcBody;
    private Line2D _arrowHeadLeft;
    private Line2D _arrowHeadRight;
    private bool _hasGuide;
    private Vector2 _fromGlobal;
    private Vector2 _toGlobal;
    private bool _valid;

    public BattlePreparationDestinationGuideOverlay()
    {
        Name = "BattlePreparationDestinationGuideOverlay";
        Visible = false;
        ZAsRelative = false;
        ZIndex = 900;
        YSortEnabled = false;
    }

    public override void _Ready()
    {
        _arcBody = GetNodeOrNull<Line2D>(ArcBodyPath);
        _arrowHeadLeft = GetNodeOrNull<Line2D>(ArrowHeadLeftPath);
        _arrowHeadRight = GetNodeOrNull<Line2D>(ArrowHeadRightPath);
        RefreshGuideVisuals();
    }

    public void SetGuide(Vector2 fromGlobal, Vector2 toGlobal, bool valid)
    {
        _fromGlobal = fromGlobal;
        _toGlobal = toGlobal;
        _valid = valid;
        _hasGuide = true;
        Visible = true;
        RefreshGuideVisuals();
    }

    public void ClearGuide()
    {
        _hasGuide = false;
        Visible = false;
        ClearVisuals();
    }

    private void RefreshGuideVisuals()
    {
        if (!_hasGuide)
        {
            ClearVisuals();
            return;
        }

        Vector2 start = ToLocal(_fromGlobal);
        Vector2 end = ToLocal(_toGlobal);
        if (start.DistanceSquaredTo(end) < 16.0f)
        {
            ClearVisuals();
            return;
        }

        Vector2[] points = BuildReferenceArcPoints(start, end, ArcPoints);
        ApplyLinePoints(_arcBody, points);
        ApplyColors(_valid ? ValidColor : InvalidColor);
        ApplyArrowLines(points);
    }

    private void ClearVisuals()
    {
        _arcBody?.ClearPoints();
        _arrowHeadLeft?.ClearPoints();
        _arrowHeadRight?.ClearPoints();
    }

    private static Vector2[] BuildReferenceArcPoints(Vector2 start, Vector2 end, int arcPoints)
    {
        int pointCount = System.Math.Max(4, arcPoints);
        Vector2 distance = end - start;
        Vector2[] points = new Vector2[pointCount + 1];
        for (int index = 0; index < pointCount; index++)
        {
            float t = index / (float)pointCount;
            points[index] = SnapToPixel(new Vector2(
                start.X + distance.X * t,
                start.Y + distance.Y * EaseOutCubic(t)));
        }

        points[^1] = SnapToPixel(end);
        return points;
    }

    private static float EaseOutCubic(float value)
        => 1.0f - Mathf.Pow(1.0f - Mathf.Clamp(value, 0.0f, 1.0f), 3.0f);

    private static void ApplyLinePoints(Line2D line, Vector2[] points)
    {
        if (line == null || !GodotObject.IsInstanceValid(line))
        {
            return;
        }

        line.ClearPoints();
        foreach (Vector2 point in points)
        {
            line.AddPoint(point);
        }
    }

    private void ApplyColors(Color core)
    {
        if (_arcBody != null)
        {
            _arcBody.DefaultColor = core;
        }

        if (_arrowHeadLeft != null)
        {
            _arrowHeadLeft.DefaultColor = core;
        }

        if (_arrowHeadRight != null)
        {
            _arrowHeadRight.DefaultColor = core;
        }
    }

    private void ApplyArrowLines(Vector2[] points)
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        Vector2 tip = points[^1];
        Vector2 direction = tip - points[^2];
        if (direction.LengthSquared() < 0.001f)
        {
            direction = tip - points[0];
        }

        if (direction.LengthSquared() < 0.001f)
        {
            return;
        }

        direction = direction.Normalized();
        Vector2 normal = new(-direction.Y, direction.X);
        Vector2 baseCenter = tip - direction * ArrowLength;
        ApplyLinePoints(_arrowHeadLeft, new[] { SnapToPixel(baseCenter + normal * ArrowHalfWidth), tip });
        ApplyLinePoints(_arrowHeadRight, new[] { SnapToPixel(baseCenter - normal * ArrowHalfWidth), tip });
    }

    private static Vector2 SnapToPixel(Vector2 point)
        => new(Mathf.Round(point.X), Mathf.Round(point.Y));
}
