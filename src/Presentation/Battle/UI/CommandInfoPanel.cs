using Godot;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Common;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class CommandInfoPanel : PanelContainer
{
    private Label _title;
    private Label _cost;
    private Label _description;
    private Label _state;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _title = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/Title", nameof(CommandInfoPanel));
        _cost = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/Cost", nameof(CommandInfoPanel));
        _description = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/Description", nameof(CommandInfoPanel));
        _state = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/State", nameof(CommandInfoPanel));
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

    private static string GetCommandDescription(ActionWheelCommandViewModel command)
    {
        return command.Id switch
        {
            "move" => "预览范围，选择地块移动",
            "attack" => "预览范围，选择目标攻击",
            _ when BattleAbilityQueries.IsAbilityCommand(command.Id) => "预览范围，选择目标释放能力",
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

}
