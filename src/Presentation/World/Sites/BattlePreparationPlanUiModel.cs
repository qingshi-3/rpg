using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Presentation.World.Sites;

internal static class BattlePreparationPlanUiModel
{
    public const string StandardFormationId = "default_line";

    public static BattlePreparationCompanyPlanStatus ResolveCompanyPlanStatus(
        BattleRuntimeCommandGroupView group,
        BattleGroupPlanSnapshot plan,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones,
        IReadOnlySet<string> explicitRuleGroups)
    {
        bool placed = IsCompanyPlaced(group);
        bool objectiveSelected = IsObjectiveValid(plan, objectiveZones);
        bool explicitRule = !string.IsNullOrWhiteSpace(group?.GroupKey) &&
                            explicitRuleGroups.Contains(group.GroupKey);
        if (placed && objectiveSelected && explicitRule)
        {
            return BattlePreparationCompanyPlanStatus.Complete;
        }

        return placed || objectiveSelected || explicitRule
            ? BattlePreparationCompanyPlanStatus.Partial
            : BattlePreparationCompanyPlanStatus.Missing;
    }

    public static bool IsCompanyPlaced(BattleRuntimeCommandGroupView group)
    {
        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            if (!IsForcePlaced(force))
            {
                return false;
            }
        }

        return group?.Forces?.Count > 0;
    }

    public static bool ArePlayerRequestSlotsPlaced(BattleStartRequest request)
    {
        bool hasPlayerSlot = false;
        foreach (BattleForceRequest force in request?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            if (force?.Count <= 0)
            {
                continue;
            }

            hasPlayerSlot = true;
            if (!IsForcePlaced(force))
            {
                return false;
            }
        }

        return hasPlayerSlot;
    }

    public static bool IsForcePlaced(BattleForceRequest force)
    {
        if (force == null || force.Count <= 0)
        {
            return false;
        }

        for (int index = 0; index < force.Count; index++)
        {
            if (index >= (force.PreferredPlacements?.Count ?? 0) ||
                force.PreferredPlacements[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsObjectiveValid(
        BattleGroupPlanSnapshot plan,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        return !string.IsNullOrWhiteSpace(plan?.ObjectiveZoneId) &&
               (objectiveZones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>())
               .Any(zone => string.Equals(zone?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
    }

    public static bool IsEngagementRuleDefined(BattleEngagementRule rule)
    {
        return System.Enum.IsDefined(typeof(BattleEngagementRule), rule);
    }

    public static bool ShouldDefaultEngagementRule(BattleGroupPlanSnapshot plan, bool explicitRuleSelected)
    {
        if (plan == null || !IsEngagementRuleDefined(plan.EngagementRule))
        {
            return true;
        }

        return plan.EngagementRule == BattleEngagementRule.AttackFirst &&
               !explicitRuleSelected &&
               string.IsNullOrWhiteSpace(plan.ObjectiveZoneId);
    }

    public static string ResolveFormationId(string currentFormationId, string defaultFormationId)
    {
        string current = currentFormationId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        string fallback = defaultFormationId?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(fallback) ? StandardFormationId : fallback;
    }

    public static string ResolveDefaultFormationId(IEnumerable<BattleForceRequest> forces)
    {
        return (forces ?? System.Array.Empty<BattleForceRequest>())
            .Select(force => force?.DefaultFormationId?.Trim() ?? "")
            .FirstOrDefault(formationId => !string.IsNullOrWhiteSpace(formationId)) ?? "";
    }

    public static string ResolveDefaultFormationId(BattleRuntimeCommandGroupView group)
    {
        return group?.DefaultFormationId?.Trim() ?? "";
    }

    public static string BuildObjectiveLabel(BattleObjectiveZoneSnapshot zone)
    {
        if (!string.IsNullOrWhiteSpace(zone?.DisplayName))
        {
            return zone.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(zone?.ObjectiveZoneId) ? "目标区域" : zone.ObjectiveZoneId;
    }

    public static string ResolveObjectiveText(
        BattleGroupPlanSnapshot plan,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.ObjectiveZoneId))
        {
            return "未选";
        }

        BattleObjectiveZoneSnapshot zone = (objectiveZones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>())
            .FirstOrDefault(item => string.Equals(item?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
        return zone == null ? "已失效" : BuildObjectiveLabel(zone);
    }

    public static string BuildRuleLabel(BattleEngagementRule rule)
    {
        return rule switch
        {
            BattleEngagementRule.MoveFirst => "推进",
            BattleEngagementRule.AttackFirst => "强攻",
            BattleEngagementRule.Hold => "坚守",
            BattleEngagementRule.FireOnTheMove => "边走边打",
            BattleEngagementRule.RetreatFirst => "撤退优先",
            BattleEngagementRule.ProtectHero => "护卫英雄",
            _ => "推进"
        };
    }

    public static string BuildRuleDetail(BattleEngagementRule rule)
    {
        return rule switch
        {
            BattleEngagementRule.AttackFirst => "发现敌人优先接战",
            BattleEngagementRule.Hold => "守住当前位置",
            _ => "先推进到目标区"
        };
    }

    public static string BuildRuleTooltip(BattleEngagementRule rule)
    {
        return BuildRuleDetail(rule);
    }
}
