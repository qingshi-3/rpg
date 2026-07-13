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

    [Export]
    public NodePath CollapseSparksPath { get; set; } = new("CollapseSparks");

    [Export(PropertyHint.Range, "1,20,0.5")]
    public double LifetimeSeconds { get; set; } = 8.0;

    private Tween _pulseTween;
    private Line2D _outerRing;
    private Line2D _innerGlyph;
    private Line2D _pulseRing;
    private CpuParticles2D _collapseSparks;
    private int _lifetimeVersion;
    private bool _collapsing;

    public override void _Ready()
    {
        _outerRing = GetNodeOrNull<Line2D>(OuterRingPath);
        _innerGlyph = GetNodeOrNull<Line2D>(InnerGlyphPath);
        _pulseRing = GetNodeOrNull<Line2D>(PulseRingPath);
        _collapseSparks = GetNodeOrNull<CpuParticles2D>(CollapseSparksPath);
        Play();
    }

    public override void _ExitTree()
    {
        _lifetimeVersion++;
        KillTween();
    }

    public void Play()
    {
        KillTween();
        int lifetimeVersion = ++_lifetimeVersion;
        _collapsing = false;
        Modulate = Colors.White;
        PrepareLine(_outerRing, new Color(0.52f, 0.9f, 1f, 0.76f), Vector2.One);
        PrepareLine(_innerGlyph, new Color(0.9f, 1f, 1f, 0.95f), Vector2.One);
        PrepareLine(_pulseRing, new Color(0.45f, 0.82f, 1f, 0.34f), Vector2.One);

        _pulseTween = CreateTween().BindNode(this);
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

        _ = QueueFreeAfterLifetime(lifetimeVersion);
    }

    public void CollapseAndDischarge()
    {
        if (_collapsing || !GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _collapsing = true;
        int lifetimeVersion = ++_lifetimeVersion;
        KillTween();
        if (_collapseSparks != null && GodotObject.IsInstanceValid(_collapseSparks))
        {
            _collapseSparks.Restart();
            _collapseSparks.Emitting = true;
        }

        // Runtime consumes the mark immediately. Presentation keeps only this short
        // collapse discharge so the authoritative removal does not read as a pop.
        _pulseTween = CreateTween().BindNode(this).SetParallel();
        _pulseTween.TweenProperty(_outerRing, "scale", new Vector2(0.08f, 0.04f), 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _pulseTween.TweenProperty(_innerGlyph, "scale", new Vector2(0.05f, 0.02f), 0.1)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _pulseTween.TweenProperty(this, "modulate", new Color(1.2f, 1.4f, 1.6f, 0), 0.14);
        _ = QueueFreeAfterCollapse(lifetimeVersion);
    }

    private async System.Threading.Tasks.Task QueueFreeAfterCollapse(int lifetimeVersion)
    {
        await ToSignal(
            GetTree().CreateTimer(0.16, processAlways: false),
            SceneTreeTimer.SignalName.Timeout);
        if (IsLifetimeVersionCurrent(lifetimeVersion))
        {
            QueueFree();
        }
    }

    private async System.Threading.Tasks.Task QueueFreeAfterLifetime(int lifetimeVersion)
    {
        if (!IsLifetimeVersionCurrent(lifetimeVersion))
        {
            return;
        }

        await ToSignal(
            GetTree().CreateTimer(System.Math.Max(0.5, LifetimeSeconds), processAlways: false),
            SceneTreeTimer.SignalName.Timeout);
        if (IsLifetimeVersionCurrent(lifetimeVersion))
        {
            QueueFree();
        }
    }

    private bool IsLifetimeVersionCurrent(int lifetimeVersion)
    {
        // Fire-and-forget timer waits must not let a stale Play cycle free the current mark.
        return GodotObject.IsInstanceValid(this) &&
            IsInsideTree() &&
            lifetimeVersion == _lifetimeVersion;
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
