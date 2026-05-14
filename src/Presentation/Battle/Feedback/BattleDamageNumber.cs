using Godot;

namespace Rpg.Presentation.Battle.Feedback;

public partial class BattleDamageNumber : Label
{
    [Export]
    // Damage feedback should mostly rise, with a slight right drift to avoid covering the unit sprite center.
    public Vector2 FloatOffset { get; set; } = BattleDamageNumberMotionSpec.Default.FloatOffset;

    [Export(PropertyHint.Range, "0.2,2,0.05")]
    public double LifetimeSeconds { get; set; } = 0.75;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public double HoldSeconds { get; set; } = 0.08;

    private bool _played;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 2000;
    }

    public void Play(string text)
    {
        Text = text ?? "";
        Modulate = Colors.White;
        CallDeferred(nameof(Start));
    }

    private void Start()
    {
        if (_played)
        {
            return;
        }

        _played = true;
        PivotOffset = Size * 0.5f;
        Position -= new Vector2(Size.X * 0.5f, Size.Y);

        if (!IsInsideTree())
        {
            return;
        }

        double lifetime = System.Math.Max(0.2, LifetimeSeconds);
        double hold = System.Math.Clamp(HoldSeconds, 0, lifetime * 0.5);
        Vector2 targetPosition = Position + FloatOffset;

        Tween tween = CreateTween();
        tween.SetParallel();
        tween.TweenProperty(this, "position", targetPosition, lifetime)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate", Colors.Transparent, System.Math.Max(0.1, lifetime - hold))
            .SetDelay(hold);
        tween.Finished += QueueFree;
    }
}
