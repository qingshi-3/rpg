using Godot;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class CommandInfoPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly Label _cost = new();
    private readonly Label _description = new();
    private readonly Label _state = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(300, 68);
        AddThemeStyleboxOverride("panel", BuildPanelStyle());

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 6);

        var root = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddThemeConstantOverride("separation", 1);

        ConfigureLabel(_title, 17, new Color(1f, 0.88f, 0.48f, 1f));
        ConfigureLabel(_cost, 12, new Color(0.76f, 0.9f, 1f, 0.92f));
        ConfigureLabel(_description, 12, new Color(1f, 1f, 1f, 0.82f));
        ConfigureLabel(_state, 12, new Color(1f, 0.56f, 0.42f, 0.92f));

        root.AddChild(_title);
        root.AddChild(_cost);
        root.AddChild(_description);
        root.AddChild(_state);
        margin.AddChild(root);
        AddChild(margin);

        ShowDefault();
    }

    public void ShowDefault()
    {
        _title.Text = "战术指令";
        _cost.Text = "选择下方行动";
        _description.Text = "从左侧转盘发起行动";
        _state.Text = "";
    }

    public void ShowLayer(string layerId)
    {
        if (layerId == ActionWheelLayerIds.Skills)
        {
            _title.Text = "技能指令";
            _cost.Text = "右键或 Esc 返回";
            _description.Text = "选择技能后进入目标选择";
            _state.Text = "";
            return;
        }

        ShowDefault();
    }

    public void ShowCommand(ActionWheelCommandViewModel command, bool selected)
    {
        _title.Text = command.Label;
        _cost.Text = command.ApCost.HasValue
            ? $"消耗 {command.ApCost.Value} 行动点"
            : "不消耗行动点";
        _description.Text = GetCommandDescription(command);
        _state.Text = GetCommandState(command, selected);
    }

    private static void ConfigureLabel(Label label, int fontSize, Color color, bool wrap = false)
    {
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    private static string GetCommandDescription(ActionWheelCommandViewModel command)
    {
        return command.Id switch
        {
            "move" => "预览范围，选择地块移动",
            "attack" => "预览范围，选择目标攻击",
            "skill-menu" => "展开技能转盘",
            "cards" => "后续接入卡牌指令",
            "corps" => "后续接入兵团指挥",
            "wait" => "放弃继续操作",
            "end" => "结束当前单位行动",
            "skill_push" => "选择目标造成推击",
            "skill_guard" => "进入守护或保护目标",
            "skill_mark" => "标记目标辅助后续行动",
            "skill_back" => "返回一级行动转盘",
            _ => "查看目标、消耗和状态"
        };
    }

    private static string GetCommandState(ActionWheelCommandViewModel command, bool selected)
    {
        if (!command.IsEnabled)
        {
            return string.IsNullOrWhiteSpace(command.DisabledReason)
                ? "当前不可用"
                : command.DisabledReason;
        }

        return selected ? "已选中" : "可使用";
    }

    private static StyleBoxFlat BuildPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.02f, 0.4f),
            BorderColor = new Color(1f, 1f, 1f, 0.16f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        return style;
    }
}
