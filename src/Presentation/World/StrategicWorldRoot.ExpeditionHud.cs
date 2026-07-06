using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool RefreshExpeditionControls()
    {
        if (_isExpeditionDrafting)
        {
            ClampExpeditionDraftCounts();
            string sourceName = ResolveSiteDisplayName(_expeditionSourceSiteId);
            AddMutedLine(_actionList, $"出发地点：{sourceName}");
            AddMutedLine(_actionList, $"已选战斗编组：{BuildExpeditionUnitText()}");
            AddMutedLine(_actionList, $"编制：{BuildSelectedDefaultCorpsText()}");

            foreach (StrategicHeroCompanyViewModel company in GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId))
            {
                bool selected = _expeditionHeroIds.Contains(company.HeroId);
                string status = company.CanCreateExpedition
                    ? $"{company.HeroDisplayName} + {company.CorpsDisplayName}"
                    : $"{company.HeroDisplayName} + {company.CorpsDisplayName}\n{FormatStrategicExpeditionFailureReason(company.DisabledReason)}";
                AddExpeditionCountRow(
                    status,
                    BattleUnitPreviewTextureResolver.ResolvePreviewTexture(company.HeroBattleUnitId),
                    BattleUnitPreviewTextureResolver.ResolvePreviewTexture(company.CorpsBattleUnitId),
                    selected ? 1 : 0,
                    company.CanCreateExpedition &&
                    (selected || _expeditionHeroIds.Count < StrategicManagementRules.FirstSliceMaxHeroCompaniesPerExpedition)
                        ? 1
                        : 0,
                    delta => AdjustExpeditionHeroCompanySelection(company.HeroId, delta));
            }

            AddExpeditionTargetButton(HasSelectedExpeditionUnits());
            AddExpeditionCancelButton();
            return true;
        }

        if (TryGetSelectedArrivedAssaultArmy(out WorldArmyState arrivedAssaultArmy))
        {
            AddArrivedAssaultChoiceButtons(arrivedAssaultArmy);
            return true;
        }

        bool handledStrategicLocationActions = TryBuildSelectedStrategicLocationDashboard(
            out StrategicManagementDashboardViewModel selectedLocationDashboard) &&
            selectedLocationDashboard.SelectedLocation.IsCity;

        if (CanShowSelectedSiteDetailEntry())
        {
            bool canEnter = CanEnterSelectedSiteDetail(out string enterFailureReason);
            Button enterButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (enterButton == null)
            {
                return false;
            }

            enterButton.Text = "查看详情";
            enterButton.TooltipText = canEnter
                ? "查看地点详情"
                : $"查看地点详情：{WorldActionResolver.FormatFailureReason(enterFailureReason)}";
            enterButton.Disabled = !canEnter;
            if (canEnter)
            {
                enterButton.Pressed += EnterSelectedSiteDetail;
            }

            _actionList.AddChild(enterButton);
        }

        if (CanStartExpeditionFromSite(_selectedSiteId, out string failureReason))
        {
            Button expeditionButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (expeditionButton == null)
            {
                return false;
            }

            expeditionButton.Text = "出征";
            expeditionButton.TooltipText = "选择战斗编组";
            expeditionButton.Pressed += BeginExpeditionDraft;
            _actionList.AddChild(expeditionButton);
        }
        else if (selectedLocationDashboard?.SelectedLocation.CanManageCity == true)
        {
            Button expeditionButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (expeditionButton == null)
            {
                return false;
            }

            expeditionButton.Text = "出征";
            expeditionButton.TooltipText = FormatStrategicExpeditionFailureReason(failureReason);
            expeditionButton.Disabled = true;
            _actionList.AddChild(expeditionButton);
        }

        if (handledStrategicLocationActions && _actionList.GetChildCount() == 0)
        {
            AddMutedLine(_actionList, "该地点暂未开放玩家经营。");
        }

        return handledStrategicLocationActions;
    }

    private string BuildSelectedDefaultCorpsText()
    {
        StrategicHeroCompanyViewModel[] selected = GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId)
            .Where(company => _expeditionHeroIds.Contains(company.HeroId))
            .OrderBy(company => company.HeroDisplayName)
            .ToArray();
        return selected.Length == 0
            ? "请选择战斗编组"
            : string.Join("、", selected.Select(company =>
                $"{company.CorpsDisplayName} 强度 {company.Strength}/100 等级 {company.Level} 装备 {company.EquipmentLevel}"));
    }

    private void AddExpeditionCountRow(
        string label,
        Texture2D heroPreviewTexture,
        Texture2D corpsPreviewTexture,
        int selected,
        int available,
        System.Action<int> adjust)
    {
        WorldExpeditionCountRow countRow = GameUiSceneFactory.CreateWorldExpeditionCountRow(nameof(StrategicWorldRoot));
        if (countRow == null)
        {
            return;
        }

        countRow.Bind(label, heroPreviewTexture, corpsPreviewTexture, selected, available);

        Button minusButton = GameUiSceneFactory.GetRequiredNode<Button>(
            countRow,
            "MinusButton",
            nameof(StrategicWorldRoot));
        Button plusButton = GameUiSceneFactory.GetRequiredNode<Button>(
            countRow,
            "PlusButton",
            nameof(StrategicWorldRoot));

        if (minusButton != null)
        {
            minusButton.Disabled = selected <= 0;
            minusButton.Pressed += () => adjust?.Invoke(-1);
        }

        if (plusButton != null)
        {
            plusButton.Disabled = selected >= available;
            plusButton.Pressed += () => adjust?.Invoke(1);
        }

        _actionList.AddChild(countRow);
    }

    private void AddExpeditionTargetButton(bool canChooseTarget)
    {
        Button targetButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (targetButton == null)
        {
            return;
        }

        targetButton.Text = "选择目的地";
        targetButton.TooltipText = _isExpeditionTargeting
            ? "左键或右键确认目标"
            : "敌方=进攻，己方=进驻，空地=移动";
        targetButton.Disabled = !canChooseTarget;
        targetButton.Pressed += BeginExpeditionTargeting;
        _actionList.AddChild(targetButton);
    }

    private void AddExpeditionCancelButton()
    {
        Button cancelButton = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(StrategicWorldRoot));
        if (cancelButton == null)
        {
            return;
        }

        cancelButton.Text = "取消出征";
        cancelButton.Pressed += CancelExpeditionDraft;
        _actionList.AddChild(cancelButton);
    }

    private static string BuildActionButtonLabel(WorldActionViewModel action)
    {
        return action?.DisplayName ?? "";
    }

    private static string BuildActionTooltip(WorldActionViewModel action)
    {
        if (action == null)
        {
            return "";
        }

        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            lines.Add(action.Description);
        }

        lines.Add(action.CostLines.Count == 0 ? "无消耗" : $"消耗：{string.Join("，", action.CostLines)}");
        lines.AddRange(action.EffectLines);
        lines.AddRange(action.WarningLines);
        if (!action.IsEnabled && !string.IsNullOrWhiteSpace(action.DisabledReason))
        {
            lines.Add(action.DisabledReason);
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private bool TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity)
    {
        opportunity = null;
        if (string.IsNullOrWhiteSpace(_selectedOpportunityId) || State?.OpportunityStates == null)
        {
            return false;
        }

        if (State.OpportunityStates.TryGetValue(_selectedOpportunityId, out opportunity) &&
            opportunity.Status == WorldOpportunityStatus.Active)
        {
            return true;
        }

        _selectedOpportunityId = "";
        opportunity = null;
        return false;
    }

    private static string BuildOpportunityRewardText(StrategicWorldDefinitionQueries queries, WorldOpportunityDefinition definition)
    {
        if (definition == null || definition.CompletionRewards.Count == 0)
        {
            return "无固定奖励";
        }

        string[] rewards = definition.CompletionRewards
            .Where(reward => reward.Amount != 0 && !string.IsNullOrWhiteSpace(reward.ResourceId))
            .Select(reward => $"{StrategicWorldDisplayNames.GetResourceLabel(queries, reward.ResourceId)} {(reward.Amount > 0 ? "+" : "")}{reward.Amount}")
            .ToArray();
        return rewards.Length == 0 ? "无固定奖励" : string.Join("，", rewards);
    }

    private static string GetOpportunityStatusLabel(WorldOpportunityStatus status)
    {
        return status switch
        {
            WorldOpportunityStatus.Active => "可处理",
            WorldOpportunityStatus.Completed => "已完成",
            WorldOpportunityStatus.Expired => "已消失",
            _ => status.ToString()
        };
    }
}
