using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicBattleGateDialog : Control
{
    private enum BattleGateState
    {
        Brief,
        Detail
    }

    private Control _briefPanel;
    private Control _detailPanel;
    private Label _briefTitleLabel;
    private Label _briefBodyLabel;
    private Label _detailTitleLabel;
    private Label _detailBodyLabel;
    private Button _briefEnterButton;
    private Button _briefDetailsButton;
    private Button _briefDeferButton;
    private Button _detailEnterButton;
    private Button _detailDeferButton;
    private StrategicBattleGateDialogData _data = new();
    private BattleGateState _state = BattleGateState.Brief;
    private bool _ready;

    public event System.Action EnterBattlePressed;
    public event System.Action ViewDetailsPressed;
    public event System.Action DeferPressed;

    public override void _Ready()
    {
        _briefPanel = GameUiSceneFactory.GetRequiredNode<Control>(this, "BriefPanel", nameof(StrategicBattleGateDialog));
        _detailPanel = GameUiSceneFactory.GetRequiredNode<Control>(this, "DetailPanel", nameof(StrategicBattleGateDialog));
        _briefTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "BriefPanel/BriefMargin/BriefStack/BriefTitle", nameof(StrategicBattleGateDialog));
        _briefBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "BriefPanel/BriefMargin/BriefStack/BriefBody", nameof(StrategicBattleGateDialog));
        _detailTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "DetailPanel/DetailMargin/DetailStack/DetailTitle", nameof(StrategicBattleGateDialog));
        _detailBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "DetailPanel/DetailMargin/DetailStack/DetailScroll/DetailBody", nameof(StrategicBattleGateDialog));
        _briefEnterButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "BriefPanel/BriefMargin/BriefStack/BriefActions/BriefEnterButton", nameof(StrategicBattleGateDialog));
        _briefDetailsButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "BriefPanel/BriefMargin/BriefStack/BriefActions/BriefDetailsButton", nameof(StrategicBattleGateDialog));
        _briefDeferButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "BriefPanel/BriefMargin/BriefStack/BriefActions/BriefDeferButton", nameof(StrategicBattleGateDialog));
        _detailEnterButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "DetailPanel/DetailMargin/DetailStack/DetailActions/DetailEnterButton", nameof(StrategicBattleGateDialog));
        _detailDeferButton = GameUiSceneFactory.GetRequiredNode<Button>(this, "DetailPanel/DetailMargin/DetailStack/DetailActions/DetailDeferButton", nameof(StrategicBattleGateDialog));

        ConnectButton(_briefEnterButton, EnterBattlePressed);
        ConnectButton(_briefDetailsButton, ViewDetailsPressed);
        ConnectButton(_briefDeferButton, DeferPressed);
        ConnectButton(_detailEnterButton, EnterBattlePressed);
        ConnectButton(_detailDeferButton, DeferPressed);

        _ready = true;
        ApplyBinding();
        ApplyState();
    }

    public void Bind(StrategicBattleGateDialogData data)
    {
        _data = data ?? new StrategicBattleGateDialogData();
        ApplyBinding();
    }

    public void OpenBrief()
    {
        _state = BattleGateState.Brief;
        Visible = true;
        ApplyState();
        GrabButtonFocus(_briefEnterButton);
    }

    public void OpenDetail()
    {
        _state = BattleGateState.Detail;
        Visible = true;
        ApplyState();
        GrabButtonFocus(_detailEnterButton);
    }

    public void Close()
    {
        Visible = false;
    }

    private static void ConnectButton(Button button, System.Action callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.Pressed += callback;
    }

    private void ApplyBinding()
    {
        if (!_ready)
        {
            return;
        }

        SetLabelText(_briefTitleLabel, _data.Title);
        SetLabelText(_briefBodyLabel, _data.BriefText);
        SetLabelText(_detailTitleLabel, _data.DetailTitle);
        SetLabelText(_detailBodyLabel, _data.DetailText);
    }

    private void ApplyState()
    {
        if (!_ready)
        {
            return;
        }

        bool brief = _state == BattleGateState.Brief;
        if (_briefPanel != null)
        {
            _briefPanel.Visible = brief;
        }

        if (_detailPanel != null)
        {
            _detailPanel.Visible = !brief;
        }
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label != null)
        {
            label.Text = string.IsNullOrWhiteSpace(text) ? "—" : text;
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

public sealed class StrategicBattleGateDialogData
{
    public string Title { get; set; } = "";
    public string BriefText { get; set; } = "";
    public string DetailTitle { get; set; } = "";
    public string DetailText { get; set; } = "";
}
