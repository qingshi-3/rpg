using Godot;

namespace Rpg.Presentation.Battle.UI;

public partial class FloatingActionHint : PanelContainer
{
    [Export]
    public float VisibleDuration { get; set; } = 1.1f;

    [Export]
    public float FadeDuration { get; set; } = 0.45f;

    private readonly Label _label = new();
    private float _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Modulate = Colors.Transparent;
        AddThemeStyleboxOverride("panel", BuildPanelStyle());

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.44f, 1f));
        _label.AddThemeFontSizeOverride("font_size", 22);

        margin.AddChild(_label);
        AddChild(margin);
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
        _label.Text = text;
        _elapsed = 0f;
        Visible = true;
        Modulate = Colors.White;
        SetProcess(true);
    }

    private static StyleBoxFlat BuildPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.58f),
            BorderColor = new Color(1f, 1f, 1f, 0.18f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        return style;
    }
}
