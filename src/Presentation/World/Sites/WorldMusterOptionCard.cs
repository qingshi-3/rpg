using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMusterOptionCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string corpsDefinitionId);

    private const string MoneyResourceId = "resource_money";
    private const string FoodResourceId = "resource_food";
    private const string WoodResourceId = "resource_wood";
    private const string OreResourceId = "resource_ore";

    private BattleUnitPlinthPreview _preview;
    private Label _nameLabel;
    private Control _moneyCostSlot;
    private Control _foodCostSlot;
    private Control _woodCostSlot;
    private Control _oreCostSlot;
    private Label _reserveAmountLabel;
    private Label _moneyAmountLabel;
    private Label _foodAmountLabel;
    private Label _woodAmountLabel;
    private Label _oreAmountLabel;
    private string _corpsDefinitionId = "";
    private string _displayName = "";
    private BattleUnitAnimatedPreviewModel _previewModel;
    private int _reserveCost;
    private IReadOnlyList<StrategicResourceCostViewModel> _resourceCosts = System.Array.Empty<StrategicResourceCostViewModel>();
    private string _costText = "";
    private string _disabledReason = "";
    private bool _selectable;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _preview = GetNodeOrNull<BattleUnitPlinthPreview>("PreviewLayer/PlinthPreview");
        _nameLabel = GetNodeOrNull<Label>("Nameplate/NameLabel") ?? GetNodeOrNull<Label>("Content/NameLabel");
        _moneyCostSlot = GetNodeOrNull<Control>("ResourceCostRow/MoneyCostSlot");
        _foodCostSlot = GetNodeOrNull<Control>("ResourceCostRow/FoodCostSlot");
        _woodCostSlot = GetNodeOrNull<Control>("ResourceCostRow/WoodCostSlot");
        _oreCostSlot = GetNodeOrNull<Control>("ResourceCostRow/OreCostSlot");
        _reserveAmountLabel = GetNodeOrNull<Label>("ResourceCostRow/ReserveCostSlot/ReserveAmountLabel");
        _moneyAmountLabel = GetNodeOrNull<Label>("ResourceCostRow/MoneyCostSlot/MoneyAmountLabel");
        _foodAmountLabel = GetNodeOrNull<Label>("ResourceCostRow/FoodCostSlot/FoodAmountLabel");
        _woodAmountLabel = GetNodeOrNull<Label>("ResourceCostRow/WoodCostSlot/WoodAmountLabel");
        _oreAmountLabel = GetNodeOrNull<Label>("ResourceCostRow/OreCostSlot/OreAmountLabel");
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
        IReadOnlyList<StrategicResourceCostViewModel> resourceCosts,
        string costText,
        bool selectable,
        string disabledReason)
    {
        _corpsDefinitionId = corpsDefinitionId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "编制" : displayName.Trim();
        _previewModel = preview;
        _reserveCost = System.Math.Max(0, reserveCost);
        _resourceCosts = resourceCosts?
            .Where(item => item != null && item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .ToArray() ?? System.Array.Empty<StrategicResourceCostViewModel>();
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

        if (_reserveAmountLabel != null)
        {
            _reserveAmountLabel.Text = _reserveCost.ToString(CultureInfo.InvariantCulture);
        }

        ApplyResourceCost(_moneyCostSlot, _moneyAmountLabel, MoneyResourceId);
        ApplyResourceCost(_foodCostSlot, _foodAmountLabel, FoodResourceId);
        ApplyResourceCost(_woodCostSlot, _woodAmountLabel, WoodResourceId);
        ApplyResourceCost(_oreCostSlot, _oreAmountLabel, OreResourceId);

        if (_preview != null)
        {
            _preview.Bind(_previewModel);
        }
    }

    private void ApplyResourceCost(Control slot, Label amountLabel, string resourceId)
    {
        int amount = _resourceCosts.FirstOrDefault(item =>
            string.Equals(item.ResourceId, resourceId, System.StringComparison.Ordinal))?.Amount ?? 0;
        bool hasCost = amount > 0;
        if (slot != null)
        {
            slot.Visible = hasCost;
        }

        if (amountLabel != null)
        {
            amountLabel.Text = amount.ToString(CultureInfo.InvariantCulture);
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
