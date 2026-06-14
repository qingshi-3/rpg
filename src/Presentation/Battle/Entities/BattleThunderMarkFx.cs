using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleThunderMarkFx : Node2D
{
    [Export]
    public NodePath OuterRingPath { get; set; } = new("OuterRing");

    [Export]
    public NodePath InnerGlyphPath { get; set; } = new("InnerGlyph");

    [Export]
    public NodePath PulseRingPath { get; set; } = new("PulseRing");

    [Export(PropertyHint.Range, "1,20,0.5")]
    public double LifetimeSeconds { get; set; } = 8.0;

    private Tween _pulseTween;
    private Line2D _outerRing;
    private Line2D _innerGlyph;
    private Line2D _pulseRing;

    public override void _Ready()
    {
        _outerRing = GetNodeOrNull<Line2D>(OuterRingPath);
        _innerGlyph = GetNodeOrNull<Line2D>(InnerGlyphPath);
        _pulseRing = GetNodeOrNull<Line2D>(PulseRingPath);
        Play();
    }

    public override void _ExitTree()
    {
        KillTween();
    }

    public void Play()
    {
        KillTween();
        Modulate = Colors.White;
        PrepareLine(_outerRing, new Color(0.52f, 0.9f, 1f, 0.76f), Vector2.One);
        PrepareLine(_innerGlyph, new Color(0.9f, 1f, 1f, 0.95f), Vector2.One);
        PrepareLine(_pulseRing, new Color(0.45f, 0.82f, 1f, 0.34f), Vector2.One);

        _pulseTween = CreateTween();
        _pulseTween.SetLoops();
        _pulseTween.TweenProperty(_pulseRing, "scale", new Vector2(1.28f, 0.58f), 0.58)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _pulseTween.Parallel().TweenProperty(_pulseRing, "modulate", new Color(0.45f, 0.82f, 1f, 0.02f), 0.58)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        _pulseTween.TweenCallback(Callable.From(() =>
        {
            PrepareLine(_pulseRing, new Color(0.45f, 0.82f, 1f, 0.34f), Vector2.One);
        }));

        _ = QueueFreeAfterLifetime();
    }

    private async System.Threading.Tasks.Task QueueFreeAfterLifetime()
    {
        if (!IsInsideTree())
        {
            return;
        }

        await ToSignal(
            GetTree().CreateTimer(System.Math.Max(0.5, LifetimeSeconds), processAlways: false),
            SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this))
        {
            QueueFree();
        }
    }

    private static void PrepareLine(Line2D line, Color color, Vector2 scale)
    {
        if (line == null || !GodotObject.IsInstanceValid(line))
        {
            return;
        }

        line.Modulate = color;
        line.Scale = scale;
    }

    private void KillTween()
    {
        if (_pulseTween != null && GodotObject.IsInstanceValid(_pulseTween))
        {
            _pulseTween.Kill();
        }

        _pulseTween = null;
    }
}
