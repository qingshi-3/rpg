using Godot;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Feedback;

public partial class BattleActionCue : Node2D
{
    [Export]
    public NodePath PulseRingPath { get; set; } = new("PulseRing");

    [Export]
    public NodePath ChevronPath { get; set; } = new("Chevron");

    [Export]
    public Color PlayerColor { get; set; } = new(0.2f, 1f, 0.48f, 0.95f);

    [Export]
    public Color EnemyColor { get; set; } = new(1f, 0.16f, 0.12f, 0.95f);

    [Export]
    public Color NeutralColor { get; set; } = new(1f, 0.84f, 0.28f, 0.95f);

    private Line2D _pulseRing;
    private Line2D _chevron;
    private Tween _tween;

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = 2100;
        _pulseRing = GetNodeOrNull<Line2D>(PulseRingPath);
        _chevron = GetNodeOrNull<Line2D>(ChevronPath);
    }

    public void Play(BattleFaction faction, double durationSeconds)
    {
        Color color = ResolveColor(faction);
        ApplyColor(color);

        Modulate = Colors.Transparent;
        Scale = Vector2.One * 0.84f;
        _tween?.Kill();
        double duration = System.Math.Max(0.1, durationSeconds);
        double fadeIn = System.Math.Min(0.12, duration * 0.35);
        double fadeOut = System.Math.Min(0.16, duration * 0.35);

        _tween = CreateTween();
        _tween.SetParallel();
        _tween.TweenProperty(this, "modulate", Colors.White, fadeIn)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "scale", Vector2.One * 1.08f, duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "modulate", Colors.Transparent, fadeOut)
            .SetDelay(System.Math.Max(0.0, duration - fadeOut));
    }

    public void Finish()
    {
        _tween?.Kill();
        QueueFree();
    }

    private void ApplyColor(Color color)
    {
        if (_pulseRing != null)
        {
            _pulseRing.DefaultColor = color;
        }

        if (_chevron != null)
        {
            _chevron.DefaultColor = color;
        }
    }

    private Color ResolveColor(BattleFaction faction)
    {
        return faction switch
        {
            BattleFaction.Player => PlayerColor,
            BattleFaction.Enemy => EnemyColor,
            _ => NeutralColor
        };
    }
}
