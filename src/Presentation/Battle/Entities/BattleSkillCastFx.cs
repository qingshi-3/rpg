using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleSkillCastFx : Node2D
{
    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public double LifetimeSeconds { get; set; } = 1.05;

    [Export]
    public NodePath GroundEllipsePath { get; set; } = new("GroundEllipse");

    [Export]
    public NodePath InnerEllipsePath { get; set; } = new("InnerEllipse");

    [Export]
    public NodePath DissolveEllipsePath { get; set; } = new("DissolveEllipse");

    [Export]
    public NodePath VerticalBeamPath { get; set; } = new("VerticalBeam");

    [Export]
    public NodePath GlyphNorthPath { get; set; } = new("GlyphNorth");

    [Export]
    public NodePath GlyphSouthPath { get; set; } = new("GlyphSouth");

    [Export]
    public Vector2 PerspectiveScale { get; set; } = new(1f, 0.42f);

    [Export]
    public Vector2 DissolveRiseOffset { get; set; } = new(0f, -20f);

    [Export(PropertyHint.Range, "24,128,1")]
    public int EllipseSegments { get; set; } = 72;

    [Export(PropertyHint.Range, "8,80,1")]
    public float GroundRadius { get; set; } = 34f;

    [Export(PropertyHint.Range, "8,80,1")]
    public float InnerRadius { get; set; } = 22f;

    [Export]
    public Color FlashColor { get; set; } = new(0.78f, 0.95f, 1f, 0.42f);

    private Tween _tween;
    private Line2D _groundEllipse;
    private Line2D _innerEllipse;
    private Line2D _dissolveEllipse;
    private Node2D _verticalBeam;
    private Node2D _glyphNorth;
    private Node2D _glyphSouth;

    public override void _Ready()
    {
        _groundEllipse = GetNodeOrNull<Line2D>(GroundEllipsePath);
        _innerEllipse = GetNodeOrNull<Line2D>(InnerEllipsePath);
        _dissolveEllipse = GetNodeOrNull<Line2D>(DissolveEllipsePath);
        _verticalBeam = GetNodeOrNull<Node2D>(VerticalBeamPath);
        _glyphNorth = GetNodeOrNull<Node2D>(GlyphNorthPath);
        _glyphSouth = GetNodeOrNull<Node2D>(GlyphSouthPath);
        ApplyEllipsePoints();
        Play();
    }

    public override void _ExitTree()
    {
        KillTween();
    }

    public void Play(double durationSeconds = 0)
    {
        KillTween();
        RestartParticleChildren(this);

        double duration = durationSeconds > 0
            ? System.Math.Clamp(durationSeconds, 0.55, 1.5)
            : System.Math.Max(0.55, LifetimeSeconds);
        double chargeSeconds = System.Math.Min(0.2, duration * 0.24);
        double releaseSeconds = System.Math.Max(0.2, duration - chargeSeconds);

        // The released cast cue stays pale blue and translucent so it layers
        // behind unit-authored skill frames instead of competing with them.
        Scale = Vector2.One;
        Modulate = Colors.White;
        PrepareCueNode(_groundEllipse, PerspectiveScale * 0.76f, Vector2.Zero, new Color(0.72f, 0.93f, 1f, 0.38f));
        PrepareCueNode(_innerEllipse, PerspectiveScale * 0.58f, Vector2.Zero, new Color(0.84f, 0.97f, 1f, 0.32f));
        PrepareCueNode(_dissolveEllipse, PerspectiveScale * 0.82f, Vector2.Zero, Colors.Transparent);
        PrepareCueNode(_verticalBeam, new Vector2(0.66f, 0.16f), new Vector2(0f, -10f), Colors.Transparent);
        PrepareCueNode(_glyphNorth, PerspectiveScale * 0.92f, Vector2.Zero, new Color(0.78f, 0.95f, 1f, 0.34f));
        PrepareCueNode(_glyphSouth, PerspectiveScale * 0.92f, Vector2.Zero, new Color(0.78f, 0.95f, 1f, 0.32f));

        _tween = CreateTween();
        _tween.SetParallel();
        TweenCanvasItem(_groundEllipse, "scale", PerspectiveScale * 1.18f, duration, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        TweenCanvasItem(_groundEllipse, "position", DissolveRiseOffset * 0.35f, duration, Tween.TransitionType.Sine, Tween.EaseType.Out);
        TweenCanvasItem(_groundEllipse, "modulate", new Color(0.72f, 0.93f, 1f, 0f), releaseSeconds, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds);
        TweenCanvasItem(_innerEllipse, "scale", PerspectiveScale * 1.02f, duration * 0.9, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        TweenCanvasItem(_innerEllipse, "position", DissolveRiseOffset * 0.5f, duration, Tween.TransitionType.Sine, Tween.EaseType.Out);
        TweenCanvasItem(_innerEllipse, "modulate", new Color(0.84f, 0.97f, 1f, 0f), releaseSeconds * 0.85, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds + releaseSeconds * 0.12);
        TweenCanvasItem(_dissolveEllipse, "modulate", new Color(0.68f, 0.92f, 1f, 0.34f), 0.08, Tween.TransitionType.Cubic, Tween.EaseType.Out, chargeSeconds * 0.35);
        TweenCanvasItem(_dissolveEllipse, "scale", PerspectiveScale * 1.72f, releaseSeconds, Tween.TransitionType.Cubic, Tween.EaseType.Out, chargeSeconds);
        TweenCanvasItem(_dissolveEllipse, "position", DissolveRiseOffset, releaseSeconds, Tween.TransitionType.Sine, Tween.EaseType.Out, chargeSeconds);
        TweenCanvasItem(_dissolveEllipse, "modulate", Colors.Transparent, releaseSeconds * 0.76, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds + releaseSeconds * 0.24);
        TweenCanvasItem(_verticalBeam, "modulate", new Color(0.7f, 0.92f, 1f, 0.3f), 0.06, Tween.TransitionType.Cubic, Tween.EaseType.Out, chargeSeconds * 0.3);
        TweenCanvasItem(_verticalBeam, "scale", new Vector2(1.05f, 1.04f), System.Math.Min(0.24, duration * 0.32), Tween.TransitionType.Back, Tween.EaseType.Out, chargeSeconds * 0.2);
        TweenCanvasItem(_verticalBeam, "position", DissolveRiseOffset * 0.7f + new Vector2(0f, -10f), releaseSeconds, Tween.TransitionType.Sine, Tween.EaseType.Out, chargeSeconds);
        TweenCanvasItem(_verticalBeam, "modulate", Colors.Transparent, releaseSeconds * 0.78, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds + releaseSeconds * 0.12);
        TweenCanvasItem(_glyphNorth, "position", DissolveRiseOffset * 0.45f, releaseSeconds, Tween.TransitionType.Sine, Tween.EaseType.Out, chargeSeconds);
        TweenCanvasItem(_glyphNorth, "modulate", Colors.Transparent, releaseSeconds * 0.74, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds + releaseSeconds * 0.16);
        TweenCanvasItem(_glyphSouth, "position", DissolveRiseOffset * 0.45f, releaseSeconds, Tween.TransitionType.Sine, Tween.EaseType.Out, chargeSeconds);
        TweenCanvasItem(_glyphSouth, "modulate", Colors.Transparent, releaseSeconds * 0.74, Tween.TransitionType.Sine, Tween.EaseType.In, chargeSeconds + releaseSeconds * 0.18);
        _tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private void ApplyEllipsePoints()
    {
        Vector2[] ground = BuildEllipsePoints(GroundRadius, System.Math.Max(24, EllipseSegments));
        Vector2[] inner = BuildEllipsePoints(InnerRadius, System.Math.Max(24, EllipseSegments));
        ApplyEllipsePoints(_groundEllipse, ground);
        ApplyEllipsePoints(_dissolveEllipse, ground);
        ApplyEllipsePoints(_innerEllipse, inner);
    }

    private static Vector2[] BuildEllipsePoints(float radius, int segments)
    {
        int pointCount = System.Math.Max(24, segments);
        Vector2[] points = new Vector2[pointCount];
        double step = System.Math.PI * 2.0 / pointCount;
        for (int index = 0; index < pointCount; index++)
        {
            double angle = step * index;
            points[index] = new Vector2(
                (float)System.Math.Cos(angle) * radius,
                (float)System.Math.Sin(angle) * radius);
        }

        return points;
    }

    private static void ApplyEllipsePoints(Line2D line, Vector2[] points)
    {
        if (line == null || !GodotObject.IsInstanceValid(line) || points == null)
        {
            return;
        }

        line.ClearPoints();
        foreach (Vector2 point in points)
        {
            line.AddPoint(point);
        }
    }

    private static void PrepareCueNode(Node2D item, Vector2 scale, Vector2 position, Color color)
    {
        if (item == null || !GodotObject.IsInstanceValid(item))
        {
            return;
        }

        item.Scale = scale;
        item.Position = position;
        item.Modulate = color;
    }

    private void TweenCanvasItem(
        Node2D item,
        string property,
        Variant value,
        double seconds,
        Tween.TransitionType transition,
        Tween.EaseType ease,
        double delaySeconds = 0)
    {
        if (item == null || !GodotObject.IsInstanceValid(item))
        {
            return;
        }

        PropertyTweener tween = _tween.TweenProperty(item, property, value, seconds);
        tween.SetTrans(transition);
        tween.SetEase(ease);
        if (delaySeconds > 0)
        {
            tween.SetDelay(delaySeconds);
        }
    }

    private void KillTween()
    {
        if (_tween != null && GodotObject.IsInstanceValid(_tween))
        {
            _tween.Kill();
        }

        _tween = null;
    }

    private static void RestartParticleChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.IsClass("CPUParticles2D") || child.IsClass("GPUParticles2D"))
            {
                child.Set("emitting", false);
                child.Call("restart");
                child.Set("emitting", true);
            }

            RestartParticleChildren(child);
        }
    }
}
