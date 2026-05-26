using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle.Snapshots;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class BattleObjectiveMapDialog : Control
{
    private VBoxContainer _companyList;
    private Label _statusLabel;
    private Button _closeButton;
    private Button _doneButton;
    private BattleObjectiveMapPreview _mapPreview;
    private IReadOnlyList<BattleObjectiveCompanyOption> _companies = System.Array.Empty<BattleObjectiveCompanyOption>();
    private IReadOnlyList<BattleObjectiveMapCell> _cells = System.Array.Empty<BattleObjectiveMapCell>();
    private IReadOnlyList<BattleObjectiveZoneSnapshot> _zones = System.Array.Empty<BattleObjectiveZoneSnapshot>();
    private IReadOnlyList<BattleObjectiveMapRegion> _regions = System.Array.Empty<BattleObjectiveMapRegion>();
    private string _selectedCompanyKey = "";
    private string _selectedObjectiveZoneId = "";

    public event System.Action<string> CompanySelected;
    public event System.Action<string> ObjectiveZoneSelected;
    public event System.Action Closed;

    public override void _Ready()
    {
        _companyList = GetNodeOrNull<VBoxContainer>("DialogPanel/DialogMargin/DialogStack/MainContent/CompanyPanel/CompanyMargin/CompanyStack/CompanyList");
        _statusLabel = GetNodeOrNull<Label>("DialogPanel/DialogMargin/DialogStack/StatusLabel");
        _closeButton = GetNodeOrNull<Button>("DialogPanel/DialogMargin/DialogStack/Header/CloseButton");
        _doneButton = GetNodeOrNull<Button>("DialogPanel/DialogMargin/DialogStack/DoneButton");
        _mapPreview = GetNodeOrNull<BattleObjectiveMapPreview>("DialogPanel/DialogMargin/DialogStack/MainContent/PreviewPanel/PreviewMargin/MapPreview");

        if (_closeButton != null)
        {
            _closeButton.Pressed += Close;
        }

        if (_doneButton != null)
        {
            _doneButton.Pressed += Close;
        }

        if (_mapPreview != null)
        {
            _mapPreview.ObjectiveZoneSelected += SelectObjectiveZone;
        }

        Visible = false;
    }

    public void Bind(
        IReadOnlyList<BattleObjectiveCompanyOption> companies,
        string selectedCompanyKey,
        IReadOnlyList<BattleObjectiveMapCell> cells,
        IReadOnlyList<BattleObjectiveZoneSnapshot> zones,
        string selectedObjectiveZoneId,
        IReadOnlyList<BattleObjectiveMapRegion> regions = null)
    {
        _companies = companies ?? System.Array.Empty<BattleObjectiveCompanyOption>();
        _selectedCompanyKey = selectedCompanyKey ?? "";
        _cells = cells ?? System.Array.Empty<BattleObjectiveMapCell>();
        _zones = zones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>();
        _selectedObjectiveZoneId = selectedObjectiveZoneId ?? "";
        _regions = regions ?? System.Array.Empty<BattleObjectiveMapRegion>();

        RefreshCompanyList();
        _mapPreview?.SetData(_cells, _zones, _selectedObjectiveZoneId, _regions);
        RefreshStatus();
    }

    public void Open()
    {
        Visible = true;
        GrabFocus();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    private void RefreshCompanyList()
    {
        if (_companyList == null)
        {
            return;
        }

        foreach (Node child in _companyList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (BattleObjectiveCompanyOption company in _companies)
        {
            Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(BattleObjectiveMapDialog));
            if (button == null)
            {
                continue;
            }

            bool selected = string.Equals(company.GroupKey, _selectedCompanyKey, System.StringComparison.Ordinal);
            button.Text = selected
                ? $"已选 {company.DisplayName}\n{company.PlanSummary}"
                : $"{company.DisplayName}\n{company.PlanSummary}";
            string capturedGroupKey = company.GroupKey ?? "";
            button.Pressed += () => SelectCompany(capturedGroupKey);
            _companyList.AddChild(button);
        }

        if (_companies.Count == 0)
        {
            Label empty = GameUiSceneFactory.CreateWorldMutedLine(nameof(BattleObjectiveMapDialog));
            if (empty != null)
            {
                empty.Text = "没有可指派的我方兵团";
                _companyList.AddChild(empty);
            }
        }
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null)
        {
            return;
        }

        string company = string.IsNullOrWhiteSpace(_selectedCompanyKey) ? "未选择兵团" : "已选择兵团";
        string objective = string.IsNullOrWhiteSpace(_selectedObjectiveZoneId) ? "未选择目标区域" : "已选择目标区域";
        _statusLabel.Text = $"{company} · {objective}";
    }

    private void SelectCompany(string groupKey)
    {
        _selectedCompanyKey = groupKey ?? "";
        CompanySelected?.Invoke(_selectedCompanyKey);
    }

    private void SelectObjectiveZone(string objectiveZoneId)
    {
        _selectedObjectiveZoneId = objectiveZoneId ?? "";
        ObjectiveZoneSelected?.Invoke(_selectedObjectiveZoneId);
    }
}
