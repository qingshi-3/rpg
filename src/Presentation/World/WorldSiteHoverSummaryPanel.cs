using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class WorldSiteHoverSummaryPanel : PanelContainer
{
    private Label _titleLabel;
    private Label _resourceLabel;
    private Label _forceLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _titleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/TitleLabel", nameof(WorldSiteHoverSummaryPanel));
        _resourceLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/ResourceLabel", nameof(WorldSiteHoverSummaryPanel));
        _forceLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Margin/Root/ForceLabel", nameof(WorldSiteHoverSummaryPanel));
    }

    public void Bind(WorldSiteHoverSummaryData data)
    {
        SetLabelText(_titleLabel, data?.Title);
        SetLabelText(_resourceLabel, data?.ResourceText);
        SetLabelText(_forceLabel, data?.ForceText);
    }

    public void HideSummary()
    {
        Visible = false;
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label != null)
        {
            label.Text = string.IsNullOrWhiteSpace(text) ? "无" : text;
        }
    }
}
