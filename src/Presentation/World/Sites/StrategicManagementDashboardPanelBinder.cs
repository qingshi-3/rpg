using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class StrategicManagementDashboardPanelBinder
{
    private readonly Label _resourceLabel;
    private readonly Label _overviewLabel;
    private readonly Label _selectionLabel;
    private readonly VBoxContainer _buildingList;
    private readonly Label _buildingBuildTitle;
    private readonly GridContainer _buildingBuildList;
    private readonly Action<string> _selectBuildingForPlacement;

    public StrategicManagementDashboardPanelBinder(
        Label resourceLabel,
        Label overviewLabel,
        Label selectionLabel,
        VBoxContainer buildingList,
        Label buildingBuildTitle,
        GridContainer buildingBuildList,
        Action<string> selectBuildingForPlacement)
    {
        _resourceLabel = resourceLabel;
        _overviewLabel = overviewLabel;
        _selectionLabel = selectionLabel;
        _buildingList = buildingList;
        _buildingBuildTitle = buildingBuildTitle;
        _buildingBuildList = buildingBuildList;
        _selectBuildingForPlacement = selectBuildingForPlacement;
    }

    public void Bind(StrategicManagementDashboardViewModel dashboard)
    {
        StrategicManagementDashboardViewModel safeDashboard = dashboard ?? new StrategicManagementDashboardViewModel();
        StrategicCityManagementViewModel city = safeDashboard.SelectedCity ?? new StrategicCityManagementViewModel();

        if (_resourceLabel != null)
        {
            _resourceLabel.Text = BuildResourceLine(safeDashboard.Resources);
        }

        if (_overviewLabel != null)
        {
            _overviewLabel.Text = BuildOverviewText(city);
        }

        if (_selectionLabel != null)
        {
            _selectionLabel.Text = $"兵力 {city.ActiveForces + city.ReserveForces}/{city.CityForceCapacity}    预备 {city.ReserveForces}    恢复 +{city.ReserveRecoveryPerElapsedPulse}/世界脉冲";
        }

        BindBuildings(city);
        BindBuildingOptions(city);
    }

    public void BindLocation(StrategicManagementDashboardViewModel dashboard)
    {
        StrategicManagementDashboardViewModel safeDashboard = dashboard ?? new StrategicManagementDashboardViewModel();
        StrategicLocationDashboardViewModel location = safeDashboard.SelectedLocation ?? new StrategicLocationDashboardViewModel();

        if (_resourceLabel != null)
        {
            _resourceLabel.Text = BuildResourceLine(safeDashboard.Resources);
        }

        if (_overviewLabel != null)
        {
            _overviewLabel.Text = BuildLocationOverviewText(location);
        }

        if (_selectionLabel != null)
        {
            _selectionLabel.Text = $"{location.KindDisplayName}    {location.ControlStateDisplayName}";
        }

        BindLocationReadOnlyLists(location);
    }

    private void BindBuildings(StrategicCityManagementViewModel city)
    {
        ClearChildren(_buildingList);
        if (city.Buildings.Count == 0)
        {
            AddMutedLine(_buildingList, "暂无建筑");
        }
        else
        {
            foreach (StrategicBuildingInstanceViewModel building in city.Buildings)
            {
                string state = building.IsConstructed ? "已建成" : "建设中";
                AddMutedLine(
                    _buildingList,
                    $"{building.DisplayName} · {building.RegionDisplayName} · {building.FootprintWidth}x{building.FootprintHeight} · {state}");
            }
        }

        if (city.ConstructionRegions.Count == 0)
        {
            AddMutedLine(_buildingList, "暂无开放建设区");
            return;
        }

        AddMutedLine(_buildingList, "建设区");
        foreach (StrategicConstructionRegionViewModel region in city.ConstructionRegions)
        {
            AddMutedLine(_buildingList, $"{region.DisplayName} · {region.Width}x{region.Height}");
        }
    }

    private void BindBuildingOptions(StrategicCityManagementViewModel city)
    {
        ClearChildren(_buildingBuildList);
        if (_buildingBuildTitle != null)
        {
            _buildingBuildTitle.Text = "可建设建筑";
        }

        if (city.BuildingOptions.Count == 0)
        {
            AddMutedLine(_buildingBuildList, "暂无可建设建筑定义。");
            return;
        }

        foreach (StrategicBuildingOptionViewModel option in city.BuildingOptions)
        {
            string buildingDefinitionId = option.BuildingDefinitionId;
            WorldBuildingOptionCard card = GameUiSceneFactory.CreateWorldBuildingOptionCard(nameof(StrategicManagementDashboardPanelBinder));
            if (card == null)
            {
                continue;
            }

            card.Bind(
                buildingDefinitionId,
                option.DisplayName,
                option.IconPath,
                option.FootprintWidth,
                option.FootprintHeight,
                FormatCostsForPresentation(option.BuildCost),
                option.CanBuild,
                option.CanBuild ? "" : FormatReasonsForPresentation(option.DisabledReason));
            if (option.CanBuild)
            {
                card.Selected += _ => _selectBuildingForPlacement?.Invoke(buildingDefinitionId);
            }

            _buildingBuildList?.AddChild(card);
        }
    }

    private void BindLocationReadOnlyLists(StrategicLocationDashboardViewModel location)
    {
        ClearChildren(_buildingList);
        AddMutedLine(_buildingList, "非城市地点没有城市建设区。");
        if (location.ProductionPerWorldTimePulse.Count > 0)
        {
            AddMutedLine(_buildingList, $"大地图时间产出：{location.ProductionDisplayText}");
        }

        if (location.SourcePermissionTags.Count > 0)
        {
            AddMutedLine(_buildingList, $"来源权限：{location.SourcePermissionDisplayText}");
        }

        ClearChildren(_buildingBuildList);
        if (_buildingBuildTitle != null)
        {
            _buildingBuildTitle.Text = "地点管理";
        }

        AddMutedLine(_buildingBuildList, "该地点不是城市，不开放建筑建设。");

    }

    private static string BuildOverviewText(StrategicCityManagementViewModel city)
    {
        if (string.IsNullOrWhiteSpace(city.LocationId))
        {
            return "战略经营城市未初始化。";
        }

        return $"{city.DisplayName} · {city.CityIdentityDisplayName}";
    }

    private static string BuildLocationOverviewText(StrategicLocationDashboardViewModel location)
    {
        if (string.IsNullOrWhiteSpace(location.LocationId))
        {
            return "战略经营地点未初始化。";
        }

        string production = string.IsNullOrWhiteSpace(location.ProductionDisplayText)
            ? "无产出"
            : location.ProductionDisplayText;
        return $"{location.DisplayName} · {location.KindDisplayName} · {location.ControlStateDisplayName} · {production}";
    }

    private static string BuildResourceLine(IReadOnlyList<StrategicResourceViewModel> resources)
    {
        if (resources == null || resources.Count == 0)
        {
            return "资源未初始化";
        }

        return string.Join("    ", resources.Select(item => $"{item.DisplayName} {item.Amount}"));
    }

    internal static string FormatCostsForPresentation(IReadOnlyList<StrategicResourceCostViewModel> costs)
    {
        return costs == null || costs.Count == 0
            ? "无"
            : string.Join(" / ", costs.Select(item => $"{item.DisplayName} {item.Amount}"));
    }

    internal static string FormatReasonsForPresentation(IReadOnlyList<string> reasons)
    {
        return reasons == null || reasons.Count == 0
            ? "未知原因"
            : string.Join(" / ", reasons.Select(FormatReason));
    }

    internal static string FormatReasonsForPresentation(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "未知原因" : FormatReason(reason);
    }

    private static string FormatReason(string reason)
    {
        return FormatFailureReason(reason);
    }

    internal static string FormatFailureReason(string reason)
    {
        return reason switch
        {
            StrategicFailureReasons.MissingDefinitions => "定义缺失",
            StrategicFailureReasons.MissingCity => "城市缺失",
            StrategicFailureReasons.MissingLocation => "地点缺失",
            StrategicFailureReasons.MissingBuilding => "建筑缺失",
            StrategicFailureReasons.MissingConstructionRegion => "建设区域缺失",
            StrategicFailureReasons.MissingCorpsDefinition => "编制定义缺失",
            StrategicFailureReasons.MissingHero => "英雄缺失",
            StrategicFailureReasons.MissingCorpsInstance => "编制实例缺失",
            StrategicFailureReasons.MissingCityIdentity => "城市底色不匹配",
            StrategicFailureReasons.MissingSourcePermission => "缺少来源权限",
            StrategicFailureReasons.InsufficientResources => "资源不足",
            StrategicFailureReasons.NoProduction => "无可结算产出",
            StrategicFailureReasons.InvalidElapsedWorldTimePulses => "经过时间无效",
            StrategicFailureReasons.WorldTimePaused => "大地图时间已暂停",
            StrategicFailureReasons.BuildingPlacementOutOfBounds => "建筑超出建设区域",
            StrategicFailureReasons.BuildingPlacementOccupied => "建筑占地被占用",
            StrategicFailureReasons.InsufficientReserveForces => "预备兵不足",
            StrategicFailureReasons.CityForceCapacityFull => "城市兵力容量已满",
            StrategicFailureReasons.InvalidReplenishmentTarget => "补员目标无效",
            StrategicFailureReasons.CorpsAlreadyFullStrength => "编制已满员",
            StrategicFailureReasons.FactionMismatch => "势力不匹配",
            StrategicFailureReasons.CorpsAlreadyAssigned => "编制已分配",
            StrategicFailureReasons.HeroAlreadyAssigned => "英雄已分配",
            StrategicFailureReasons.HeroHasNoAssignedCorps => "英雄没有已分配编制",
            StrategicFailureReasons.CorpsNotAssignedToHero => "编制未分配给该英雄",
            StrategicFailureReasons.HeroAlreadyOnExpedition => "英雄已在出征中",
            StrategicFailureReasons.CorpsAlreadyOnExpedition => "编制已在出征中",
            StrategicFailureReasons.SourceLocationNotOwned => "出发地点未控制",
            StrategicFailureReasons.SameLocationTarget => "目标不能是当前地点",
            StrategicFailureReasons.TargetLocationNotOwned => "目标地点未控制",
            StrategicFailureReasons.TargetLocationNotAttackable => "目标地点不可攻击",
            StrategicFailureReasons.ExpeditionCapacityFull => "出征队伍已满",
            StrategicFailureReasons.ExpeditionNotCommandable => "出征队伍不可指挥",
            StrategicFailureReasons.UnsupportedExpeditionIntent => "不支持的出征意图",
            StrategicFailureReasons.InvalidExpeditionParticipants => "出征成员无效",
            _ => reason
        };
    }

    private static void AddMutedLine(Container parent, string text)
    {
        if (parent == null)
        {
            return;
        }

        Label label = GameUiSceneFactory.CreateWorldMutedLine(nameof(StrategicManagementDashboardPanelBinder));
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
}
