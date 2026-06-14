using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Definitions.Battle;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattlePreparationHudBinder
{
    public delegate BattleGroupPlanSnapshot ResolvePlanDelegate(string groupKey, bool create);

    public delegate bool CanLaunchPreparedBattleDelegate(BattleStartRequest request, out string failureReason);

    public void BindCompanyRoster(
        VBoxContainer rosterList,
        IReadOnlyList<BattleRuntimeCommandGroupView> playerGroups,
        string selectedGroupKey,
        ResolvePlanDelegate resolvePlan,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones,
        IReadOnlySet<string> explicitRuleGroups,
        BattlePreparationRosterRow.SelectedEventHandler onSelected,
        BattlePreparationRosterRow.DragStartedEventHandler onDragStarted)
    {
        if (rosterList == null)
        {
            return;
        }

        ClearChildren(rosterList);
        foreach (BattleRuntimeCommandGroupView group in playerGroups ?? System.Array.Empty<BattleRuntimeCommandGroupView>())
        {
            BattlePreparationRosterRow row = GameUiSceneFactory.CreateBattlePreparationRosterRow(nameof(WorldSiteRoot));
            if (row == null)
            {
                continue;
            }

            bool selected = string.Equals(group.GroupKey, selectedGroupKey, System.StringComparison.Ordinal);
            row.Bind(
                group.GroupKey,
                group.DisplayName,
                ResolveCompanyPlanStatus(group, resolvePlan?.Invoke(group.GroupKey, create: false), objectiveZones, explicitRuleGroups),
                selected);
            row.Selected += onSelected;
            row.DragStarted += onDragStarted;
            rosterList.AddChild(row);
        }
    }

    public void BindCompactPlanControls(
        Label companyLabel,
        Label objectiveLabel,
        Button moveFirstButton,
        Button attackFirstButton,
        Button holdButton,
        Button startButton,
        BattleRuntimeCommandGroupView selectedGroup,
        string selectedGroupKey,
        BattleGroupPlanSnapshot plan,
        BattleStartRequest request,
        IReadOnlySet<string> explicitRuleGroups,
        CanLaunchPreparedBattleDelegate CanLaunchPreparedBattle)
    {
        if (companyLabel != null)
        {
            companyLabel.Text = selectedGroup?.DisplayName ?? "未选择部队";
        }

        if (objectiveLabel != null)
        {
            objectiveLabel.Text = $"目标：{BattlePreparationPlanUiModel.ResolveObjectiveText(plan, request?.ObjectiveZones)}";
        }

        BindBattlePreparationRuleButton(moveFirstButton, BattleEngagementRule.MoveFirst, plan, selectedGroupKey, explicitRuleGroups);
        BindBattlePreparationRuleButton(attackFirstButton, BattleEngagementRule.AttackFirst, plan, selectedGroupKey, explicitRuleGroups);
        BindBattlePreparationRuleButton(holdButton, BattleEngagementRule.Hold, plan, selectedGroupKey, explicitRuleGroups);

        if (startButton != null)
        {
            string failureReason = "";
            bool canLaunch = CanLaunchPreparedBattle?.Invoke(request, out failureReason) == true;
            startButton.Disabled = !canLaunch;
            startButton.TooltipText = canLaunch ? "开始实时战斗" : failureReason;
        }
    }

    private static BattlePreparationCompanyPlanStatus ResolveCompanyPlanStatus(
        BattleRuntimeCommandGroupView group,
        BattleGroupPlanSnapshot plan,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones,
        IReadOnlySet<string> explicitRuleGroups)
    {
        return BattlePreparationPlanUiModel.ResolveCompanyPlanStatus(
            group,
            plan,
            objectiveZones,
            explicitRuleGroups);
    }

    private static void BindBattlePreparationRuleButton(
        Button button,
        BattleEngagementRule rule,
        BattleGroupPlanSnapshot plan,
        string selectedGroupKey,
        IReadOnlySet<string> explicitRuleGroups)
    {
        if (button == null)
        {
            return;
        }

        bool explicitSelected = plan != null &&
                                plan.EngagementRule == rule &&
                                (explicitRuleGroups?.Contains(selectedGroupKey ?? "") == true);
        button.Text = explicitSelected ? $"✓ {BattlePreparationPlanUiModel.BuildRuleLabel(rule)}" : BattlePreparationPlanUiModel.BuildRuleLabel(rule);
        button.Disabled = string.IsNullOrWhiteSpace(selectedGroupKey);
        button.TooltipText = BattlePreparationPlanUiModel.BuildRuleTooltip(rule);
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren().ToArray())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
