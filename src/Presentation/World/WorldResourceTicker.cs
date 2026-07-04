using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class WorldResourceTicker : Control
{
    [Export]
    public NodePath CurrentLabelPath { get; set; } = new("CurrentLabel");

    [Export]
    public NodePath IncomingLabelPath { get; set; } = new("IncomingLabel");

    [Export]
    public double RollSeconds { get; set; } = 0.22;

    private Label _currentLabel;
    private Label _incomingLabel;
    private Tween _rollTween;
    private string _currentText = "";

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _currentLabel = GetNodeOrNull<Label>(CurrentLabelPath);
        _incomingLabel = GetNodeOrNull<Label>(IncomingLabelPath);
        if (_currentLabel == null || _incomingLabel == null)
        {
            GameLog.Warn(nameof(WorldResourceTicker), $"Missing ticker labels current={_currentLabel != null} incoming={_incomingLabel != null}");
            return;
        }

        PrepareLabel(_currentLabel);
        PrepareLabel(_incomingLabel);
        _incomingLabel.Visible = false;
        LayoutLabels(resetPositions: true);
    }

    public override void _ExitTree()
    {
        _rollTween?.Kill();
        _rollTween = null;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutLabels(resetPositions: _rollTween == null);
        }
    }

    public void SetText(string text, bool animate)
    {
        text ??= "";
        if (_currentLabel == null)
        {
            return;
        }

        if (string.Equals(text, _currentText, System.StringComparison.Ordinal))
        {
            return;
        }

        if (!animate || string.IsNullOrWhiteSpace(_currentText) || _incomingLabel == null)
        {
            SetImmediate(text);
            return;
        }

        PlayRoll(text);
    }

    private void SetImmediate(string text)
    {
        _rollTween?.Kill();
        _rollTween = null;
        _currentText = text;
        _currentLabel.Text = text;
        _currentLabel.Visible = true;
        _currentLabel.Modulate = Colors.White;
        _currentLabel.Position = Vector2.Zero;
        if (_incomingLabel != null)
        {
            _incomingLabel.Visible = false;
            _incomingLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            _incomingLabel.Position = new Vector2(0.0f, ResolveLineHeight());
        }
    }

    private void PlayRoll(string text)
    {
        _rollTween?.Kill();
        float lineHeight = ResolveLineHeight();

        _incomingLabel.Text = text;
        _incomingLabel.Visible = true;
        _incomingLabel.Position = new Vector2(0.0f, lineHeight);
        _incomingLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.15f);
        _currentLabel.Visible = true;
        _currentLabel.Position = Vector2.Zero;
        _currentLabel.Modulate = Colors.White;
        _currentText = text;

        // The ticker is a presentation-only response to resource amount changes;
        // Strategic Management remains the single source of resource truth.
        _rollTween = CreateTween().BindNode(this);
        _rollTween.TweenProperty(_currentLabel, "position", new Vector2(0.0f, -lineHeight), RollSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _rollTween.Parallel().TweenProperty(_currentLabel, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), RollSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _rollTween.Parallel().TweenProperty(_incomingLabel, "position", Vector2.Zero, RollSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _rollTween.Parallel().TweenProperty(_incomingLabel, "modulate", Colors.White, RollSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _rollTween.TweenCallback(Callable.From(CompleteRoll));
    }

    private void CompleteRoll()
    {
        _currentLabel.Text = _currentText;
        _currentLabel.Position = Vector2.Zero;
        _currentLabel.Modulate = Colors.White;
        _currentLabel.Visible = true;
        if (_incomingLabel != null)
        {
            _incomingLabel.Visible = false;
            _incomingLabel.Position = new Vector2(0.0f, ResolveLineHeight());
            _incomingLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        }

        _rollTween = null;
    }

    private void LayoutLabels(bool resetPositions)
    {
        Vector2 labelSize = new(Size.X, ResolveLineHeight());
        if (_currentLabel != null)
        {
            _currentLabel.Size = labelSize;
            if (resetPositions)
            {
                _currentLabel.Position = Vector2.Zero;
            }
        }

        if (_incomingLabel != null)
        {
            _incomingLabel.Size = labelSize;
            if (resetPositions)
            {
                _incomingLabel.Position = new Vector2(0.0f, labelSize.Y);
            }
        }
    }

    private float ResolveLineHeight()
    {
        float height = Size.Y;
        if (height <= 1.0f)
        {
            height = CustomMinimumSize.Y;
        }

        return Mathf.Max(18.0f, height);
    }

    private static void PrepareLabel(Label label)
    {
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.ClipText = false;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
    }
}
