using Godot;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Common;

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
        _cost.Text = "选择行动菜单中的指令";
        _description.Text = "移动、攻击、能力可以按任意顺序发起";
        _state.Text = "";
    }

    public void ShowCommand(BattleActionMenuCommandViewModel command, bool selected)
    {
        _title.Text = command.Label;
        _cost.Text = command.ApCost.HasValue
            ? $"消耗 {command.ApCost.Value} 行动点"
            : "不消耗行动点";
        _description.Text = GetCommandDescription(command);
        _state.Text = GetCommandState(command, selected);
    }

    private static string GetCommandDescription(BattleActionMenuCommandViewModel command)
    {
        return command.Id switch
        {
            "move" => "预览范围，选择地块移动",
            "attack" => "预览范围，选择目标攻击",
            _ when BattleAbilityQueries.IsAbilityCommand(command.Id) => "预览范围，选择目标释放能力",
            "cards" => "后续接入卡牌指令",
            "corps" or "corps_order" => "切换兵团指令（突击 / 集火 / 坚守）",
            "wait" => "放弃继续操作",
            "end" => "结束当前单位行动",
            _ => "查看目标、消耗和状态"
        };
    }

    private static string GetCommandState(BattleActionMenuCommandViewModel command, bool selected)
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
