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
    private readonly VBoxContainer _buildingList;
    private readonly Label _buildingBuildTitle;
    private readonly GridContainer _buildingBuildList;
    private readonly VBoxContainer _conscriptionList;
    private readonly VBoxContainer _corpsList;
    private readonly Action<string> _selectBuildingForPlacement;
    private readonly Action _manualConscript;
    private readonly Action<string> _setAutoConscriptionIntensity;
    private readonly Action<string> _replenishCorps;
    private readonly Action<string> _toggleHeroAssignment;

    public StrategicManagementDashboardPanelBinder(
        Label resourceLabel,
        Label overviewLabel,
        Label selectionLabel,
        VBoxContainer buildingList,
        Label buildingBuildTitle,
        GridContainer buildingBuildList,
        VBoxContainer conscriptionList,
        VBoxContainer corpsList,
        Action<string> selectBuildingForPlacement,
        Action manualConscript,
        Action<string> setAutoConscriptionIntensity,
        Action<string> replenishCorps,
        Action<string> toggleHeroAssignment)
    {
        _resourceLabel = resourceLabel;
        _overviewLabel = overviewLabel;
        _selectionLabel = selectionLabel;
        _buildingList = buildingList;
        _buildingBuildTitle = buildingBuildTitle;
        _buildingBuildList = buildingBuildList;
        _conscriptionList = conscriptionList;
        _corpsList = corpsList;
        _selectBuildingForPlacement = selectBuildingForPlacement;
        _manualConscript = manualConscript;
        _setAutoConscriptionIntensity = setAutoConscriptionIntensity;
        _replenishCorps = replenishCorps;
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
            _selectionLabel.Text = $"{city.CityIdentityDisplayName}    预备兵 {city.ReserveForces}    兵力 {city.ActiveForces + city.ReserveForces}/{city.CityForceCapacity}";
        }

        BindBuildings(city);
        BindBuildingOptions(city);
        BindConscription(city.Conscription);
        BindCorpsAndHeroes(safeDashboard, city);
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
            AddMutedLine(_buildingList, "当前城市还没有已建建筑。");
        }
        else
        {
            foreach (StrategicBuildingInstanceViewModel building in city.Buildings)
            {
                string state = building.IsConstructed ? "已建成" : "建设中";
                AddMutedLine(
                    _buildingList,
                    $"{building.DisplayName}\n区域 {building.RegionDisplayName}    坐标 {building.GridX},{building.GridY}    占地 {building.FootprintWidth}x{building.FootprintHeight}    {state}");
            }
        }

        if (city.ConstructionRegions.Count == 0)
        {
            AddMutedLine(_buildingList, "当前城市没有开放建设区域。");
            return;
        }

        AddMutedLine(_buildingList, "建设区域");
        foreach (StrategicConstructionRegionViewModel region in city.ConstructionRegions)
        {
            AddMutedLine(
                _buildingList,
                $"{region.DisplayName}\n范围 {region.OriginX},{region.OriginY} / {region.Width}x{region.Height}");
        }
    }

    private void BindBuildingOptions(StrategicCityManagementViewModel city)
    {
        ClearChildren(_buildingBuildList);
        if (_buildingBuildTitle != null)
        {
            _buildingBuildTitle.Text = "可建建筑";
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
                option.CanBuild);
            if (option.CanBuild)
            {
                card.Selected += _ => _selectBuildingForPlacement?.Invoke(buildingDefinitionId);
            }

            _buildingBuildList?.AddChild(card);
        }
    }

    private void BindConscription(StrategicConscriptionViewModel conscription)
    {
        ClearChildren(_conscriptionList);
        StrategicConscriptionViewModel safeConscription = conscription ?? new StrategicConscriptionViewModel();
        AddMutedLine(
            _conscriptionList,
            $"兵力：现役 {safeConscription.ActiveForces} / 预备 {safeConscription.ReserveForces} / 容量 {safeConscription.CityForceCapacity} / 剩余 {safeConscription.RemainingForceCapacity}");

        StrategicConscriptionManualOptionViewModel manual = safeConscription.ManualOption ?? new StrategicConscriptionManualOptionViewModel();
        string manualLine = $"手动征兵\n预备兵 +{manual.ReserveGain}    成本 {FormatCostsForPresentation(manual.Cost)}";
        if (!manual.CanConscript)
        {
            manualLine = $"{manualLine}\n不可执行：{FormatReasonsForPresentation(manual.DisabledReason)}";
        }

        AddActionButton(
            _conscriptionList,
            manualLine,
            !manual.CanConscript,
            () => _manualConscript?.Invoke());

        AddMutedLine(_conscriptionList, "自动征兵力度");
        foreach (StrategicConscriptionIntensityOptionViewModel option in safeConscription.IntensityOptions)
        {
            string currentText = option.IsCurrent ? "当前" : "可选择";
            string requirementText = option.RequiresTrainingGround ? "需要训练场" : "无需训练场";
            string optionLine =
                $"{option.DisplayName}    {currentText}\n每次大地图结算：预备兵 +{option.ReserveGain}    成本 {FormatCostsForPresentation(option.Cost)}    {requirementText}";
            bool disabled = option.IsCurrent || !option.CanSelect;
            if (!option.CanSelect && !option.IsCurrent)
            {
                optionLine = $"{optionLine}\n不可选择：{FormatReasonsForPresentation(option.DisabledReason)}";
            }

            string intensityId = option.IntensityId;
            AddActionButton(
                _conscriptionList,
                optionLine,
                disabled,
                () => _setAutoConscriptionIntensity?.Invoke(intensityId));
        }
    }

    private void BindCorpsAndHeroes(
        StrategicManagementDashboardViewModel dashboard,
        StrategicCityManagementViewModel city)
    {
        ClearChildren(_corpsList);
        AddMutedLine(_corpsList, "现有编制");
        if (city.CorpsInstances.Count == 0)
        {
            AddMutedLine(_corpsList, "当前城市还没有已创建的编制实例。");
        }

        foreach (StrategicCorpsInstanceViewModel corps in city.CorpsInstances)
        {
            string replenishLine = corps.Strength >= 100
                ? "无需补员"
                : corps.CanReplenish
                    ? $"可补员    预备兵 {corps.ReplenishReserveCost}    成本 {FormatCostsForPresentation(corps.ReplenishCost)}"
                    : $"不可补员：{FormatReasonsForPresentation(corps.ReplenishDisabledReason)}";
            string corpsInstanceId = corps.CorpsInstanceId;
            AddActionButton(
                _corpsList,
                $"{corps.DisplayName}\n强度 {corps.Strength}/100    等级 {corps.Level}    装备 {corps.EquipmentLevel}    状态 {FormatCorpsStatus(corps.Status)}\n{replenishLine}",
                corps.Strength >= 100 || !corps.CanReplenish,
                () => _replenishCorps?.Invoke(corpsInstanceId));
        }

        AddMutedLine(_corpsList, "英雄编制");
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
                    : "当前城市没有可用驻守编制";
            AddActionButton(
                _corpsList,
                $"{hero.DisplayName}\n{assignmentLine}",
                disabled,
                () => _toggleHeroAssignment?.Invoke(heroId));
        }
    }

    private void BindLocationReadOnlyLists(StrategicLocationDashboardViewModel location)
    {
        ClearChildren(_buildingList);
        AddMutedLine(_buildingList, "非城市地点没有城市建设区域。");
        if (location.ProductionPerWorldTimePulse.Count > 0)
        {
            AddMutedLine(_buildingList, $"大地图时间产出：{location.ProductionDisplayText}");
        }

        ClearChildren(_buildingBuildList);
        if (_buildingBuildTitle != null)
        {
            _buildingBuildTitle.Text = "地点管理";
        }

        AddMutedLine(_buildingBuildList, "该地点不是城市，不开放建筑建设。");

        ClearChildren(_conscriptionList);
        AddMutedLine(_conscriptionList, "该地点不开放城市征兵。");

        ClearChildren(_corpsList);
        AddMutedLine(_corpsList, "该地点不管理城市预备兵和编制。");
        if (location.ProductionPerWorldTimePulse.Count > 0)
        {
            AddMutedLine(_corpsList, $"被动产出：{location.ProductionDisplayText}");
        }

        if (location.SourcePermissionTags.Count > 0)
        {
            AddMutedLine(_corpsList, $"来源权限：{location.SourcePermissionDisplayText}");
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
            $"兵力：现役 {city.ActiveForces} / 预备 {city.ReserveForces} / 容量 {city.CityForceCapacity} / 剩余 {city.RemainingForceCapacity}",
            $"建筑：{city.Buildings.Count} 项    建设区域：{city.ConstructionRegions.Count}",
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
            StrategicFailureReasons.InvalidConscriptionIntensity => "征兵力度无效",
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

    private static string FormatCorpsStatus(StrategicCorpsInstanceStatus status)
    {
        return status switch
        {
            StrategicCorpsInstanceStatus.Garrisoned => "驻守",
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
