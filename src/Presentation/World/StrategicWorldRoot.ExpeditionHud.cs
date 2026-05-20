using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool RefreshExpeditionControls()
    {
        WorldSiteState selectedSite = State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site)
            ? site
            : null;

        if (_isExpeditionDrafting)
        {
            ClampExpeditionDraftCounts();
            string sourceName = ResolveSiteDisplayName(_expeditionSourceSiteId);
            AddMutedLine(_actionList, $"出发场域：{sourceName}");
            AddMutedLine(_actionList, $"已选英雄：{BuildExpeditionUnitText()}");
            AddMutedLine(_actionList, $"默认兵团：{GetUnitLabel(HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit)} x{HeroCorpsV0PlayableSliceIds.DefaultCorpsCount}");

            foreach ((string unitTypeId, int available) in GetAvailableExpeditionUnits(_expeditionSourceSiteId))
            {
                _expeditionUnitCounts.TryGetValue(unitTypeId, out int selected);
                AddExpeditionCountRow(
                    GetUnitLabel(unitTypeId),
                    selected,
                    available,
                    delta => AdjustExpeditionUnitCount(unitTypeId, delta));
            }

            /*
            AddExpeditionCountRow(
                $"英雄：{GetUnitLabel(StrategicWorldIds.UnitPlayerKnight)}",
                _expeditionHeroCount,
                availableHeroes,
                AdjustExpeditionHeroCount);
            AddExpeditionCountRow(
                $"小兵：{GetUnitLabel(StrategicWorldIds.UnitMilitia)}",
                _expeditionMilitiaCount,
                availableMilitia,
                AdjustExpeditionMilitiaCount);
            */
            AddExpeditionTargetButton(HasSelectedExpeditionUnits());
            AddExpeditionCancelButton();
            return true;
        }

        if (TryGetSelectedArrivedAssaultArmy(out WorldArmyState arrivedAssaultArmy))
        {
            AddArrivedAssaultChoiceButtons(arrivedAssaultArmy);
            return true;
        }

        if (CanShowSelectedSiteDetailEntry(selectedSite))
        {
            bool canEnter = CanEnterSelectedSiteDetail(out string enterFailureReason);
            Button enterButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (enterButton == null)
            {
                return false;
            }

            enterButton.Text = canEnter
                ? "查看场地详情"
                : $"查看场地详情\n{WorldActionResolver.FormatFailureReason(enterFailureReason)}";
            enterButton.Disabled = !canEnter;
            if (canEnter)
            {
                enterButton.Pressed += EnterSelectedSiteDetail;
            }

            _actionList.AddChild(enterButton);
        }

        if (selectedSite?.OwnerFactionId == State.PlayerFactionId &&
            selectedSite.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged)
        {
            bool canStart = CanStartExpeditionFromSite(_selectedSiteId, out string failureReason);
            Button expeditionButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (expeditionButton == null)
            {
                return false;
            }

            expeditionButton.Text = canStart
                ? "出征\n选择英雄"
                : $"出征\n{WorldActionResolver.FormatFailureReason(failureReason)}";
            expeditionButton.Disabled = !canStart;
            if (canStart)
            {
                expeditionButton.Pressed += BeginExpeditionDraft;
            }

            _actionList.AddChild(expeditionButton);
        }

        return false;
    }

    private void AddExpeditionCountRow(
        string label,
        int selected,
        int available,
        System.Action<int> adjust)
    {
        HBoxContainer countRow = GameUiSceneFactory.CreateWorldExpeditionCountRow(nameof(StrategicWorldRoot));
        if (countRow == null)
        {
            return;
        }

        Button minusButton = GameUiSceneFactory.GetRequiredNode<Button>(
            countRow,
            "MinusButton",
            nameof(StrategicWorldRoot));
        Label countLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            countRow,
            "CountLabel",
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

        if (countLabel != null)
        {
            countLabel.Text = $"{label} {selected}/{available}";
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

        targetButton.Text = _isExpeditionTargeting
            ? "选择目的地中\n右键场域或空地"
            : "选择目的地\n敌方=进攻 己方=进驻 空地=移动";
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

    private static string BuildActionButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
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
