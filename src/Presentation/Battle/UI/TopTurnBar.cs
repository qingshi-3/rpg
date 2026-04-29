using Godot;

namespace Rpg.Presentation.Battle.UI;

public partial class TopTurnBar : PanelContainer
{
    private readonly Label _label = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", BuildPanelStyle());

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 6);

        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.Text = "第 1 回合     我方行动     回合顺序：○  ○  ○  …";
        _label.AddThemeColorOverride("font_color", Colors.White);

        margin.AddChild(_label);
        AddChild(margin);
    }

    private static StyleBoxFlat BuildPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.42f),
            BorderColor = new Color(1f, 1f, 1f, 0.14f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        return style;
    }
}
