using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void RefreshAll()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        StrategicWorldDefinitionQueries queries = new(Definition);
        RefreshResources();
        RefreshSiteButtons(queries);
        RefreshCurrentStrategicWorldPanel(queries);
        RefreshWorldClockLabel();
        _noticeLabel.Text = StrategicWorldRuntime.LastNotice;
        RefreshStrategicFog();
        QueueStrategicOverlayRedraw();
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds >= 16)
        {
            GameLog.Info(
                nameof(StrategicWorldRoot),
                $"StrategicRefreshAllCost selectedSite={_selectedSiteId} selectedOpportunity={_selectedOpportunityId} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
    }

    private WorldUiMode ResolveStrategicWorldUiMode()
    {
        return _isExpeditionDrafting || _isExpeditionTargeting
            ? WorldUiMode.ExpeditionDraft
            : WorldUiMode.StrategicSelection;
    }

    private void RefreshCurrentStrategicWorldPanel(StrategicWorldDefinitionQueries queries)
    {
        switch (ResolveStrategicWorldUiMode())
        {
            case WorldUiMode.ExpeditionDraft:
                BindExpeditionDraftPanel(queries);
                break;
            default:
                BindStrategicSelectionPanel(queries);
                break;
        }
    }

    private void BindStrategicSelectionPanel(StrategicWorldDefinitionQueries queries)
    {
        RefreshDetail(queries);
        RefreshActions();
    }

    private void BindExpeditionDraftPanel(StrategicWorldDefinitionQueries queries)
    {
        RefreshDetail(queries);
        RefreshActions();
    }

    private void RefreshDetail(StrategicWorldDefinitionQueries queries)
    {
        if (TryRefreshOpportunityDetail(queries))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedSiteId))
        {
            _selectedSiteId = "";
            HideWorldDetailSections();
            return;
        }

        SetSiteDetailSectionsVisible(true);
        WorldSiteDefinition definition = queries.GetSite(_selectedSiteId);

        // Large-map detail consumes Strategic Management read models. Legacy WorldSiteState
        // remains map/scene scaffolding until later slices, but it is not management truth here.
        StrategicManagementRuntime.EnsureInitialized();
        if (!StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(_selectedSiteId, out string locationId))
        {
            BindUnmappedStrategicLocationDetail(definition);
            return;
        }

        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildLocationDashboard(
            StrategicManagementIds.FactionPlayer,
            locationId);
        StrategicLocationDashboardViewModel location = dashboard.SelectedLocation ?? new StrategicLocationDashboardViewModel();
        StrategicCityManagementViewModel city = dashboard.SelectedCity ?? new StrategicCityManagementViewModel();

        string title = string.IsNullOrWhiteSpace(location.DisplayName)
            ? definition?.DisplayName ?? _selectedSiteId
            : location.DisplayName;
        string kind = string.IsNullOrWhiteSpace(location.KindDisplayName)
            ? "战略地点"
            : location.KindDisplayName;

        _siteTitleLabel.Text = $"{title}  ·  {kind}";
        _siteBodyLabel.Text = BuildStrategicLocationBody(definition, location, city);

        ClearChildren(_facilityList);
        AddStrategicFacilityLines(_facilityList, location, city);

        ClearChildren(_garrisonList);
        AddStrategicCorpsLines(_garrisonList, location, city);
    }

    private void BindUnmappedStrategicLocationDetail(WorldSiteDefinition definition)
    {
        string title = definition?.DisplayName ?? _selectedSiteId;
        _siteTitleLabel.Text = $"{title}  ·  战略经营未配置";
        _siteBodyLabel.Text = string.Join(
            "\n",
            string.IsNullOrWhiteSpace(definition?.Description) ? "该大地图地点还没有战略经营配置。" : definition.Description,
            "",
            "该地点暂时只保留地图展示，不显示旧设施或驻军事实。");

        ClearChildren(_facilityList);
        AddMutedLine(_facilityList, "等待战略经营地点配置。");

        ClearChildren(_garrisonList);
        AddMutedLine(_garrisonList, "等待战略经营编制配置。");
    }

    private static string BuildStrategicLocationBody(
        WorldSiteDefinition definition,
        StrategicLocationDashboardViewModel location,
        StrategicCityManagementViewModel city)
    {
        List<string> detailLines = new();
        if (!string.IsNullOrWhiteSpace(definition?.Description))
        {
            detailLines.Add(definition.Description);
            detailLines.Add("");
        }

        detailLines.Add($"控制状态：{FormatStrategicText(location?.ControlStateDisplayName, "未知")}");
        detailLines.Add($"所属势力：{FormatStrategicFactionId(location?.OwnerFactionId)}");
        detailLines.Add($"来源权限：{FormatStrategicText(location?.SourcePermissionDisplayText, "无")}");
        detailLines.Add($"大地图时间产出：{FormatStrategicText(location?.ProductionDisplayText, "无")}");

        if (location?.CanManageCity == true)
        {
            int creatableTemplates = city?.MusterTemplates?.Count(template => template.CanCreate) ?? 0;
            int totalTemplates = city?.MusterTemplates?.Count ?? 0;
            int corpsCount = city?.CorpsInstances?.Count ?? 0;
            detailLines.Add("");
            detailLines.Add($"城市底色：{FormatStrategicText(city?.CityIdentityDisplayName, "未配置")}");
            detailLines.Add($"设施槽：{city?.FacilitySlotsUsed ?? 0}/{city?.FacilitySlotsTotal ?? 0}");
            detailLines.Add($"可创建编制：{creatableTemplates}/{totalTemplates}");
            detailLines.Add($"已有编制：{corpsCount}");
        }
        else
        {
            detailLines.Add("");
            detailLines.Add("城市经营：该地点不开放城市经营。");
        }

        return string.Join("\n", detailLines);
    }

    private void AddStrategicFacilityLines(
        VBoxContainer list,
        StrategicLocationDashboardViewModel location,
        StrategicCityManagementViewModel city)
    {
        if (list == null)
        {
            return;
        }

        if (location?.CanManageCity == true)
        {
            if (city?.BuiltFacilities == null || city.BuiltFacilities.Count == 0)
            {
                AddMutedLine(list, "当前城市还没有已建设施。");
                return;
            }

            foreach (StrategicBuiltFacilityViewModel facility in city.BuiltFacilities)
            {
                AddMutedLine(list, $"{facility.DisplayName}    槽位 {facility.SlotCost}");
            }

            return;
        }

        AddMutedLine(list, "非城市地点没有城市设施。");
        if (location?.ProductionPerWorldTimePulse?.Count > 0)
        {
            AddMutedLine(list, $"大地图时间产出：{location.ProductionDisplayText}");
        }

        if (location?.SourcePermissionTags?.Count > 0)
        {
            AddMutedLine(list, $"来源权限：{location.SourcePermissionDisplayText}");
        }
    }

    private void AddStrategicCorpsLines(
        VBoxContainer list,
        StrategicLocationDashboardViewModel location,
        StrategicCityManagementViewModel city)
    {
        if (list == null)
        {
            return;
        }

        if (location?.CanManageCity != true)
        {
            AddMutedLine(list, "该地点不管理城市编制。");
            return;
        }

        if (city?.CorpsInstances == null || city.CorpsInstances.Count == 0)
        {
            AddMutedLine(list, "当前城市还没有编制实例。");
            return;
        }

        foreach (StrategicCorpsInstanceViewModel corps in city.CorpsInstances)
        {
            string assignment = string.IsNullOrWhiteSpace(corps.AssignedHeroId)
                ? ""
                : $"    英雄 {corps.AssignedHeroId}";
            AddMutedLine(
                list,
                $"{corps.DisplayName}    强度 {corps.Strength}/100    等级 {corps.Level}    装备 {corps.EquipmentLevel}    状态 {FormatStrategicCorpsStatus(corps.Status)}{assignment}");
        }
    }

    private static string FormatStrategicText(string text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string FormatStrategicFactionId(string factionId)
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

    private static string FormatStrategicCorpsStatus(StrategicCorpsInstanceStatus status)
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

    private bool TryRefreshOpportunityDetail(StrategicWorldDefinitionQueries queries)
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            return false;
        }

        if (_opportunityDetailPanel == null)
        {
            _selectedOpportunityId = "";
            GameLog.Warn(nameof(StrategicWorldRoot), "Missing WorldOpportunityDetailPanel scene instance.");
            return false;
        }

        WorldOpportunityDefinition definition = queries.GetOpportunity(opportunity.DefinitionId);
        OpportunitySpawnPointDefinition spawnPoint = queries.GetOpportunitySpawnPoint(opportunity.SpawnPointId);
        int remainingTicks = System.Math.Max(0, opportunity.ExpiresTick - State.WorldTick);
        SetSiteDetailSectionsVisible(false);
        _opportunityDetailPanel.Visible = true;
        _opportunityDetailPanel.Bind(new WorldOpportunityDetailPanelData
        {
            Title = $"野外小场域 · {definition?.DisplayName ?? opportunity.DefinitionId}",
            Description = definition?.Description ?? "临时出现的野外机会。",
            StatusText = GetOpportunityStatusLabel(opportunity.Status),
            SpawnPointText = spawnPoint?.DisplayName ?? opportunity.SpawnPointId,
            RemainingText = $"{remainingTicks} 次大地图结算",
            RewardText = BuildOpportunityRewardText(queries, definition)
        });
        return true;
    }

    private void SetSiteDetailSectionsVisible(bool visible)
    {
        if (_siteSummaryCard != null)
        {
            _siteSummaryCard.Visible = visible;
        }

        if (_facilityCard != null)
        {
            _facilityCard.Visible = visible;
        }

        if (_defenseCard != null)
        {
            _defenseCard.Visible = visible;
        }

        if (_actionCard != null)
        {
            _actionCard.Visible = visible;
        }

        if (_opportunityCard != null)
        {
            _opportunityCard.Visible = !visible;
        }

        if (_opportunityDetailPanel != null)
        {
            _opportunityDetailPanel.Visible = !visible;
        }
    }

    private void HideWorldDetailSections()
    {
        SetSiteDetailSectionsVisible(false);
        if (_opportunityDetailPanel != null)
        {
            _opportunityDetailPanel.Visible = false;
        }
    }

    private string BuildArmyArrivalText(WorldArmyState army)
    {
        float remainingDistance = army.WorldPosition.DistanceTo(army.Destination);
        double movementSpeed = System.Math.Max(1.0, army.MoveSpeed * WorldClockSpeedMultipliers[_worldClockSpeedIndex]);
        double etaSeconds = System.Math.Ceiling(remainingDistance / movementSpeed);
        return $"敌军行军中，预计 {etaSeconds:0}s 抵达";
    }

    private void RefreshActions()
    {
        ClearChildren(_actionList);
        if (string.IsNullOrWhiteSpace(_selectedSiteId) &&
            !_isExpeditionDrafting &&
            !TryGetSelectedActiveOpportunity(out _))
        {
            return;
        }

        if (RefreshExpeditionControls())
        {
            return;
        }

        if (TryGetSelectedActiveOpportunity(out _))
        {
            return;
        }

        IReadOnlyList<WorldActionViewModel> actions = string.IsNullOrWhiteSpace(_selectedSiteId)
            ? System.Array.Empty<WorldActionViewModel>()
            : _actionResolver.GetAvailableActions(State, Definition, _selectedSiteId);
        foreach (WorldActionViewModel action in actions)
        {
            if (!ShouldShowStrategicAction(action))
            {
                continue;
            }

            Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = BuildActionButtonText(action);
            button.Disabled = !action.IsEnabled;

            if (action.IsEnabled)
            {
                button.Pressed += () => ExecuteAction(action);
            }

            _actionList.AddChild(button);
        }

        if (_actionList.GetChildCount() == 0)
        {
            AddMutedLine(_actionList, "暂无可执行行动");
        }
    }

    private bool ShouldShowStrategicAction(WorldActionViewModel action)
    {
        if (action == null)
        {
            return false;
        }

        WorldActionDefinition definition = new StrategicWorldDefinitionQueries(Definition).GetAction(action.ActionId);
        return definition == null ||
               definition.Scope is not WorldActionScope.Site and not WorldActionScope.Facility;
    }

    private void CompleteSelectedOpportunity()
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            StrategicWorldRuntime.LastNotice = "野外小场域已经消失。";
            _selectedOpportunityId = "";
            RefreshAll();
            return;
        }

        WorldActionResult result = _opportunityService.CompleteOpportunity(State, Definition, opportunity.OpportunityId);
        StrategicWorldRuntime.LastNotice = result.Message;
        if (result.Success)
        {
            _selectedOpportunityId = "";
        }

        RefreshAll();
    }
}
