using System;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class StrategicMilitaryWorkbenchBinder
{
    private readonly Control _panel;
    private readonly VBoxContainer _heroList;
    private readonly GridContainer _musterGrid;
    private readonly Label _heroSummaryLabel;
    private readonly Label _noticeLabel;
    private readonly Button _backButton;
    private readonly Action<string> _selectHero;
    private readonly Action<string> _recruitCorps;

    public StrategicMilitaryWorkbenchBinder(
        Control panel,
        VBoxContainer heroList,
        GridContainer musterGrid,
        Label heroSummaryLabel,
        Label noticeLabel,
        Button backButton,
        Action<string> selectHero,
        Action<string> recruitCorps)
    {
        _panel = panel;
        _heroList = heroList;
        _musterGrid = musterGrid;
        _heroSummaryLabel = heroSummaryLabel;
        _noticeLabel = noticeLabel;
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

        if (string.IsNullOrWhiteSpace(selectedHeroId))
        {
            BindHeroSelectionStep(safeDashboard, notice);
            return;
        }

        BindCorpsAdjustmentStep(safeDashboard, selectedHeroId, notice);
    }

    public void Hide()
    {
        if (_panel != null)
        {
            _panel.Visible = false;
        }
    }

    private void BindHeroSelectionStep(
        StrategicManagementDashboardViewModel dashboard,
        string notice)
    {
        ClearChildren(_heroList);
        ClearChildren(_musterGrid);
        if (_heroSummaryLabel != null)
        {
            _heroSummaryLabel.Text = "选择要调整编制的英雄";
        }

        if (_noticeLabel != null)
        {
            _noticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "先选择英雄，再为该英雄招募或替换主编制。"
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

        if (_heroSummaryLabel != null)
        {
            string corpsName = string.IsNullOrWhiteSpace(hero.AssignedCorpsDisplayName)
                ? "未配置编制"
                : hero.AssignedCorpsDisplayName;
            _heroSummaryLabel.Text = $"{hero.DisplayName}    当前编制：{corpsName}";
        }

        if (_noticeLabel != null)
        {
            _noticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "选择一个编制模板后，会消耗资源和预备兵，并绑定到当前英雄。"
                : notice.Trim();
        }

        if (_backButton != null)
        {
            _backButton.Disabled = false;
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
                template.IconPath,
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

    private void AddHeroCard(StrategicHeroAssignmentViewModel hero, string selectedHeroId)
    {
        WorldMilitaryHeroCard card = GameUiSceneFactory.CreateWorldMilitaryHeroCard(nameof(StrategicMilitaryWorkbenchBinder));
        if (card == null)
        {
            return;
        }

        card.Bind(
            hero.HeroId,
            hero.DisplayName,
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
