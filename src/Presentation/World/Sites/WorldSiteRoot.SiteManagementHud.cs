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
    private void BuildSiteHud()
    {
        if (_siteHudRoot != null)
        {
            return;
        }

        Node canvasLayer = GetNodeOrNull<Node>("CanvasLayer") ?? this;
        _siteHudRoot = GameUiSceneFactory.Instantiate<Control>(
            GameUiSceneFactory.WorldSitePeacetimeHudScenePath,
            nameof(WorldSiteRoot));
        if (_siteHudRoot == null)
        {
            return;
        }

        _siteHudRoot.Visible = false;
        _siteHudRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvasLayer.AddChild(_siteHudRoot);
        ApplySiteHudFullRect("build");
        EnsureSitePlacementEntityRoot();

        // Layout hosts are Presentation-only containers. Site data, battle requests,
        // and settlement authority stay in Application/Runtime paths.
        _siteHudTopBar = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "TopBarHost/SiteTopBar",
            nameof(WorldSiteRoot));
        _sitePeacetimePanel = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel",
            nameof(WorldSiteRoot));
        ApplySiteHudFullRect("bound");
        _siteHudTitle = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteHudTitle",
            nameof(WorldSiteRoot));
        _siteResourceLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteResourceLabel",
            nameof(WorldSiteRoot));
        _returnMapButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "TopBarHost/SiteTopBar/TopMargin/TopBox/ReturnMapButton",
            nameof(WorldSiteRoot));
        _siteHudBody = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteHudBody",
            nameof(WorldSiteRoot));
        _siteSelectionLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteSelectionLabel",
            nameof(WorldSiteRoot));
        _siteFacilityBuildCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard",
            nameof(WorldSiteRoot));
        _siteFacilityCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard",
            nameof(WorldSiteRoot));
        _siteDefenseCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard",
            nameof(WorldSiteRoot));
        _siteActionCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard",
            nameof(WorldSiteRoot));
        _siteBattlePreparationContent = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent",
            nameof(WorldSiteRoot));
        _siteBattlePreparationRosterList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent/BattlePreparationMargin/BattlePreparationStack/BattlePreparationRosterList",
            nameof(WorldSiteRoot));
        _siteBattlePreparationEnemySummary = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent/BattlePreparationMargin/BattlePreparationStack/BattlePreparationEnemySummary",
            nameof(WorldSiteRoot));
        _siteBattlePreparationStatus = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent/BattlePreparationMargin/BattlePreparationStack/BattlePreparationStatus",
            nameof(WorldSiteRoot));
        _siteBattlePreparationActionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent/BattlePreparationMargin/BattlePreparationStack/BattlePreparationActionList",
            nameof(WorldSiteRoot));
        _siteFacilityBuildTitle = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/BuildTitle",
            nameof(WorldSiteRoot));
        _siteFacilityBuildList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/SiteFacilityBuildList",
            nameof(WorldSiteRoot));
        _siteFacilityList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/SiteFacilityList",
            nameof(WorldSiteRoot));
        _siteGarrisonList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/SiteGarrisonList",
            nameof(WorldSiteRoot));
        _siteThreatList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/SiteThreatList",
            nameof(WorldSiteRoot));
        _siteActionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteActionList",
            nameof(WorldSiteRoot));
        _siteNoticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteNoticeLabel",
            nameof(WorldSiteRoot));
        Label operationHintLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteOperationHintLabel",
            nameof(WorldSiteRoot));
        Label facilityTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/FacilityTitle",
            nameof(WorldSiteRoot));
        Label garrisonTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/GarrisonTitle",
            nameof(WorldSiteRoot));
        Label threatTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/ThreatTitle",
            nameof(WorldSiteRoot));
        Label actionTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/ActionTitle",
            nameof(WorldSiteRoot));
        Label noticeTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/NoticeTitle",
            nameof(WorldSiteRoot));

        if (operationHintLabel != null)
        {
            operationHintLabel.Text = "场域经营：点击建筑点管理；有探索内容时，点击地图格设置移动意图。";
        }

        if (_returnMapButton != null)
        {
            _returnMapButton.Text = "返回大地图";
        }

        if (facilityTitleLabel != null)
        {
            facilityTitleLabel.Text = "建筑总览";
        }

        if (garrisonTitleLabel != null)
        {
            garrisonTitleLabel.Text = "驻防兵力";
        }

        if (threatTitleLabel != null)
        {
            threatTitleLabel.Text = "敌情追踪";
        }

        if (actionTitleLabel != null)
        {
            actionTitleLabel.Text = "可执行行动";
        }

        if (noticeTitleLabel != null)
        {
            noticeTitleLabel.Text = "最近反馈";
        }

        if (_returnMapButton != null)
        {
            _returnMapButton.Pressed += () => ReturnToReturnScene(_siteHudReturnScenePath);
        }
    }

    private void EnsureSitePlacementEntityRoot()
    {
        if (_sitePlacementEntityRoot != null)
        {
            return;
        }

        _sitePlacementEntityRoot = new Node2D
        {
            Name = "SitePlacementEntityRoot",
            Visible = false,
            YSortEnabled = true
        };
        (_unitRoot ?? (Node)this).AddChild(_sitePlacementEntityRoot);
    }

    private void SwitchToNonBattleUi(
        BattleOutcome outcome,
        BattleStartRequest request,
        WorldActionResult applyResult,
        string returnScenePath)
    {
        SetBattleRuntimeEnabled(false);
        StrategicWorldRuntime.EnsureInitialized();

        string siteId = ResolveRequestSiteId(request);
        string pendingVisitArmyId = "";
        if (request == null &&
            StrategicWorldRuntime.TryConsumePendingSiteVisit(out string pendingSiteId, out string pendingReturnScenePath, out string pendingArmyId))
        {
            siteId = pendingSiteId;
            pendingVisitArmyId = pendingArmyId;
            if (string.IsNullOrWhiteSpace(returnScenePath))
            {
                returnScenePath = pendingReturnScenePath;
            }
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"PendingSiteVisitConsumed site={siteId} army={pendingVisitArmyId} returnScene={returnScenePath}");
        }

        _siteHudSiteId = siteId;
        _siteHudReturnScenePath = string.IsNullOrWhiteSpace(returnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : returnScenePath;
        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        if (!string.IsNullOrWhiteSpace(pendingVisitArmyId) &&
            StrategicWorldRuntime.State?.SiteStates.TryGetValue(siteId, out WorldSiteState pendingVisitSite) == true)
        {
            WorldSiteDefinition pendingVisitDefinition = ResolveSiteDefinition(siteId);
            EnsureVisitingArmyPlacement(pendingVisitSite, pendingVisitDefinition, pendingVisitArmyId);
            EnterSiteAlertModeForVisit(pendingVisitSite, pendingVisitArmyId);
            LogSiteUnitState("SiteVisitInitialized", pendingVisitSite, pendingVisitArmyId);
        }

        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = string.IsNullOrWhiteSpace(_siteHudReturnScenePath);
            _returnMapButton.TooltipText = _returnMapButton.Disabled ? "没有可返回的大地图场景。" : "";
        }

        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("show");
        }

        if (outcome == BattleOutcome.None)
        {
            BindSiteManagementPanel(applyResult?.Message, outcome);
        }
        else
        {
            BindSettlementReportPanel(outcome, applyResult);
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SwitchedToSiteManagementUi site={siteId} hudVisible={_siteHudRoot?.Visible == true} topBarVisible={_siteHudTopBar?.Visible == true} panelVisible={_sitePeacetimePanel?.Visible == true} hudRect={DescribeControlRect(_siteHudRoot)} panelRect={DescribeControlRect(_sitePeacetimePanel)} viewport={GetViewportRect().Size} returnScene={_siteHudReturnScenePath}");
    }

    private void OnViewportSizeChanged()
    {
        if (_siteHudRoot?.Visible == true)
        {
            ApplySiteHudFullRect("viewport_resized");
        }

        UpdateMainWorldViewportLayout("viewport_resized");
    }

    private void ApplySiteHudFullRect(string reason)
    {
        if (_siteHudRoot == null)
        {
            return;
        }

        _siteHudRoot.AnchorLeft = 0.0f;
        _siteHudRoot.AnchorTop = 0.0f;
        _siteHudRoot.AnchorRight = 1.0f;
        _siteHudRoot.AnchorBottom = 1.0f;
        _siteHudRoot.OffsetLeft = 0.0f;
        _siteHudRoot.OffsetTop = 0.0f;
        _siteHudRoot.OffsetRight = 0.0f;
        _siteHudRoot.OffsetBottom = 0.0f;
        _siteHudRoot.Position = Vector2.Zero;
        _siteHudRoot.Size = GetViewportRect().Size;
        ApplySitePeacetimePanelLayout();
        UpdateMainWorldViewportLayout(reason);

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteHudFullRectApplied reason={reason} hudRect={DescribeControlRect(_siteHudRoot)} panelRect={DescribeControlRect(_sitePeacetimePanel)} viewport={GetViewportRect().Size} parent={_siteHudRoot.GetParent()?.GetPath()}");
    }

    private void ApplySitePeacetimePanelLayout()
    {
        if (_sitePeacetimePanel == null)
        {
            return;
        }

        // During the UI layout migration this panel is the left primary workspace.
        // Mode-specific content is bound inside it; the panel itself is not data authority.
        _sitePeacetimePanel.AnchorLeft = 0.0f;
        _sitePeacetimePanel.AnchorTop = 0.0f;
        _sitePeacetimePanel.AnchorRight = 0.0f;
        _sitePeacetimePanel.AnchorBottom = 1.0f;
        _sitePeacetimePanel.OffsetLeft = 24.0f;
        _sitePeacetimePanel.OffsetTop = 82.0f;
        _sitePeacetimePanel.OffsetRight = 544.0f;
        _sitePeacetimePanel.OffsetBottom = -24.0f;
        _sitePeacetimePanel.CustomMinimumSize = new Vector2(520.0f, 0.0f);
    }

    private void UpdateSitePeacetimePanelVisibility(string reason)
    {
        if (_sitePeacetimePanel == null)
        {
            return;
        }

        bool shouldShow = _siteHudRoot?.Visible == true &&
                          (_isBattlePreparationActive || !string.IsNullOrWhiteSpace(_selectedFacilitySlotId));
        if (shouldShow)
        {
            ApplySitePeacetimePanelLayout();
        }

        if (_sitePeacetimePanel.Visible == shouldShow)
        {
            return;
        }

        _sitePeacetimePanel.Visible = shouldShow;
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SitePeacetimePanelVisibilityChanged visible={shouldShow} reason={reason} selectedSlot={_selectedFacilitySlotId} panelRect={DescribeControlRect(_sitePeacetimePanel)}");
    }

    private static string DescribeControlRect(Control control)
    {
        if (control == null)
        {
            return "<null>";
        }

        Rect2 rect = control.GetGlobalRect();
        return $"pos={rect.Position} size={rect.Size} anchors=({control.AnchorLeft:0.##},{control.AnchorTop:0.##},{control.AnchorRight:0.##},{control.AnchorBottom:0.##}) offsets=({control.OffsetLeft:0.#},{control.OffsetTop:0.#},{control.OffsetRight:0.#},{control.OffsetBottom:0.#})";
    }

    private void SetBattleRuntimeEnabled(bool enabled, bool keepBattlePresentation = false)
    {
        _battleRuntimeEnabled = enabled;
        if (enabled)
        {
            _draggedPlacementId = "";
            ClearSiteDeploymentDragPreview(null);
        }

        if (enabled && _siteHudRoot != null)
        {
            _siteHudRoot.Visible = false;
        }

        UpdateMainWorldViewportLayout(enabled ? "battle_runtime_enabled" : "battle_runtime_disabled");

        if (!enabled)
        {
            _unitRoot?.PlayIdleForActiveEntities();
        }

        if (_unitRoot != null)
        {
            _unitRoot.Visible = enabled || !string.IsNullOrWhiteSpace(_siteHudSiteId) || keepBattlePresentation;
        }

        if (_sitePlacementEntityRoot != null)
        {
            _sitePlacementEntityRoot.Visible = !enabled || keepBattlePresentation;
        }

        SetFacilitySlotsVisible(true);
    }

    private void SetFacilitySlotsVisible(bool visible)
    {
        if (_activeSiteMap?.GetNodeOrNull<CanvasItem>(FacilitySlotsRootName) is { } slotsRoot)
        {
            slotsRoot.Visible = visible;
        }
    }

    private static string ResolveRequestSiteId(BattleStartRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request?.TargetSiteId))
        {
            return request.TargetSiteId;
        }

        if (!string.IsNullOrWhiteSpace(request?.SourceSiteId))
        {
            return request.SourceSiteId;
        }

        StrategicWorldRuntime.EnsureInitialized();
        return string.IsNullOrWhiteSpace(StrategicWorldRuntime.Definition?.StartingSiteId)
            ? StrategicWorldIds.SitePlayerCamp
            : StrategicWorldRuntime.Definition.StartingSiteId;
    }

    private WorldSiteState ResolveSiteState(string siteId)
    {
        StrategicWorldRuntime.EnsureInitialized();
        return !string.IsNullOrWhiteSpace(siteId) &&
               StrategicWorldRuntime.State.SiteStates.TryGetValue(siteId, out WorldSiteState site)
            ? site
            : null;
    }

    private WorldSiteDefinition ResolveSiteDefinition(string siteId)
    {
        StrategicWorldRuntime.EnsureInitialized();
        return new StrategicWorldDefinitionQueries(StrategicWorldRuntime.Definition).GetSite(siteId);
    }

    private string ResolveSiteName(string siteId)
    {
        WorldSiteDefinition definition = ResolveSiteDefinition(siteId);
        return string.IsNullOrWhiteSpace(definition?.DisplayName) ? siteId : definition.DisplayName;
    }

    private static bool CanOpenSiteDetail(WorldSiteState site)
    {
        return site != null &&
               site.OwnerFactionId == StrategicWorldRuntime.State.PlayerFactionId &&
               site.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged;
    }

    private void RefreshSiteManagementUi(string notice = "", BattleOutcome outcome = BattleOutcome.None)
    {
        BindSiteManagementPanel(notice, outcome);
    }

    private void BindSiteManagementPanel(string notice = "", BattleOutcome outcome = BattleOutcome.None)
    {
        StrategicWorldRuntime.EnsureInitialized();
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        _deploymentService.EnsureGarrisonPlacements(site, definition);
        EnsureSitePlacementsRespectTerrain(site, definition);

        _siteHudTitle.Text = outcome == BattleOutcome.None
            ? $"{ResolveSiteName(_siteHudSiteId)} · 场域经营"
            : $"{ResolveSiteName(_siteHudSiteId)} · {GetBattleOutcomeLabel(outcome)}";
        _siteResourceLabel.Text = BuildResourceLine();
        _siteHudBody.Text = BuildSiteOverview(_siteHudSiteId);
        _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice) ? StrategicWorldRuntime.LastNotice : notice.Trim();
        SetBattlePreparationContentVisible(false);

        RefreshSiteMapEntities(site, definition);
        RefreshFacilityList(site, definition);
        RefreshFacilityBuildList(site, definition);
        RefreshGarrisonList(site);
        RefreshThreatList(site);
        RefreshActionList(site);
        UpdateSitePeacetimePanelVisibility("refresh");
    }

    private void BindSettlementReportPanel(BattleOutcome outcome, WorldActionResult applyResult)
    {
        // V0 settlement report still reuses the site-management panel layout. Keeping
        // this binder boundary prevents settlement presentation from growing inside
        // unrelated management refresh call sites.
        BindSiteManagementPanel(applyResult?.Message, outcome);
    }

    private void SetBattlePreparationContentVisible(bool visible)
    {
        if (_siteBattlePreparationContent != null)
        {
            _siteBattlePreparationContent.Visible = visible;
        }

        if (_siteFacilityBuildCard != null)
        {
            _siteFacilityBuildCard.Visible = !visible && _siteFacilityBuildCard.Visible;
        }

        if (_siteFacilityCard != null)
        {
            _siteFacilityCard.Visible = !visible;
        }

        if (_siteDefenseCard != null)
        {
            _siteDefenseCard.Visible = !visible;
        }

        if (_siteActionCard != null)
        {
            _siteActionCard.Visible = !visible;
        }
    }

    private void SetSiteNoticeText(string notice)
    {
        if (_siteNoticeLabel != null)
        {
            _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? StrategicWorldRuntime.LastNotice
                : notice.Trim();
        }
    }

    private string BuildResourceLine()
    {
        ResourceStore resources = StrategicWorldRuntime.State.PlayerResources;
        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        return
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation)} {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}    " +
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy)} {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}    " +
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} {resources.GetAmount(StrategicWorldIds.ResourceStone)}    " +
            $"世界步 {StrategicWorldRuntime.State.WorldTick}";
    }

    private string BuildSiteOverview(string siteId)
    {
        WorldSiteState site = ResolveSiteState(siteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(siteId);
        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        if (site == null)
        {
            return "当前场域状态缺失。";
        }

        WorldSiteIntelViewModel intelView = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            siteId,
            WorldIntelVisibility.Visible);
        int facilityCount = site.Facilities.Count(facility => facility.State != FacilityState.Destroyed);
        string garrisonOverviewText = BuildSiteGarrisonOverviewText(site, intelView);
        int activeThreatCount = site.PendingThreatIds
            .Select(id => StrategicWorldRuntime.State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .Count(threat => threat is { Stage: not ThreatStage.Resolved });
        List<string> overviewLines = new()
        {
            definition?.Description ?? ResolveSiteName(siteId)
        };
        overviewLines.AddRange(WorldSiteIntelPresenter.BuildSummaryLines(intelView));
        overviewLines.AddRange(new[]
        {
            $"控制：{GetControlStateLabel(site.ControlState)}    模式：{GetSiteModeLabel(site.SiteMode)}",
            $"归属：{StrategicWorldDisplayNames.GetFactionLabel(queries, site.OwnerFactionId)}    受损：{site.DamageLevel}",
            $"建筑：{facilityCount}    驻军：{garrisonOverviewText}    威胁：{activeThreatCount}"
        });

        return string.Join("\n", overviewLines);
    }

    private void RefreshFacilityList(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearChildren(_siteFacilityList);
        if (site == null || definition == null || definition.FacilitySlots.Count == 0)
        {
            AddMutedLine(_siteFacilityList, "无可经营建筑点");
            _siteSelectionLabel.Text = "";
            return;
        }

        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        IEnumerable<FacilitySlotDefinition> visibleSlots = definition.FacilitySlots
            .OrderByDescending(slot => slot.SlotId == _selectedFacilitySlotId)
            .ThenBy(slot => slot.DisplayName);
        foreach (FacilitySlotDefinition slot in visibleSlots)
        {
            FacilityInstance facility = site.Facilities.FirstOrDefault(item => item.SlotId == slot.SlotId && item.State != FacilityState.Destroyed);
            string facilityText = facility == null
                ? $"空置，可建：{BuildAllowedFacilityNames(slot, queries)}"
                : $"{queries.GetFacility(facility.FacilityId)?.DisplayName ?? facility.FacilityId} · {GetFacilityStateLabel(facility.State)}";
            string slotTitle = slot.SlotId == _selectedFacilitySlotId
                ? $"已选 · {slot.DisplayName}"
                : slot.DisplayName;
            AddMutedLine(_siteFacilityList, $"{slotTitle}\n{facilityText}");
        }

        RefreshSelectedSlotLabel(site);
    }

    private void RefreshFacilityBuildList(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearChildren(_siteFacilityBuildList);

        if (_siteFacilityBuildTitle == null || _siteFacilityBuildList == null)
        {
            return;
        }

        if (site == null || definition == null || definition.FacilitySlots.Count == 0)
        {
            if (_siteFacilityBuildCard != null)
            {
                _siteFacilityBuildCard.Visible = false;
            }
            _siteFacilityBuildTitle.Visible = false;
            _siteFacilityBuildList.Visible = false;
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteFacilityBuildPanelRefreshed site={site?.SiteId ?? _siteHudSiteId} visible=false reason=no_site_or_slots hasSite={site != null} hasDefinition={definition != null} definedSlots={definition?.FacilitySlots.Count ?? 0}");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedFacilitySlotId))
        {
            if (_siteFacilityBuildCard != null)
            {
                _siteFacilityBuildCard.Visible = false;
            }
            _siteFacilityBuildTitle.Visible = false;
            _siteFacilityBuildList.Visible = false;
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteFacilityBuildPanelRefreshed site={site.SiteId} visible=false reason=no_selected_slot definedSlots={definition.FacilitySlots.Count} registeredSlots={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count}");
            return;
        }

        FacilitySlotDefinition selectedSlot = definition.FacilitySlots.FirstOrDefault(item => item.SlotId == _selectedFacilitySlotId);
        if (selectedSlot == null)
        {
            if (_siteFacilityBuildCard != null)
            {
                _siteFacilityBuildCard.Visible = false;
            }
            _siteFacilityBuildTitle.Visible = false;
            _siteFacilityBuildList.Visible = false;
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"SiteFacilityBuildPanelRefreshed site={site.SiteId} visible=false reason=selected_slot_missing selectedSlot={_selectedFacilitySlotId} definedSlots={definition.FacilitySlots.Count}");
            return;
        }

        if (_siteFacilityBuildCard != null)
        {
            _siteFacilityBuildCard.Visible = true;
        }
        _siteFacilityBuildTitle.Visible = true;
        _siteFacilityBuildList.Visible = true;

        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        FacilityInstance existingFacility = ResolveFacilityInSlot(site, selectedSlot.SlotId);
        if (existingFacility != null)
        {
            string facilityName = queries.GetFacility(existingFacility.FacilityId)?.DisplayName ?? existingFacility.FacilityId;
            _siteFacilityBuildTitle.Text = $"建筑信息 · {selectedSlot.DisplayName}";
            AddMutedLine(_siteFacilityBuildList, $"{facilityName}\n状态：{GetFacilityStateLabel(existingFacility.State)}");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteFacilityBuildPanelRefreshed site={site.SiteId} visible=true reason=occupied selectedSlot={_selectedFacilitySlotId} facility={existingFacility.FacilityId} definedSlots={definition.FacilitySlots.Count} registeredSlots={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count} buttons=0");
            return;
        }

        _siteFacilityBuildTitle.Text = $"可建建筑 · {selectedSlot.DisplayName}";
        IReadOnlyList<WorldActionViewModel> buildActions = ResolveBuildActionsForSlot(site, selectedSlot);
        if (buildActions.Count == 0)
        {
            AddMutedLine(_siteFacilityBuildList, "暂无可建建筑。");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteFacilityBuildPanelRefreshed site={site.SiteId} visible=true reason=no_build_actions selectedSlot={_selectedFacilitySlotId} definedSlots={definition.FacilitySlots.Count} registeredSlots={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count} buttons=0");
            return;
        }

        int buildButtonCount = 0;
        int enabledBuildButtonCount = 0;
        foreach (WorldActionViewModel action in buildActions)
        {
            Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = BuildFacilityBuildButtonText(action);
            button.Disabled = !action.IsEnabled;
            button.TooltipText = BuildActionTooltip(action);
            if (action.IsEnabled)
            {
                enabledBuildButtonCount++;
                string targetSlotId = selectedSlot.SlotId;
                button.Pressed += () => ExecuteSiteAction(action, targetSlotId);
            }

            _siteFacilityBuildList.AddChild(button);
            buildButtonCount++;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteFacilityBuildPanelRefreshed site={site.SiteId} visible=true selectedSlot={_selectedFacilitySlotId} buildSlots=1 buttons={buildButtonCount} enabled={enabledBuildButtonCount} definedSlots={definition.FacilitySlots.Count} registeredSlots={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count}");
    }

    private void RefreshGarrisonList(WorldSiteState site)
    {
        ClearChildren(_siteGarrisonList);
        WorldSiteIntelViewModel intelView = site == null
            ? null
            : WorldSiteIntelService.BuildCurrentView(
                StrategicWorldRuntime.State,
                StrategicWorldRuntime.Definition,
                site.SiteId,
                WorldIntelVisibility.Visible);
        AddSiteGarrisonLines(_siteGarrisonList, site, intelView);
    }

    private string BuildSiteGarrisonOverviewText(WorldSiteState site, WorldSiteIntelViewModel intelView)
    {
        if (!CanRevealSiteGarrison(site, intelView))
        {
            return "驻军情报不足，需探索确认。";
        }

        return site?.Garrison?.Sum(garrison => garrison.Count).ToString() ?? "0";
    }

    private void AddSiteGarrisonLines(VBoxContainer list, WorldSiteState site, WorldSiteIntelViewModel intelView)
    {
        if (list == null)
        {
            return;
        }

        if (site == null)
        {
            AddMutedLine(list, "无");
            return;
        }

        if (!CanRevealSiteGarrison(site, intelView))
        {
            AddMutedLine(list, "驻军情报不足，需探索确认。");
            return;
        }

        if (site.Garrison.Count == 0)
        {
            AddMutedLine(list, "无");
            return;
        }

        WorldSiteDefinition definition = ResolveSiteDefinition(site.SiteId);
        AddMutedLine(list, $"驻军区：{_deploymentService.BuildGarrisonSummary(site, definition)}");
        foreach (GarrisonState garrison in site.Garrison)
        {
            AddMutedLine(list, $"{GetUnitLabel(garrison.UnitTypeId)} x{garrison.Count}    士气 {garrison.Morale}");
        }
    }

    private bool CanRevealSiteGarrison(WorldSiteState site, WorldSiteIntelViewModel intelView)
    {
        return site != null &&
               (site.OwnerFactionId == StrategicWorldRuntime.State?.PlayerFactionId ||
                intelView?.CanInspectFullTacticalLayout == true);
    }

    private void RefreshThreatList(WorldSiteState site)
    {
        ClearChildren(_siteThreatList);
        if (site == null)
        {
            AddMutedLine(_siteThreatList, "暂无");
            return;
        }

        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        EnemyThreatPlan[] threats = site.PendingThreatIds
            .Select(id => StrategicWorldRuntime.State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .Where(threat => threat is { Stage: not ThreatStage.Resolved })
            .ToArray();

        if (threats.Length == 0)
        {
            AddMutedLine(_siteThreatList, "暂无");
            return;
        }

        foreach (EnemyThreatPlan threat in threats)
        {
            string source = queries.GetSite(threat.SourceSiteId)?.DisplayName ?? threat.SourceSiteId;
            AddMutedLine(_siteThreatList, $"{GetThreatStageLabel(threat.Stage)}    来源：{source}    倒计时：{threat.CountdownTicks}");
        }
    }

    private void RefreshActionList(WorldSiteState site)
    {
        ClearChildren(_siteActionList);
        if (TryAppendSiteExplorationAlertChoices(site))
        {
            return;
        }

        WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId);
        if (IsSiteExplorationActive(site, definition) &&
            TryAppendSiteExplorationPointActions(site, definition))
        {
            return;
        }

        string selectedThreatId = ResolveSelectedThreatId(site);
        IReadOnlyList<WorldActionViewModel> actions = _worldActionResolver.GetAvailableActions(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            _siteHudSiteId,
            selectedThreatId);

        foreach (WorldActionViewModel action in actions)
        {
            if (IsFacilityBuildAction(action.ActionId))
            {
                continue;
            }

            Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = BuildActionButtonText(action);
            button.Disabled = !action.IsEnabled;

            if (action.IsEnabled)
            {
                button.Pressed += () => ExecuteSiteAction(action);
            }

            _siteActionList.AddChild(button);
        }

        if (_siteActionList.GetChildCount() == 0)
        {
            AddMutedLine(_siteActionList, "暂无可执行行动");
        }
    }
}
