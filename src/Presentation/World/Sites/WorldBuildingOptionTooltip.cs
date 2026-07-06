using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldBuildingOptionTooltip : PanelContainer
{
    private Label _titleLabel;
    private Label _footprintLabel;
    private Label _costLabel;
    private Label _disabledReasonLabel;
    private string _titleText = "建筑";
    private string _footprintText = "占地 1x1";
    private string _costText = "成本 无";
    private string _disabledReasonText = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _titleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/TitleLabel", nameof(WorldBuildingOptionTooltip));
        _footprintLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/FootprintLabel", nameof(WorldBuildingOptionTooltip));
        _costLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/CostLabel", nameof(WorldBuildingOptionTooltip));
        _disabledReasonLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/DisabledReasonLabel", nameof(WorldBuildingOptionTooltip));
        ApplyBinding();
    }

    public void Bind(string titleText, string footprintText, string costText, string disabledReasonText = "")
    {
        _titleText = string.IsNullOrWhiteSpace(titleText) ? "建筑" : titleText.Trim();
        _footprintText = string.IsNullOrWhiteSpace(footprintText) ? "占地 1x1" : footprintText.Trim();
        _costText = string.IsNullOrWhiteSpace(costText) ? "成本 无" : costText.Trim();
        _disabledReasonText = disabledReasonText ?? "";
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        SetLabelText(_titleLabel, _titleText);
        SetLabelText(_footprintLabel, _footprintText);
        SetLabelText(_costLabel, _costText);

        if (_disabledReasonLabel != null)
        {
            bool hasDisabledReason = !string.IsNullOrWhiteSpace(_disabledReasonText);
            _disabledReasonLabel.Visible = hasDisabledReason;
            _disabledReasonLabel.Text = hasDisabledReason ? $"不可建造：{_disabledReasonText.Trim()}" : "";
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
