using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private BattleFaction ResolveBattleFaction(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return BattleFaction.Neutral;
        }

        return factionId == StrategicWorldRuntime.State?.PlayerFactionId ||
               factionId == StrategicWorldIds.FactionPlayer
            ? BattleFaction.Player
            : BattleFaction.Enemy;
    }

    private FacilityInstance ResolveFacilityInSlot(WorldSiteState site, string slotId)
    {
        return string.IsNullOrWhiteSpace(slotId)
            ? null
            : site?.Facilities.FirstOrDefault(item => item.SlotId == slotId && item.State != FacilityState.Destroyed);
    }

    private IReadOnlyList<WorldActionViewModel> ResolveBuildActionsForSlot(WorldSiteState site, FacilitySlotDefinition slot)
    {
        if (site == null ||
            slot == null)
        {
            return System.Array.Empty<WorldActionViewModel>();
        }

        HashSet<string> actionIds = ResolveBuildActionIdsForSlot(slot).ToHashSet();
        if (actionIds.Count == 0)
        {
            return System.Array.Empty<WorldActionViewModel>();
        }

        IReadOnlyList<WorldActionViewModel> actions = _worldActionResolver.GetAvailableActions(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            _siteHudSiteId,
            slot.SlotId);
        return actions
            .Where(action => actionIds.Contains(action.ActionId))
            .ToArray();
    }

    private IReadOnlyList<string> ResolveBuildActionIdsForSlot(FacilitySlotDefinition slot)
    {
        if (slot == null)
        {
            return System.Array.Empty<string>();
        }

        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        return queries.Actions.Values
            .Where(action =>
            {
                string facilityId = ResolveAddedFacilityId(action);
                return !string.IsNullOrWhiteSpace(facilityId) &&
                       slot.AllowedFacilityIds.Contains(facilityId);
            })
            .Select(action => action.Id)
            .ToArray();
    }

    private static string ResolveAddedFacilityId(WorldActionDefinition action)
    {
        return action?.Effects
            .FirstOrDefault(effect => effect.Kind == WorldEffectKind.AddFacility &&
                                      !string.IsNullOrWhiteSpace(effect.FacilityId))
            ?.FacilityId ?? "";
    }

    private string BuildPlacementDisplayName(string placementId)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        return placement == null ? placementId : BuildPlacementDisplayName(placement);
    }

    private string BuildPlacementDisplayName(WorldSiteUnitPlacement placement)
    {
        return _battleUnitFactory.ResolveUnitInstanceDisplayName(placement.UnitTypeId, placement.UnitIndex - 1);
    }

    private static string FormatPlacementFailure(string failureReason)
    {
        return failureReason switch
        {
            "placement_cell_occupied" => "无法放置：目标地块已有驻军。",
            "placement_cell_blocked" => "无法放置：目标地块不可行走。",
            "placement_cell_water" => "无法放置：该单位不能进入水域。",
            "placement_cell_invalid" => "无法放置：目标地块无效。",
            "missing_placement" => "无法放置：驻军记录不存在。",
            _ => "无法放置：目标地块无效。"
        };
    }

    private static string BuildAllowedFacilityNames(FacilitySlotDefinition slot, StrategicWorldDefinitionQueries queries)
    {
        return slot.AllowedFacilityIds.Count == 0
            ? "未配置"
            : string.Join("、", slot.AllowedFacilityIds.Select(id => queries.GetFacility(id)?.DisplayName ?? id));
    }

    private static string BuildActionButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
    }

    private static string BuildFacilityBuildButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
    }

    private static string BuildActionTooltip(WorldActionViewModel action)
    {
        if (action == null)
        {
            return "";
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            lines.Add(action.Description);
        }

        lines.AddRange(action.EffectLines);
        lines.AddRange(action.WarningLines);
        if (!action.IsEnabled && !string.IsNullOrWhiteSpace(action.DisabledReason))
        {
            lines.Add(action.DisabledReason);
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private bool IsFacilityBuildAction(string actionId)
    {
        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        return !string.IsNullOrWhiteSpace(ResolveAddedFacilityId(queries.GetAction(actionId)));
    }

    private static void AddMutedLine(Container parent, string text)
    {
        Label label = GameUiSceneFactory.CreateWorldMutedLine(nameof(WorldSiteRoot));
        if (label == null)
        {
            return;
        }

        label.Text = text;
        parent.AddChild(label);
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

    private static string GetBattleOutcomeLabel(BattleOutcome outcome)
    {
        return outcome switch
        {
            BattleOutcome.Victory => "战斗胜利",
            BattleOutcome.Defeat => "战斗失败",
            BattleOutcome.Withdraw => "已撤退",
            BattleOutcome.Disaster => "惨败",
            BattleOutcome.None => "非战时",
            _ => "战斗结束"
        };
    }

    private static string GetControlStateLabel(SiteControlState state)
    {
        return state switch
        {
            SiteControlState.Unknown => "未知",
            SiteControlState.Neutral => "中立",
            SiteControlState.Hostile => "敌控",
            SiteControlState.Contested => "争夺中",
            SiteControlState.PlayerHeld => "玩家控制",
            SiteControlState.Damaged => "受损",
            SiteControlState.Lost => "丢失",
            _ => "未知"
        };
    }

    private static string GetSiteModeLabel(WorldSiteMode mode)
    {
        return mode switch
        {
            WorldSiteMode.Peacetime => "非战时",
            WorldSiteMode.Wartime => "战时",
            WorldSiteMode.Aftermath => "战后",
            _ => "未知"
        };
    }

    private static string GetFacilityStateLabel(FacilityState state)
    {
        return state switch
        {
            FacilityState.Planned => "规划",
            FacilityState.Building => "建造中",
            FacilityState.Active => "运行",
            FacilityState.Damaged => "受损",
            FacilityState.Disabled => "停用",
            FacilityState.Destroyed => "摧毁",
            _ => "未知"
        };
    }

    private string GetUnitLabel(string unitTypeId)
    {
        return _battleUnitFactory.ResolveUnitDisplayName(unitTypeId);
    }
}
