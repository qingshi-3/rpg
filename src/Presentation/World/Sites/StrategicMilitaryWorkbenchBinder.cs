using System;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class StrategicMilitaryWorkbenchBinder
{
    private readonly Control _panel;
    private readonly Control _backdrop;
    private readonly VBoxContainer _heroList;
    private readonly GridContainer _musterGrid;
    private readonly Label _heroSummaryLabel;
    private readonly Label _noticeLabel;
    private readonly BattleUnitPlinthPreview _selectedHeroPreview;
    private readonly Label _selectedHeroNameLabel;
    private readonly Label _selectedHeroCorpsLabel;
    private readonly Button _backButton;
    private readonly Action<string> _selectHero;
    private readonly Action<string> _recruitCorps;

    public StrategicMilitaryWorkbenchBinder(
        Control panel,
        Control backdrop,
        VBoxContainer heroList,
        GridContainer musterGrid,
        Label heroSummaryLabel,
        Label noticeLabel,
        BattleUnitPlinthPreview selectedHeroPreview,
        Label selectedHeroNameLabel,
        Label selectedHeroCorpsLabel,
        Button backButton,
        Action<string> selectHero,
        Action<string> recruitCorps)
    {
        _panel = panel;
        _backdrop = backdrop;
        _heroList = heroList;
        _musterGrid = musterGrid;
        _heroSummaryLabel = heroSummaryLabel;
        _noticeLabel = noticeLabel;
        _selectedHeroPreview = selectedHeroPreview;
        _selectedHeroNameLabel = selectedHeroNameLabel;
        _selectedHeroCorpsLabel = selectedHeroCorpsLabel;
        _backButton = backButton;
        _selectHero = selectHero;
        _recruitCorps = recruitCorps;
    }

    public void Bind(
        StrategicManagementDashboardViewModel dashboard,
        string selectedHeroId,
        string notice)
    {
        StrategicManagementDashboardViewModel safeDashboard = dashboard ?? new StrategicManagementDashboardViewModel();
        if (_panel != null)
        {
            _panel.Visible = true;
        }
        if (_backdrop != null)
        {
            _backdrop.Visible = true;
        }

        string resolvedHeroId = ResolveSelectedHeroId(safeDashboard, selectedHeroId);
        if (string.IsNullOrWhiteSpace(resolvedHeroId))
        {
            BindHeroSelectionStep(safeDashboard, notice);
            return;
        }

        BindCorpsAdjustmentStep(safeDashboard, resolvedHeroId, notice);
    }

    public void Hide()
    {
        if (_panel != null)
        {
            _panel.Visible = false;
        }
        if (_backdrop != null)
        {
            _backdrop.Visible = false;
        }
    }

    private static string ResolveSelectedHeroId(
        StrategicManagementDashboardViewModel dashboard,
        string selectedHeroId)
    {
        if (!string.IsNullOrWhiteSpace(selectedHeroId) &&
            dashboard.Heroes.Any(hero => string.Equals(hero.HeroId, selectedHeroId, StringComparison.Ordinal)))
        {
            return selectedHeroId;
        }

        return dashboard.Heroes.FirstOrDefault()?.HeroId ?? "";
    }

    private void BindHeroSelectionStep(
        StrategicManagementDashboardViewModel dashboard,
        string notice)
    {
        ClearChildren(_heroList);
        ClearChildren(_musterGrid);
        BindSelectedHeroPanel(new StrategicHeroAssignmentViewModel());
        if (_heroSummaryLabel != null)
        {
            _heroSummaryLabel.Text = "英雄编制";
        }

        if (_noticeLabel != null)
        {
            _noticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "当前没有可调整的英雄。"
                : notice.Trim();
        }

        if (_backButton != null)
        {
            _backButton.Disabled = true;
        }

        foreach (StrategicHeroAssignmentViewModel hero in dashboard.Heroes)
        {
            AddHeroCard(hero, "");
        }
    }

    private void BindCorpsAdjustmentStep(
        StrategicManagementDashboardViewModel dashboard,
        string selectedHeroId,
        string notice)
    {
        ClearChildren(_heroList);
        ClearChildren(_musterGrid);
        StrategicHeroAssignmentViewModel hero = dashboard.Heroes.FirstOrDefault(item =>
            string.Equals(item.HeroId, selectedHeroId, StringComparison.Ordinal)) ?? new StrategicHeroAssignmentViewModel();
        BindSelectedHeroPanel(hero);

        if (_heroSummaryLabel != null)
        {
            _heroSummaryLabel.Text = "英雄编制";
        }

        if (_noticeLabel != null)
        {
            _noticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "选择一个兵种后，会消耗资源和预备兵，并绑定到当前英雄。"
                : notice.Trim();
        }

        if (_backButton != null)
        {
            _backButton.Disabled = true;
        }

        foreach (StrategicHeroAssignmentViewModel option in dashboard.Heroes)
        {
            AddHeroCard(option, selectedHeroId);
        }

        foreach (StrategicMusterTemplateViewModel template in dashboard.SelectedCity.MusterTemplates)
        {
            string corpsDefinitionId = template.CorpsDefinitionId;
            WorldMusterOptionCard card = GameUiSceneFactory.CreateWorldMusterOptionCard(nameof(StrategicMilitaryWorkbenchBinder));
            if (card == null)
            {
                continue;
            }

            card.Bind(
                corpsDefinitionId,
                template.DisplayName,
                BattleUnitPreviewResolver.ResolveAnimatedPreview(template.BattleUnitId),
                template.ReserveForceCost,
                StrategicManagementDashboardPanelBinder.FormatCostsForPresentation(template.CreationCost),
                template.CanCreate,
                StrategicManagementDashboardPanelBinder.FormatReasonsForPresentation(template.DisabledReasons));
            if (template.CanCreate)
            {
                card.Selected += selectedCorpsDefinitionId => _recruitCorps?.Invoke(selectedCorpsDefinitionId);
            }

            _musterGrid?.AddChild(card);
        }
    }

    private void BindSelectedHeroPanel(StrategicHeroAssignmentViewModel hero)
    {
        string displayName = string.IsNullOrWhiteSpace(hero?.DisplayName) ? "英雄" : hero.DisplayName.Trim();
        string corpsName = string.IsNullOrWhiteSpace(hero?.AssignedCorpsDisplayName) ? "未配置编制" : hero.AssignedCorpsDisplayName.Trim();

        if (_selectedHeroPreview != null)
        {
            _selectedHeroPreview.Bind(string.IsNullOrWhiteSpace(hero?.BattleUnitId)
                ? null
                : BattleUnitPreviewResolver.ResolveAnimatedPreview(hero.BattleUnitId));
        }

        if (_selectedHeroNameLabel != null)
        {
            _selectedHeroNameLabel.Text = displayName;
        }

        if (_selectedHeroCorpsLabel != null)
        {
            _selectedHeroCorpsLabel.Text = $"当前：{corpsName}";
        }
    }

    private void AddHeroCard(StrategicHeroAssignmentViewModel hero, string selectedHeroId)
    {
        WorldMilitaryHeroCard card = GameUiSceneFactory.CreateWorldMilitaryWorkbenchHeroCard(nameof(StrategicMilitaryWorkbenchBinder));
        if (card == null)
        {
            return;
        }

        card.Bind(
            hero.HeroId,
            hero.DisplayName,
            BattleUnitPreviewResolver.ResolveAnimatedPreview(hero.BattleUnitId),
            hero.AssignedCorpsDisplayName,
            string.Equals(hero.HeroId, selectedHeroId, StringComparison.Ordinal));
        card.Selected += selectedHeroIdFromCard => _selectHero?.Invoke(selectedHeroIdFromCard);
        _heroList?.AddChild(card);
    }

    private static void ClearChildren(Node node)
    {
        if (node == null)
        {
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
