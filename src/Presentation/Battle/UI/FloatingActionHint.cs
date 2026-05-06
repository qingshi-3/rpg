using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class FloatingActionHint : PanelContainer
{
    [Export]
    public float VisibleDuration { get; set; } = 1.1f;

    [Export]
    public float FadeDuration { get; set; } = 0.45f;

    private Label _label;
    private float _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Modulate = Colors.Transparent;
        _label = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Label",
            nameof(FloatingActionHint));
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        float totalDuration = VisibleDuration + FadeDuration;

        if (_elapsed >= totalDuration)
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        float alpha = _elapsed <= VisibleDuration
            ? 1f
            : 1f - ((_elapsed - VisibleDuration) / Mathf.Max(FadeDuration, 0.001f));

        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }

    public void ShowHint(string text)
    {
        if (_label == null)
        {
            return;
        }

        _label.Text = text;
        _elapsed = 0f;
        Visible = true;
        Modulate = Colors.White;
        SetProcess(true);
    }
}
