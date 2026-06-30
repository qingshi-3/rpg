using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class PostBattleSettlementDialog : Control
{
    private Label _titleLabel;
    private Label _resultBodyLabel;
    private Button _manageCityButton;
    private Button _returnButton;
    private PostBattleSettlementDialogData _data = new();
    private bool _ready;

    public event System.Action ManageCityPressed;
    public event System.Action ReturnPressed;

    public override void _Ready()
    {
        _titleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Panel/Margin/Stack/TitleLabel", nameof(PostBattleSettlementDialog));
        _resultBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Panel/Margin/Stack/ResultScroll/ResultBody", nameof(PostBattleSettlementDialog));
        _manageCityButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "Panel/Margin/Stack/Actions/ManageCityButton", nameof(PostBattleSettlementDialog));
        _returnButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "Panel/Margin/Stack/Actions/ReturnButton", nameof(PostBattleSettlementDialog));

        if (_manageCityButton != null)
        {
            _manageCityButton.Pressed += () => ManageCityPressed?.Invoke();
        }

        if (_returnButton != null)
        {
            _returnButton.Pressed += () => ReturnPressed?.Invoke();
        }

        _ready = true;
        ApplyBinding();
    }

    public void Bind(PostBattleSettlementDialogData data)
    {
        _data = data ?? new PostBattleSettlementDialogData();
        ApplyBinding();
    }

    public void Open()
    {
        Visible = true;
        GrabButtonFocus(_data.ManageCityAvailable ? _manageCityButton : _returnButton);
    }

    public void Close()
    {
        Visible = false;
    }

    private void ApplyBinding()
    {
        if (!_ready)
        {
            return;
        }

        if (_titleLabel != null)
        {
            _titleLabel.Text = string.IsNullOrWhiteSpace(_data.Title) ? "战斗结算" : _data.Title.Trim();
        }

        if (_resultBodyLabel != null)
        {
            _resultBodyLabel.Text = string.IsNullOrWhiteSpace(_data.ResultText) ? "结算已完成。" : _data.ResultText.Trim();
        }

        if (_manageCityButton != null)
        {
            _manageCityButton.Visible = _data.ManageCityAvailable;
            _manageCityButton.Disabled = !_data.ManageCityAvailable;
            _manageCityButton.TooltipText = _data.ManageCityAvailable ? "进入该城池的经营界面" : "";
        }

        if (_returnButton != null)
        {
            _returnButton.Visible = true;
            _returnButton.Disabled = false;
            _returnButton.TooltipText = "返回大地图";
        }
    }

    private static void GrabButtonFocus(Button button)
    {
        if (button != null)
        {
            button.GrabFocus();
        }
    }
}

public sealed class PostBattleSettlementDialogData
{
    public string Title { get; set; } = "";
    public string ResultText { get; set; } = "";
    public bool ManageCityAvailable { get; set; }
}
