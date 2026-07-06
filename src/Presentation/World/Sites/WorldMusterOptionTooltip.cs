using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMusterOptionTooltip : PanelContainer
{
    private Label _titleLabel;
    private Label _reserveLabel;
    private Label _costLabel;
    private Label _disabledReasonLabel;
    private string _titleText = "兵种";
    private string _reserveText = "预备兵 0";
    private string _costText = "成本 无";
    private string _disabledReasonText = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _titleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/TitleLabel", nameof(WorldMusterOptionTooltip));
        _reserveLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/ReserveLabel", nameof(WorldMusterOptionTooltip));
        _costLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/CostLabel", nameof(WorldMusterOptionTooltip));
        _disabledReasonLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/DisabledReasonLabel", nameof(WorldMusterOptionTooltip));
        ApplyBinding();
    }

    public void Bind(string titleText, string reserveText, string costText, string disabledReasonText = "")
    {
        _titleText = string.IsNullOrWhiteSpace(titleText) ? "兵种" : titleText.Trim();
        _reserveText = string.IsNullOrWhiteSpace(reserveText) ? "预备兵 0" : reserveText.Trim();
        _costText = string.IsNullOrWhiteSpace(costText) ? "成本 无" : costText.Trim();
        _disabledReasonText = disabledReasonText ?? "";
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        SetLabelText(_titleLabel, _titleText);
        SetLabelText(_reserveLabel, _reserveText);
        SetLabelText(_costLabel, _costText);

        if (_disabledReasonLabel != null)
        {
            bool hasDisabledReason = !string.IsNullOrWhiteSpace(_disabledReasonText);
            _disabledReasonLabel.Visible = hasDisabledReason;
            _disabledReasonLabel.Text = hasDisabledReason ? $"不可招募：{_disabledReasonText.Trim()}" : "";
        }
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label != null)
        {
            label.Text = string.IsNullOrWhiteSpace(text) ? "无" : text;
        }
    }
}
