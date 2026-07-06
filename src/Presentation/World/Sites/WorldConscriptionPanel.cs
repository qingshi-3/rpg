using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldConscriptionPanel : VBoxContainer
{
    [Signal]
    public delegate void ManualConscriptRequestedEventHandler();

    [Signal]
    public delegate void AutoConscriptionIntensityRequestedEventHandler(string intensityId);

    private Label _reserveValueLabel;
    private Label _capacityUsageLabel;
    private ProgressBar _reserveFill;
    private Button _manualConscriptButton;
    private Control _currentIntensityPanel;
    private Label _currentIntensityNameLabel;
    private Label _currentIntensityGainLabel;
    private GridContainer _intensityOptionGrid;
    private StrategicConscriptionViewModel _pendingView = new();

    public override void _Ready()
    {
        _reserveValueLabel = GetNodeOrNull<Label>("ReserveSummaryPanel/SummaryMargin/SummaryStack/ReserveHeaderRow/ReserveValueLabel");
        _capacityUsageLabel = GetNodeOrNull<Label>("ReserveSummaryPanel/SummaryMargin/SummaryStack/CapacityUsageLabel");
        _reserveFill = GetNodeOrNull<ProgressBar>("ReserveSummaryPanel/SummaryMargin/SummaryStack/ReserveFill");
        _manualConscriptButton = GetNodeOrNull<Button>("ManualConscriptButton");
        _currentIntensityPanel = GetNodeOrNull<Control>("CurrentIntensityPanel");
        _currentIntensityNameLabel = GetNodeOrNull<Label>("CurrentIntensityPanel/CurrentIntensityMargin/CurrentIntensityRow/CurrentIntensityTextStack/CurrentIntensityNameLabel");
        _currentIntensityGainLabel = GetNodeOrNull<Label>("CurrentIntensityPanel/CurrentIntensityMargin/CurrentIntensityRow/CurrentIntensityGainLabel");
        _intensityOptionGrid = GetNodeOrNull<GridContainer>("IntensityOptionGrid");

        if (_manualConscriptButton != null)
        {
            _manualConscriptButton.Pressed += OnManualConscriptPressed;
        }

        ApplyBinding();
    }

    public override void _ExitTree()
    {
        if (_manualConscriptButton != null)
        {
            _manualConscriptButton.Pressed -= OnManualConscriptPressed;
        }
    }

    public void Bind(StrategicConscriptionViewModel viewModel)
    {
        _pendingView = viewModel ?? new StrategicConscriptionViewModel();
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        StrategicConscriptionViewModel view = _pendingView ?? new StrategicConscriptionViewModel();
        int active = System.Math.Max(0, view.ActiveForces);
        int reserve = System.Math.Max(0, view.ReserveForces);
        int capacity = System.Math.Max(0, view.CityForceCapacity);
        int remaining = System.Math.Max(0, view.RemainingForceCapacity);
        int occupied = System.Math.Min(active + reserve, capacity);

        if (_reserveValueLabel != null)
        {
            _reserveValueLabel.Text = reserve.ToString();
        }

        if (_capacityUsageLabel != null)
        {
            _capacityUsageLabel.Text = capacity > 0
                ? $"兵力 {occupied}/{capacity} · 剩余 {remaining}"
                : "兵力容量未配置";
        }

        if (_reserveFill != null)
        {
            _reserveFill.MaxValue = System.Math.Max(1, capacity);
            _reserveFill.Value = System.Math.Clamp(occupied, 0, System.Math.Max(1, capacity));
        }

        BindManualConscript(view.ManualOption ?? new StrategicConscriptionManualOptionViewModel());
        BindCurrentIntensity(view);
        BindIntensityOptions(view);
    }

    private void BindManualConscript(StrategicConscriptionManualOptionViewModel manual)
    {
        if (_manualConscriptButton == null)
        {
            return;
        }

        string cost = StrategicManagementDashboardPanelBinder.FormatCostsForPresentation(manual.Cost);
        int gain = System.Math.Max(0, manual.ReserveGain);
        _manualConscriptButton.Text = $"手动征兵  +{gain}";
        _manualConscriptButton.Disabled = !manual.CanConscript;
        _manualConscriptButton.TooltipText = manual.CanConscript
            ? $"立即补充城市预备兵 +{gain}\n成本 {cost}"
            : $"不可执行：{StrategicManagementDashboardPanelBinder.FormatReasonsForPresentation(manual.DisabledReason)}\n成本 {cost}";
    }

    private void BindCurrentIntensity(StrategicConscriptionViewModel view)
    {
        StrategicConscriptionIntensityOptionViewModel current = view.IntensityOptions?
            .FirstOrDefault(option => option != null && option.IsCurrent);

        if (_currentIntensityNameLabel != null)
        {
            _currentIntensityNameLabel.Text = current == null ? "未配置" : current.DisplayName;
        }

        if (_currentIntensityGainLabel != null)
        {
            int gain = System.Math.Max(0, current?.ReserveGain ?? 0);
            _currentIntensityGainLabel.Text = $"每次 +{gain}";
        }

        if (_currentIntensityPanel != null)
        {
            _currentIntensityPanel.TooltipText = current == null
                ? "自动征兵未配置。"
                : BuildIntensityTooltip(current);
        }
    }

    private void BindIntensityOptions(StrategicConscriptionViewModel view)
    {
        if (_intensityOptionGrid == null)
        {
            return;
        }

        foreach (Node child in _intensityOptionGrid.GetChildren())
        {
            child.QueueFree();
        }

        foreach (StrategicConscriptionIntensityOptionViewModel option in view.IntensityOptions ?? Enumerable.Empty<StrategicConscriptionIntensityOptionViewModel>())
        {
            if (option == null || option.IsCurrent)
            {
                continue;
            }

            BindIntensityOption(option);
        }
    }

    private void BindIntensityOption(StrategicConscriptionIntensityOptionViewModel option)
    {
        Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldConscriptionPanel));
        if (button == null || _intensityOptionGrid == null)
        {
            return;
        }

        string intensityId = option.IntensityId ?? "";
        button.CustomMinimumSize = new Vector2(0.0f, 52.0f);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Text = $"{option.DisplayName}  {BuildIntensityStatusText(option)}";
        button.TooltipText = BuildIntensityTooltip(option);
        button.Disabled = !option.CanSelect;
        button.ToggleMode = false;
        button.FocusMode = FocusModeEnum.All;

        if (option.CanSelect)
        {
            button.Pressed += () => EmitSignal(SignalName.AutoConscriptionIntensityRequested, intensityId);
        }

        _intensityOptionGrid.AddChild(button);
    }

    private static string BuildIntensityStatusText(StrategicConscriptionIntensityOptionViewModel option)
    {
        if (!option.CanSelect)
        {
            return "锁定";
        }

        return $"+{System.Math.Max(0, option.ReserveGain)}";
    }

    private static string BuildIntensityTooltip(StrategicConscriptionIntensityOptionViewModel option)
    {
        string requirement = option.RequiresTrainingGround ? "需要训练场" : "无需训练场";
        string tooltip =
            $"{option.DisplayName}\n每次大地图结算：预备兵 +{System.Math.Max(0, option.ReserveGain)}\n成本 {StrategicManagementDashboardPanelBinder.FormatCostsForPresentation(option.Cost)}\n{requirement}";
        if (!option.CanSelect && !option.IsCurrent)
        {
            tooltip = $"{tooltip}\n不可选择：{StrategicManagementDashboardPanelBinder.FormatReasonsForPresentation(option.DisabledReason)}";
        }

        return tooltip;
    }

    private void OnManualConscriptPressed()
    {
        EmitSignal(SignalName.ManualConscriptRequested);
    }
}
