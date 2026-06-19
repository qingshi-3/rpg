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

        SetWorldDetailPanelVisible(true);
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
        _siteBodyLabel.Text = BuildStrategicLocationContextSummary(definition, location, city);

    }

    private void BindUnmappedStrategicLocationDetail(WorldSiteDefinition definition)
    {
        string title = definition?.DisplayName ?? _selectedSiteId;
        _siteTitleLabel.Text = $"{title}  ·  战略地点";
        _siteBodyLabel.Text = string.Join(
            "\n",
            string.IsNullOrWhiteSpace(definition?.Description) ? "该地点暂未接入战略经营。" : definition.Description,
            "当前仅保留地图上下文。");
    }

    private static string BuildStrategicLocationContextSummary(
        WorldSiteDefinition definition,
        StrategicLocationDashboardViewModel location,
        StrategicCityManagementViewModel city)
    {
        List<string> detailLines = new();
        if (!string.IsNullOrWhiteSpace(definition?.Description))
        {
            detailLines.Add(definition.Description);
        }

        detailLines.Add(BuildStrategicLocationStatusLine(location));
        detailLines.Add(location?.CanManageCity == true
            ? BuildCityCompactOperationSummary(city)
            : "当前没有城市经营入口。");

        return string.Join("\n", detailLines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string BuildStrategicLocationStatusLine(StrategicLocationDashboardViewModel location)
    {
        string control = FormatStrategicText(location?.ControlStateDisplayName, "未知");
        string owner = FormatStrategicFactionId(location?.OwnerFactionId);
        string production = FormatStrategicText(location?.ProductionDisplayText, "无");
        string permission = FormatStrategicText(location?.SourcePermissionDisplayText, "无");
        return $"控制 {control}  ·  {owner}\n产出 {production}  ·  权限 {permission}";
    }

    private static string BuildCityCompactOperationSummary(StrategicCityManagementViewModel city)
    {
        int builtFacilities = city?.BuiltFacilities?.Count ?? 0;
        int facilitySlotsUsed = city?.FacilitySlotsUsed ?? 0;
        int facilitySlotsTotal = city?.FacilitySlotsTotal ?? 0;
        int availableCompanies = city?.HeroCompanies?.Count(company => company.CanCreateExpedition) ?? 0;
        int totalCompanies = city?.HeroCompanies?.Count ?? 0;
        return $"建设 {builtFacilities} 项  ·  槽位 {facilitySlotsUsed}/{facilitySlotsTotal}\n出征队伍 {availableCompanies}/{totalCompanies}";
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
        SetWorldDetailPanelVisible(true);
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

    private void SetWorldDetailPanelVisible(bool visible)
    {
        if (_siteDetailPanel == null || _siteDetailPanelVisibleRequested == visible)
        {
            return;
        }

        _siteDetailPanelVisibleRequested = visible;
        if (visible)
        {
            AnimateWorldDetailPanelIn();
            return;
        }

        AnimateWorldDetailPanelOut();
    }

    private void AnimateWorldDetailPanelIn()
    {
        if (_siteDetailPanel == null)
        {
            return;
        }

        Vector2 restPosition = ResolveWorldDetailPanelRestPosition();
        KillWorldDetailPanelTween();
        ConfigureWorldDetailPanelPivot();
        _siteDetailPanel.Visible = true;
        _siteDetailPanel.Position = restPosition + new Vector2(0.0f, SiteDetailPanelSlidePixels);
        _siteDetailPanel.Scale = new Vector2(0.98f, 0.94f);
        _siteDetailPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        _siteDetailPanelTween = CreateTween().BindNode(this);
        _siteDetailPanelTween.TweenProperty(_siteDetailPanel, "position", restPosition - new Vector2(0.0f, SiteDetailPanelOvershootPixels), SiteDetailPanelEnterSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _siteDetailPanelTween.Parallel().TweenProperty(_siteDetailPanel, "scale", new Vector2(1.02f, 1.03f), SiteDetailPanelEnterSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _siteDetailPanelTween.Parallel().TweenProperty(_siteDetailPanel, "modulate", Colors.White, SiteDetailPanelEnterSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _siteDetailPanelTween.Chain().TweenProperty(_siteDetailPanel, "position", restPosition, SiteDetailPanelSettleSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _siteDetailPanelTween.Parallel().TweenProperty(_siteDetailPanel, "scale", Vector2.One, SiteDetailPanelSettleSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _siteDetailPanelTween.TweenCallback(Callable.From(() => CompleteWorldDetailPanelAnimation(restPosition, true)));
    }

    private void AnimateWorldDetailPanelOut()
    {
        if (_siteDetailPanel == null)
        {
            return;
        }

        Vector2 restPosition = ResolveWorldDetailPanelRestPosition();
        KillWorldDetailPanelTween();
        ConfigureWorldDetailPanelPivot();
        _siteDetailPanel.Visible = true;

        _siteDetailPanelTween = CreateTween().BindNode(this);
        _siteDetailPanelTween.TweenProperty(_siteDetailPanel, "position", restPosition + new Vector2(0.0f, SiteDetailPanelSlidePixels), SiteDetailPanelExitSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _siteDetailPanelTween.Parallel().TweenProperty(_siteDetailPanel, "scale", new Vector2(0.98f, 0.94f), SiteDetailPanelExitSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _siteDetailPanelTween.Parallel().TweenProperty(_siteDetailPanel, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), SiteDetailPanelExitSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _siteDetailPanelTween.TweenCallback(Callable.From(() => CompleteWorldDetailPanelAnimation(restPosition, false)));
    }

    private Vector2 ResolveWorldDetailPanelRestPosition()
    {
        if (_siteDetailPanelTween != null && _siteDetailPanelHasRestPosition)
        {
            return _siteDetailPanelRestPosition;
        }

        _siteDetailPanelRestPosition = _siteDetailPanel?.Position ?? Vector2.Zero;
        _siteDetailPanelHasRestPosition = true;
        return _siteDetailPanelRestPosition;
    }

    private void ConfigureWorldDetailPanelPivot()
    {
        if (_siteDetailPanel == null)
        {
            return;
        }

        _siteDetailPanel.PivotOffset = new Vector2(_siteDetailPanel.Size.X * 0.5f, _siteDetailPanel.Size.Y);
    }

    private void KillWorldDetailPanelTween()
    {
        _siteDetailPanelTween?.Kill();
        _siteDetailPanelTween = null;
    }

    private void CompleteWorldDetailPanelAnimation(Vector2 restPosition, bool visible)
    {
        if (_siteDetailPanel == null)
        {
            return;
        }

        _siteDetailPanel.Position = restPosition;
        _siteDetailPanel.Scale = Vector2.One;
        _siteDetailPanel.Modulate = visible ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.0f);
        if (visible)
        {
            _siteDetailPanel.Show();
        }
        else
        {
            _siteDetailPanel.Hide();
        }
        _siteDetailPanelTween = null;

        if (!visible)
        {
            SetSiteDetailSectionsVisible(false);
            if (_opportunityDetailPanel != null)
            {
                _opportunityDetailPanel.Visible = false;
            }
        }
    }

    private void HideWorldDetailSections()
    {
        SetWorldDetailPanelVisible(false);
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

            button.Text = BuildActionButtonLabel(action);
            button.TooltipText = BuildActionTooltip(action);
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
