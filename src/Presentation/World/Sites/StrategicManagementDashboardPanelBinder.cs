using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class StrategicManagementDashboardPanelBinder
{
    private readonly Label _resourceLabel;
    private readonly Label _overviewLabel;
    private readonly Label _selectionLabel;
    private readonly VBoxContainer _facilityList;
    private readonly Control _facilityBuildCard;
    private readonly Label _facilityBuildTitle;
    private readonly VBoxContainer _facilityBuildList;
    private readonly VBoxContainer _corpsList;
    private readonly VBoxContainer _actionList;
    private readonly Action<string> _buildFacility;
    private readonly Action<string> _createCorps;
    private readonly Action<string> _toggleHeroAssignment;

    public StrategicManagementDashboardPanelBinder(
        Label resourceLabel,
        Label overviewLabel,
        Label selectionLabel,
        VBoxContainer facilityList,
        Control facilityBuildCard,
        Label facilityBuildTitle,
        VBoxContainer facilityBuildList,
        VBoxContainer corpsList,
        VBoxContainer actionList,
        Action<string> buildFacility,
        Action<string> createCorps,
        Action<string> toggleHeroAssignment)
    {
        _resourceLabel = resourceLabel;
        _overviewLabel = overviewLabel;
        _selectionLabel = selectionLabel;
        _facilityList = facilityList;
        _facilityBuildCard = facilityBuildCard;
        _facilityBuildTitle = facilityBuildTitle;
        _facilityBuildList = facilityBuildList;
        _corpsList = corpsList;
        _actionList = actionList;
        _buildFacility = buildFacility;
        _createCorps = createCorps;
        _toggleHeroAssignment = toggleHeroAssignment;
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
            _selectionLabel.Text = $"{city.CityIdentityDisplayName}    设施槽 {city.FacilitySlotsUsed}/{city.FacilitySlotsTotal}";
        }

        BindBuiltFacilities(city);
        BindFacilityOptions(city);
        BindCorpsInstances(city);
        BindTemplatesAndHeroes(safeDashboard, city);
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

    private void BindBuiltFacilities(StrategicCityManagementViewModel city)
    {
        ClearChildren(_facilityList);
        if (city.BuiltFacilities.Count == 0)
        {
            AddMutedLine(_facilityList, "当前城市还没有已建设施。");
            return;
        }

        foreach (StrategicBuiltFacilityViewModel facility in city.BuiltFacilities)
        {
            AddMutedLine(
                _facilityList,
                $"{facility.DisplayName}\n槽位 {facility.SlotCost}    id {facility.FacilityDefinitionId}");
        }
    }

    private void BindFacilityOptions(StrategicCityManagementViewModel city)
    {
        SetFacilityBuildPanelVisible(true);
        ClearChildren(_facilityBuildList);
        if (_facilityBuildTitle != null)
        {
            _facilityBuildTitle.Text = "可建设施";
        }

        if (city.FacilityOptions.Count == 0)
        {
            AddMutedLine(_facilityBuildList, "暂无可建设施定义。");
            return;
        }

        foreach (StrategicFacilityOptionViewModel option in city.FacilityOptions)
        {
            string state = option.CanBuild ? "可建设" : $"不可建设：{FormatReasons(option.DisabledReason)}";
            string facilityDefinitionId = option.FacilityDefinitionId;
            AddActionButton(
                _facilityBuildList,
                $"{option.DisplayName}\n{state}    槽位 {option.SlotCost}    成本 {FormatCosts(option.BuildCost)}",
                !option.CanBuild,
                () => _buildFacility?.Invoke(facilityDefinitionId));
        }
    }

    private void BindCorpsInstances(StrategicCityManagementViewModel city)
    {
        ClearChildren(_corpsList);
        if (city.CorpsInstances.Count == 0)
        {
            AddMutedLine(_corpsList, "当前城市还没有已创建的编制实例。");
            return;
        }

        foreach (StrategicCorpsInstanceViewModel corps in city.CorpsInstances)
        {
            AddMutedLine(
                _corpsList,
                $"{corps.DisplayName}\n强度 {corps.Strength}/100    等级 {corps.Level}    装备 {corps.EquipmentLevel}    状态 {FormatCorpsStatus(corps.Status)}");
        }
    }

    private void BindTemplatesAndHeroes(
        StrategicManagementDashboardViewModel dashboard,
        StrategicCityManagementViewModel city)
    {
        ClearChildren(_actionList);
        AddMutedLine(_actionList, "可创建编制");
        foreach (StrategicMusterTemplateViewModel template in city.MusterTemplates)
        {
            string state = template.CanCreate ? "可创建" : $"不可创建：{FormatReasons(template.DisabledReasons)}";
            string corpsDefinitionId = template.CorpsDefinitionId;
            AddActionButton(
                _actionList,
                $"{template.DisplayName}\n{state}    成本 {FormatCosts(template.CreationCost)}",
                !template.CanCreate,
                () => _createCorps?.Invoke(corpsDefinitionId));
        }

        AddMutedLine(_actionList, "英雄编制");
        bool hasAvailableCorps = city.CorpsInstances.Any(corps =>
            corps.Status == StrategicCorpsInstanceStatus.Garrisoned &&
            string.IsNullOrWhiteSpace(corps.AssignedHeroId));
        foreach (StrategicHeroAssignmentViewModel hero in dashboard.Heroes)
        {
            string heroId = hero.HeroId;
            bool disabled = !hero.HasAssignedCorps && !hasAvailableCorps;
            string assignmentLine = hero.HasAssignedCorps
                ? $"解除编制：{hero.AssignedCorpsDisplayName}    适性 {hero.AptitudeGrade}"
                : hasAvailableCorps
                    ? "分配当前城市可用编制"
                    : "当前城市没有可用驻扎编制";
            AddActionButton(
                _actionList,
                $"{hero.DisplayName}\n{assignmentLine}",
                disabled,
                () => _toggleHeroAssignment?.Invoke(heroId));
        }
    }

    private void BindLocationReadOnlyLists(StrategicLocationDashboardViewModel location)
    {
        ClearChildren(_facilityList);
        AddMutedLine(_facilityList, "非城市地点没有城市设施。");
        if (location.ProductionPerWorldTimePulse.Count > 0)
        {
            AddMutedLine(_facilityList, $"大地图时间产出：{location.ProductionDisplayText}");
        }

        SetFacilityBuildPanelVisible(true);
        ClearChildren(_facilityBuildList);
        if (_facilityBuildTitle != null)
        {
            _facilityBuildTitle.Text = "地点管理";
        }

        AddMutedLine(_facilityBuildList, "该地点不是城市，不开放设施建设。");

        ClearChildren(_corpsList);
        AddMutedLine(_corpsList, "该地点不管理城市驻防编制。");

        ClearChildren(_actionList);
        AddMutedLine(_actionList, "城市建设、编制创建和英雄编制分配只在城市开放。");
        if (location.ProductionPerWorldTimePulse.Count > 0)
        {
            AddMutedLine(_actionList, $"被动产出：{location.ProductionDisplayText}");
        }

        if (location.SourcePermissionTags.Count > 0)
        {
            AddMutedLine(_actionList, $"来源权限：{location.SourcePermissionDisplayText}");
        }
    }

    private static string BuildOverviewText(StrategicCityManagementViewModel city)
    {
        if (string.IsNullOrWhiteSpace(city.LocationId))
        {
            return "战略经营城市未初始化。";
        }

        return string.Join(
            "\n",
            city.DisplayName,
            $"城市底色：{city.CityIdentityDisplayName}",
            $"设施槽：{city.FacilitySlotsUsed}/{city.FacilitySlotsTotal}",
            $"可创建编制：{city.MusterTemplates.Count(item => item.CanCreate)}/{city.MusterTemplates.Count}    已有编制：{city.CorpsInstances.Count}");
    }

    private static string BuildLocationOverviewText(StrategicLocationDashboardViewModel location)
    {
        if (string.IsNullOrWhiteSpace(location.LocationId))
        {
            return "战略经营地点未初始化。";
        }

        return string.Join(
            "\n",
            location.DisplayName,
            $"地点类型：{location.KindDisplayName}",
            $"控制状态：{location.ControlStateDisplayName}",
            $"所属势力：{FormatFactionId(location.OwnerFactionId)}",
            $"来源权限：{(string.IsNullOrWhiteSpace(location.SourcePermissionDisplayText) ? "无" : location.SourcePermissionDisplayText)}",
            $"大地图时间产出：{(string.IsNullOrWhiteSpace(location.ProductionDisplayText) ? "无" : location.ProductionDisplayText)}",
            location.CanManageCity ? "该地点可进入城市经营。" : "该地点不是城市，不开放城市经营命令。");
    }

    private static string BuildResourceLine(IReadOnlyList<StrategicResourceViewModel> resources)
    {
        if (resources == null || resources.Count == 0)
        {
            return "资源未初始化";
        }

        return string.Join("    ", resources.Select(item => $"{item.DisplayName} {item.Amount}"));
    }

    private static string FormatCosts(IReadOnlyList<StrategicResourceCostViewModel> costs)
    {
        return costs == null || costs.Count == 0
            ? "无"
            : string.Join(" / ", costs.Select(item => $"{item.DisplayName} {item.Amount}"));
    }

    private static string FormatReasons(IReadOnlyList<string> reasons)
    {
        return reasons == null || reasons.Count == 0 ? "未知原因" : string.Join(" / ", reasons.Select(FormatReason));
    }

    private static string FormatReasons(string reason)
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
            StrategicFailureReasons.MissingFacility => "缺少设施",
            StrategicFailureReasons.MissingCorpsDefinition => "编制定义缺失",
            StrategicFailureReasons.MissingHero => "英雄缺失",
            StrategicFailureReasons.MissingCorpsInstance => "编制实例缺失",
            StrategicFailureReasons.MissingCityIdentity => "城市底色不匹配",
            StrategicFailureReasons.MissingSourcePermission => "缺少来源权限",
            StrategicFailureReasons.InsufficientResources => "资源不足",
            StrategicFailureReasons.NoProduction => "无可结算产出",
            StrategicFailureReasons.InvalidElapsedWorldTimePulses => "经过时间无效",
            StrategicFailureReasons.FacilitySlotsFull => "设施槽已满",
            StrategicFailureReasons.FactionMismatch => "势力不匹配",
            StrategicFailureReasons.CorpsAlreadyAssigned => "编制已分配",
            StrategicFailureReasons.HeroAlreadyAssigned => "英雄已分配",
            _ => reason
        };
    }

    private static string FormatCorpsStatus(StrategicCorpsInstanceStatus status)
    {
        return status switch
        {
            StrategicCorpsInstanceStatus.Garrisoned => "驻扎",
            StrategicCorpsInstanceStatus.AssignedToHero => "已分配",
            StrategicCorpsInstanceStatus.Expedition => "远征",
            StrategicCorpsInstanceStatus.Recovering => "恢复中",
            StrategicCorpsInstanceStatus.Routed => "溃散",
            StrategicCorpsInstanceStatus.Scattered => "散失",
            StrategicCorpsInstanceStatus.Rebuilding => "重建中",
            _ => "未知"
        };
    }

    private static string FormatFactionId(string factionId)
    {
        return factionId switch
        {
            StrategicManagementIds.FactionPlayer => "玩家势力",
            StrategicManagementIds.FactionEnemy => "敌对势力",
            "" => "未知",
            null => "未知",
            _ => factionId
        };
    }

    private void SetFacilityBuildPanelVisible(bool visible)
    {
        if (_facilityBuildCard != null)
        {
            _facilityBuildCard.Visible = visible;
        }

        if (_facilityBuildTitle != null)
        {
            _facilityBuildTitle.Visible = visible;
        }

        if (_facilityBuildList != null)
        {
            _facilityBuildList.Visible = visible;
        }
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

    private static void AddActionButton(Container parent, string text, bool disabled, Action pressed)
    {
        if (parent == null)
        {
            return;
        }

        Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicManagementDashboardPanelBinder));
        if (button == null)
        {
            return;
        }

        button.Text = text;
        button.Disabled = disabled;
        if (!disabled)
        {
            button.Pressed += () => pressed?.Invoke();
        }

        parent.AddChild(button);
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
