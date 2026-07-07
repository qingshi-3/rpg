using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMusterOptionCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string corpsDefinitionId);

    private BattleUnitPlinthPreview _preview;
    private Label _nameLabel;
    private string _corpsDefinitionId = "";
    private string _displayName = "";
    private BattleUnitAnimatedPreviewModel _previewModel;
    private int _reserveCost;
    private string _costText = "";
    private string _disabledReason = "";
    private bool _selectable;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _preview = GetNodeOrNull<BattleUnitPlinthPreview>("PreviewLayer/PlinthPreview");
        _nameLabel = GetNodeOrNull<Label>("Nameplate/NameLabel") ?? GetNodeOrNull<Label>("Content/NameLabel");
        Pressed += OnPressed;
        ApplyBinding();
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
    }

    public void Bind(
        string corpsDefinitionId,
        string displayName,
        BattleUnitAnimatedPreviewModel preview,
        int reserveCost,
        string costText,
        bool selectable,
        string disabledReason)
    {
        _corpsDefinitionId = corpsDefinitionId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "编制" : displayName.Trim();
        _previewModel = preview;
        _reserveCost = System.Math.Max(0, reserveCost);
        _costText = string.IsNullOrWhiteSpace(costText) ? "无" : costText.Trim();
        _selectable = selectable;
        _disabledReason = disabledReason ?? "";
        ApplyBinding();
    }

    public override Control _MakeCustomTooltip(string forText)
    {
        if (string.IsNullOrWhiteSpace(forText))
        {
            return null;
        }

        WorldMusterOptionTooltip tooltip = GameUiSceneFactory.CreateWorldMusterOptionTooltip(nameof(WorldMusterOptionCard));
        if (tooltip == null)
        {
            return null;
        }

        tooltip.Bind(
            _displayName,
            $"预备兵 {_reserveCost}",
            $"成本 {_costText}",
            _selectable ? "" : _disabledReason);
        return tooltip;
    }

    private void ApplyBinding()
    {
        // Keep disabled options hoverable so the custom tooltip can explain cost
        // and unavailable reasons; click authority stays gated in OnPressed.
        Disabled = false;
        TooltipText = _displayName;
        SelfModulate = _selectable
            ? Colors.White
            : new Color(1.0f, 1.0f, 1.0f, 0.62f);

        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        if (_preview != null)
        {
            _preview.Bind(_previewModel);
        }
    }

    private void OnPressed()
    {
        if (_selectable)
        {
            EmitSignal(SignalName.Selected, _corpsDefinitionId);
        }
    }
}
