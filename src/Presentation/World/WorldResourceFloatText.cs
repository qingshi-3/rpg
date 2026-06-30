using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class WorldResourceFloatText : Control
{
    [Export]
    public NodePath IconLabelPath { get; set; } = new("Margin/Row/IconLabel");

    [Export]
    public NodePath AmountLabelPath { get; set; } = new("Margin/Row/AmountLabel");

    [Export]
    public double FloatSeconds { get; set; } = 0.9;

    private Label _iconLabel;
    private Label _amountLabel;
    private Tween _floatTween;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _iconLabel = GetNodeOrNull<Label>(IconLabelPath);
        _amountLabel = GetNodeOrNull<Label>(AmountLabelPath);
        if (_iconLabel == null || _amountLabel == null)
        {
            GameLog.Warn(nameof(WorldResourceFloatText), $"Missing resource float labels icon={_iconLabel != null} amount={_amountLabel != null}");
        }
    }

    public override void _ExitTree()
    {
        _floatTween?.Kill();
        _floatTween = null;
    }

    public void Bind(string resourceSymbol, string resourceDisplayName, int amount, Color accentColor)
    {
        if (_iconLabel != null)
        {
            _iconLabel.Text = string.IsNullOrWhiteSpace(resourceSymbol) ? "?" : resourceSymbol;
            _iconLabel.TooltipText = resourceDisplayName ?? "";
            _iconLabel.AddThemeColorOverride("font_color", accentColor.Lightened(0.2f));
            _iconLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.8f));
            _iconLabel.AddThemeConstantOverride("outline_size", 3);
        }

        if (_amountLabel != null)
        {
            _amountLabel.Text = amount > 0 ? $"+{amount}" : amount.ToString();
            _amountLabel.TooltipText = resourceDisplayName ?? "";
            _amountLabel.AddThemeColorOverride("font_color", accentColor.Lightened(0.35f));
            _amountLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
            _amountLabel.AddThemeConstantOverride("outline_size", 4);
        }
    }

    public void Play(Vector2 screenPosition, double delaySeconds)
    {
        _floatTween?.Kill();
        Vector2 size = ResolveControlSize();
        Position = screenPosition - new Vector2(size.X * 0.5f, size.Y);
        PivotOffset = size * 0.5f;
        Scale = new Vector2(0.92f, 0.92f);
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        Visible = delaySeconds <= 0.0;

        Vector2 restPosition = Position;
        _floatTween = CreateTween().BindNode(this);
        if (delaySeconds > 0.0)
        {
            _floatTween.TweenInterval(delaySeconds);
            _floatTween.TweenCallback(Callable.From(() => Visible = true));
        }

        _floatTween.TweenProperty(this, "modulate", Colors.White, 0.12)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _floatTween.Parallel().TweenProperty(this, "scale", Vector2.One, 0.12)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _floatTween.TweenProperty(this, "position", restPosition + new Vector2(0.0f, -42.0f), FloatSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _floatTween.Parallel().TweenProperty(this, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), FloatSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _floatTween.TweenCallback(Callable.From(() => QueueFree()));
    }

    private Vector2 ResolveControlSize()
    {
        Vector2 size = Size;
        if (size.X <= 1.0f || size.Y <= 1.0f)
        {
            size = CustomMinimumSize;
        }

        if (size.X <= 1.0f || size.Y <= 1.0f)
        {
            size = GetCombinedMinimumSize();
        }

        return new Vector2(Mathf.Max(72.0f, size.X), Mathf.Max(26.0f, size.Y));
    }
}
