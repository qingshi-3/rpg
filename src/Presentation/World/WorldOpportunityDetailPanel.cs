using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class WorldOpportunityDetailPanel : VBoxContainer
{
    [Signal]
    public delegate void CompletePressedEventHandler();

    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _statusValueLabel;
    private Label _spawnPointValueLabel;
    private Label _remainingValueLabel;
    private Label _rewardValueLabel;
    private Button _completeButton;

    public override void _Ready()
    {
        _titleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "TitleLabel", nameof(WorldOpportunityDetailPanel));
        _descriptionLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "DescriptionLabel", nameof(WorldOpportunityDetailPanel));
        _statusValueLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "MetaGrid/StatusValueLabel", nameof(WorldOpportunityDetailPanel));
        _spawnPointValueLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "MetaGrid/SpawnPointValueLabel", nameof(WorldOpportunityDetailPanel));
        _remainingValueLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "MetaGrid/RemainingValueLabel", nameof(WorldOpportunityDetailPanel));
        _rewardValueLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "MetaGrid/RewardValueLabel", nameof(WorldOpportunityDetailPanel));
        _completeButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "CompleteButton", nameof(WorldOpportunityDetailPanel));
        if (_completeButton != null)
        {
            _completeButton.Pressed += () => EmitSignal(SignalName.CompletePressed);
        }
    }

    public void Bind(WorldOpportunityDetailPanelData data)
    {
        SetLabelText(_titleLabel, data?.Title);
        SetLabelText(_descriptionLabel, data?.Description);
        SetLabelText(_statusValueLabel, data?.StatusText);
        SetLabelText(_spawnPointValueLabel, data?.SpawnPointText);
        SetLabelText(_remainingValueLabel, data?.RemainingText);
        SetLabelText(_rewardValueLabel, data?.RewardText);
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label != null)
        {
            label.Text = string.IsNullOrWhiteSpace(text) ? "—" : text;
        }
    }
}

public sealed class WorldOpportunityDetailPanelData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string SpawnPointText { get; set; } = "";
    public string RemainingText { get; set; } = "";
    public string RewardText { get; set; } = "";
}
