using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;
public partial class WorldSiteRoot
{
    private enum SiteManagementSection
    {
        Build,
        Overview
    }

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
        WorldSitePeacetimeHudNodeRefs hudRefs = WorldSitePeacetimeHudNodeRefs.Resolve(_siteHudRoot, nameof(WorldSiteRoot));
        _sitePeacetimePanel = hudRefs.SitePeacetimePanel;
        _siteBottomCommandHost = hudRefs.BottomCommandHost;
        _siteMinimapHost = hudRefs.MinimapHost;
        _siteModalHost = hudRefs.ModalHost;
        _battleRuntimeSummaryBar = hudRefs.BattleRuntimeSummaryBar;
        _battleRuntimeSummaryList = hudRefs.BattleRuntimeSummaryList;
        _battleRuntimeSummaryPresenter = new BattleRuntimeHeroTroopSummaryPresenter(_battleRuntimeSummaryList);
        _battleRuntimeCommandBar = hudRefs.BattleRuntimeCommandBar;
        _battleRuntimePauseDetailPanel = hudRefs.BattleRuntimePauseDetailPanel;
        _battleRuntimeHeroFrame = hudRefs.BattleRuntimeHeroFrame;
        _battleRuntimeHeroSelectorPresenter = new BattleRuntimeHeroSelectorPresenter(hudRefs.BattleRuntimeHeroSelectorList, SelectBattleRuntimeCommandGroup);
        _battleRuntimeHeroNameLabel = hudRefs.BattleRuntimeHeroNameLabel;
        _battleRuntimeHeroStateLabel = hudRefs.BattleRuntimeHeroStateLabel;
        _battleRuntimeHeroHealthBar = hudRefs.BattleRuntimeHeroHealthBar;
        _battleRuntimeHeroManaBar = hudRefs.BattleRuntimeHeroManaBar;
        _battleRuntimeHeroSkillList = hudRefs.BattleRuntimeHeroSkillList;
        _battleRuntimeLiveRegroupButton = hudRefs.BattleRuntimeLiveRegroupButton;
        _battleRuntimeLiveRetreatButton = hudRefs.BattleRuntimeLiveRetreatButton;
        _battleRuntimePauseRegroupButton = hudRefs.BattleRuntimePauseRegroupButton;
        _battleRuntimePauseRetreatButton = hudRefs.BattleRuntimePauseRetreatButton;
        _battleRuntimeLiveRegroupButton.Pressed += () => SubmitBattleRuntimeTacticalCommand(Rpg.Application.Battle.Commands.CommandKind.Regroup);
        _battleRuntimeLiveRetreatButton.Pressed += () => SubmitBattleRuntimeTacticalCommand(Rpg.Application.Battle.Commands.CommandKind.Retreat);
        _battleRuntimePauseRegroupButton.Pressed += () => SubmitBattleRuntimeTacticalCommand(Rpg.Application.Battle.Commands.CommandKind.Regroup);
        _battleRuntimePauseRetreatButton.Pressed += () => SubmitBattleRuntimeTacticalCommand(Rpg.Application.Battle.Commands.CommandKind.Retreat);
        _battleRuntimeHeroFramePresenter = new BattleRuntimeHeroFramePresenter(
            _battleRuntimeHeroFrame,
            _battleRuntimeHeroNameLabel,
            _battleRuntimeHeroStateLabel,
            _battleRuntimeHeroHealthBar,
            _battleRuntimeHeroManaBar,
            _battleRuntimeHeroSelectorPresenter,
            _battleRuntimeHeroSkillList,
            OnBattleRuntimeSkillSlotPressed);
        ApplySiteHudFullRect("bound");
        _siteHudTitle = hudRefs.SiteHudTitle;
        _siteResourceBarAnimator.Bind(hudRefs.SiteResourceBar);
        _siteResourceLabel = hudRefs.SiteResourceLabel;
        _returnMapButton = hudRefs.ReturnMapButton;
        _siteManagementTabRail = hudRefs.SiteManagementTabRail;
        _siteBuildTabButton = hudRefs.BuildTabButton;
        _siteRecruitTabButton = hudRefs.RecruitTabButton;
        _siteOverviewTabButton = hudRefs.OverviewTabButton;
        _sitePanelCloseButton = hudRefs.SitePanelCloseButton;
        _siteBuildSection = hudRefs.SiteBuildSection;
        _siteOverviewSection = hudRefs.SiteOverviewSection;
        _militaryWorkbenchBackdrop = hudRefs.MilitaryWorkbenchBackdrop;
        _militaryWorkbenchPanel = hudRefs.MilitaryWorkbenchPanel;
        _militaryHeroList = hudRefs.MilitaryHeroList;
        _militaryMusterGrid = hudRefs.MilitaryMusterGrid;
        _militaryHeroSummaryLabel = hudRefs.MilitaryHeroSummaryLabel;
        _militaryNoticeLabel = hudRefs.MilitaryNoticeLabel;
        _militarySelectedHeroPreview = hudRefs.MilitarySelectedHeroPreview;
        _militarySelectedHeroNameLabel = hudRefs.MilitarySelectedHeroNameLabel;
        _militarySelectedHeroCorpsLabel = hudRefs.MilitarySelectedHeroCorpsLabel;
        _militaryCloseButton = hudRefs.MilitaryCloseButton;
        _siteHudBody = hudRefs.SiteHudBody;
        _siteSelectionLabel = hudRefs.SiteSelectionLabel;
        _battlePreparationRosterDock = hudRefs.BattlePreparationRosterDock;
        _battlePreparationRosterList = hudRefs.BattlePreparationRosterList;
        _battlePreparationLaunchDock = hudRefs.BattlePreparationLaunchDock;
        _battlePreparationStartButton = hudRefs.BattlePreparationStartButton;
        _battlePreparationObjectiveThumbnailDock = hudRefs.BattlePreparationObjectiveThumbnailDock;
        _battlePreparationObjectiveThumbnail = hudRefs.BattlePreparationObjectiveThumbnail;
        _battlePreparationTopPromptLabel = hudRefs.BattlePreparationTopPromptLabel;
        _siteBuildingBuildTitle = hudRefs.SiteBuildingBuildTitle;
        _siteBuildingOptionGrid = hudRefs.SiteBuildingOptionGrid;
        _siteBuildingList = hudRefs.SiteBuildingList;
        _strategicManagementDashboardPanelBinder = new StrategicManagementDashboardPanelBinder(
            _siteResourceLabel,
            _siteHudBody,
            _siteSelectionLabel,
            _siteBuildingList,
            _siteBuildingBuildTitle,
            _siteBuildingOptionGrid,
            OnStrategicBuildBuildingSelected);
        _strategicMilitaryWorkbenchBinder = new StrategicMilitaryWorkbenchBinder(
            _militaryHeroList,
            _militaryMusterGrid,
            _militaryHeroSummaryLabel,
            _militaryNoticeLabel,
            _militarySelectedHeroPreview,
            _militarySelectedHeroNameLabel,
            _militarySelectedHeroCorpsLabel,
            OnStrategicMilitaryHeroSelected,
            OnStrategicRecruitCorpsForHeroPressed);
        _siteNoticeLabel = hudRefs.SiteNoticeLabel;
        Label buildingTitleLabel = hudRefs.BuildingTitleLabel;
        Label noticeTitleLabel = hudRefs.NoticeTitleLabel;

        if (buildingTitleLabel != null)
        {
            buildingTitleLabel.Text = "建筑总览";
        }

        if (noticeTitleLabel != null)
        {
            noticeTitleLabel.Text = "最近反馈";
        }

        if (_returnMapButton != null)
        {
            _returnMapButton.Text = "返回";
            _returnMapButton.TooltipText = "";
            _returnMapButton.Pressed += () => ReturnToReturnScene(_siteHudReturnScenePath);
            WireSiteManagementTabHover(_returnMapButton, "返回");
        }

        if (_siteBuildTabButton != null)
        {
            _siteBuildTabButton.Pressed += () => OpenSiteManagementSectionWithBounce(SiteManagementSection.Build, _siteBuildTabButton, "建造");
            WireSiteManagementTabHover(_siteBuildTabButton, "建造");
        }

        if (_siteRecruitTabButton != null)
        {
            _siteRecruitTabButton.Pressed += OpenStrategicMilitaryWorkbench;
            WireSiteManagementTabHover(_siteRecruitTabButton, "招兵");
        }

        if (_siteOverviewTabButton != null)
        {
            _siteOverviewTabButton.Pressed += () => OpenSiteManagementSectionWithBounce(SiteManagementSection.Overview, _siteOverviewTabButton, "总览");
            WireSiteManagementTabHover(_siteOverviewTabButton, "总览");
        }

        if (_sitePanelCloseButton != null)
        {
            _sitePanelCloseButton.Pressed += CloseSiteManagementPanelToRail;
        }

        if (_militaryCloseButton != null)
        {
            _militaryCloseButton.Pressed += CloseStrategicMilitaryWorkbench;
        }

        UpdateSiteManagementEntryVisibility("build");

        if (_battlePreparationStartButton != null)
        {
            _battlePreparationStartButton.Pressed += LaunchPreparedBattle;
        }

        if (_battlePreparationObjectiveThumbnail != null)
        {
            _battlePreparationObjectiveThumbnail.ObjectiveZoneSelected += SelectBattlePreparationObjectiveZone;
        }

        BuildBattleObjectiveMapDialog();
        if (_postBattleSettlementDialog == null)
        {
            _postBattleSettlementDialog = GameUiSceneFactory.CreatePostBattleSettlementDialog(nameof(WorldSiteRoot));
            if (_postBattleSettlementDialog != null)
            {
                (_siteModalHost ?? _siteHudRoot ?? (Node)this).AddChild(_postBattleSettlementDialog);
                _postBattleSettlementDialog.ManageCityPressed += OnPostBattleSettlementManageCityPressed;
                _postBattleSettlementDialog.ReturnPressed += OnPostBattleSettlementReturnPressed;
            }
        }

    }

    private void BuildBattleObjectiveMapDialog()
    {
        if (_battleObjectiveMapDialog != null)
        {
            return;
        }

        _battleObjectiveMapDialog = GameUiSceneFactory.CreateBattleObjectiveMapDialog(nameof(WorldSiteRoot));
        if (_battleObjectiveMapDialog == null)
        {
            return;
        }

        (_siteModalHost ?? _siteHudRoot ?? (Node)this).AddChild(_battleObjectiveMapDialog);
        _battleObjectiveMapDialog.CompanySelected += OnBattleObjectiveDialogCompanySelected;
        _battleObjectiveMapDialog.ObjectiveZoneSelected += OnBattleObjectiveDialogObjectiveSelected;
        _battleObjectiveMapDialog.Closed += () => RefreshBattlePreparationPlanUi("", "battle_preparation_objective_dialog_closed");
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
        _selectedStrategicBuildingDefinitionId = "";
        _siteManagementPanelOpen = false;
        ClearStrategicBuildingPlacementPreview();
        _postBattleSettlementDialog?.Close();
        _postBattleSettlementDialogOpen = false;
        CloseStrategicMilitaryWorkbench();
        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = string.IsNullOrWhiteSpace(_siteHudReturnScenePath);
            _returnMapButton.TooltipText = "";
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
            $"SwitchedToSiteManagementUi site={siteId} hudVisible={_siteHudRoot?.Visible == true} panelVisible={_sitePeacetimePanel?.Visible == true} hudRect={DescribeControlRect(_siteHudRoot)} panelRect={DescribeControlRect(_sitePeacetimePanel)} viewport={GetViewportRect().Size} returnScene={_siteHudReturnScenePath}");
    }

    private void ShowPostBattleSettlementDialog(
        BattleOutcome outcome,
        BattleStartRequest request,
        WorldActionResult applyResult,
        string returnScenePath)
    {
        BuildSiteHud();
        StrategicWorldRuntime.EnsureInitialized();

        string siteId = ResolveRequestSiteId(request);
        _postBattleSettlementOutcome = outcome;
        _postBattleSettlementRequest = request;
        _postBattleSettlementApplyResult = applyResult;
        _postBattleSettlementSiteId = siteId;
        _postBattleSettlementReturnScenePath = ResolveBattleResultReturnScenePath(returnScenePath);
        _siteHudSiteId = siteId;
        _siteHudReturnScenePath = _postBattleSettlementReturnScenePath;
        _selectedPlacementId = "";
        _selectedStrategicBuildingDefinitionId = "";
        _siteManagementPanelOpen = false;
        ClearStrategicBuildingPlacementPreview();
        CloseStrategicMilitaryWorkbench();
        _postBattleSettlementDialogOpen = true;

        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("post_battle_settlement_dialog");
        }

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        UpdateSiteManagementEntryVisibility("post_battle_settlement_dialog");
        SetBattlePreparationHudVisible(false);

        bool manageCityAvailable = outcome == BattleOutcome.Victory && CanOpenManagedCityDetail(siteId);
        _postBattleSettlementDialog?.Bind(new PostBattleSettlementDialogData
        {
            Title = GetBattleOutcomeLabel(outcome),
            ResultText = BuildPostBattleSettlementDialogText(outcome, applyResult, siteId, manageCityAvailable),
            ManageCityAvailable = manageCityAvailable
        });
        _postBattleSettlementDialog?.Open();

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"PostBattleSettlementDialogShown outcome={outcome} site={siteId} manageCityAvailable={manageCityAvailable} returnScene={_postBattleSettlementReturnScenePath}");
    }

    private string BuildPostBattleSettlementDialogText(
        BattleOutcome outcome,
        WorldActionResult applyResult,
        string siteId,
        bool manageCityAvailable)
    {
        List<string> lines = new()
        {
            $"地点：{ResolveSiteName(siteId)}",
            $"结果：{GetBattleOutcomeLabel(outcome)}"
        };

        if (!string.IsNullOrWhiteSpace(applyResult?.Message))
        {
            lines.Add(applyResult.Message.Trim());
        }
        else
        {
            lines.Add("结算已完成。");
        }

        if (outcome == BattleOutcome.Victory)
        {
            lines.Add(manageCityAvailable
                ? "该地点已进入我方控制，可以直接进入城池经营。"
                : "该地点的战后结果已写回；当前地点暂未开放城池经营界面。");
        }
        else
        {
            lines.Add("部队损伤和战斗结果已写回，可以返回大地图。");
        }

        return string.Join("\n", lines);
    }

    private void OnPostBattleSettlementManageCityPressed()
    {
        if (!CanOpenManagedCityDetail(_postBattleSettlementSiteId))
        {
            _postBattleSettlementDialog?.Bind(new PostBattleSettlementDialogData
            {
                Title = GetBattleOutcomeLabel(_postBattleSettlementOutcome),
                ResultText = BuildPostBattleSettlementDialogText(
                    _postBattleSettlementOutcome,
                    _postBattleSettlementApplyResult,
                    _postBattleSettlementSiteId,
                    manageCityAvailable: false),
                ManageCityAvailable = false
            });
            return;
        }

        _postBattleSettlementDialog?.Close();
        _postBattleSettlementDialogOpen = false;
        SwitchToNonBattleUi(
            _postBattleSettlementOutcome,
            _postBattleSettlementRequest,
            _postBattleSettlementApplyResult,
            _postBattleSettlementReturnScenePath);
    }

    private void OnPostBattleSettlementReturnPressed()
    {
        _postBattleSettlementDialog?.Close();
        _postBattleSettlementDialogOpen = false;
        ReturnToReturnScene(_postBattleSettlementReturnScenePath);
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
        _battlePreparationHudRestPositions.Clear();
        ApplySitePeacetimePanelLayout();
        _siteResourceBarAnimator.ApplyLayout();
        UpdateMainWorldViewportLayout(reason);
        UpdateSiteResourceBarVisibility(reason);

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

        // Site management now overlays the fullscreen map. Keep this panel bounded
        // so the map remains readable and the left tab rail can return on close.
        _sitePeacetimePanel.AnchorLeft = 0.0f;
        _sitePeacetimePanel.AnchorTop = 0.0f;
        _sitePeacetimePanel.AnchorRight = 0.0f;
        _sitePeacetimePanel.AnchorBottom = 0.0f;
        _sitePeacetimePanel.OffsetLeft = 0.0f;
        _sitePeacetimePanel.OffsetTop = 72.0f;
        _sitePeacetimePanel.OffsetRight = 640.0f;
        _sitePeacetimePanel.OffsetBottom = 840.0f;
        _sitePeacetimePanel.CustomMinimumSize = new Vector2(640.0f, 0.0f);
    }

    private void OpenSiteManagementSectionWithBounce(SiteManagementSection section, Button button, string expandedText)
    {
        _selectedSiteManagementSection = section;
        _siteManagementPanelOpen = true;
        ApplySiteManagementSectionVisibility();
        SiteManagementDrawerAnimator.OpenPanelAfterTabRetracts(this, _sitePeacetimePanel, button, expandedText, () => UpdateSiteManagementEntryVisibility("section_selected"));
    }

    private void ApplySiteManagementSectionVisibility()
    {
        // Section switching is Presentation-only state. Strategic facts still come
        // from the dashboard view model and mutate only through command callbacks.
        ApplySiteManagementSectionVisibility(
            _siteBuildSection,
            _siteBuildTabButton,
            SiteManagementSection.Build);
        ApplySiteManagementSectionVisibility(
            _siteOverviewSection,
            _siteOverviewTabButton,
            SiteManagementSection.Overview);
        _siteRecruitTabButton?.SetPressedNoSignal(false);
        _returnMapButton?.SetPressedNoSignal(false);
    }

    private void ApplySiteManagementSectionVisibility(
        Control section,
        Button tabButton,
        SiteManagementSection sectionKind)
    {
        bool selected = _selectedSiteManagementSection == sectionKind;
        if (section != null)
        {
            section.Visible = selected;
        }

        tabButton?.SetPressedNoSignal(selected);
    }

    private void UpdateSitePeacetimePanelVisibility(string reason) => UpdateSiteManagementEntryVisibility(reason);

    private void UpdateSiteManagementEntryVisibility(string reason)
    {
        if (_sitePeacetimePanel == null && _siteManagementTabRail == null)
        {
            return;
        }

        if (_battleRuntimeEnabled)
        {
            SetSiteManagementPanelVisible(false);
            SetSiteManagementTabRailVisible(false);
            UpdateSiteResourceBarVisibility(reason);
            return;
        }

        bool shouldShowEntry = ShouldShowSiteManagementEntry();
        bool placementActive = !string.IsNullOrWhiteSpace(_selectedStrategicBuildingDefinitionId);
        bool workbenchOpen = _militaryWorkbenchPanel?.Visible == true || _militaryWorkbenchBackdrop?.Visible == true;
        bool showPanel = shouldShowEntry && _siteManagementPanelOpen && !placementActive && !workbenchOpen;
        bool showRail = shouldShowEntry && !_siteManagementPanelOpen && !placementActive && !workbenchOpen;
        if (!shouldShowEntry)
        {
            _siteManagementPanelOpen = false;
        }

        if (showPanel)
        {
            ApplySitePeacetimePanelLayout();
        }

        SetSiteManagementPanelVisible(showPanel);
        SetSiteManagementTabRailVisible(showRail);
        UpdateSiteResourceBarVisibility(reason);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteManagementEntryVisibilityChanged reason={reason} rail={showRail} panel={showPanel} placement={placementActive} workbench={workbenchOpen} panelRect={DescribeControlRect(_sitePeacetimePanel)}");
    }

    private void UpdateSiteResourceBarVisibility(string reason)
    {
        bool placementActive = !string.IsNullOrWhiteSpace(_selectedStrategicBuildingDefinitionId);
        _siteResourceBarAnimator.Update(
            _siteHudRoot?.Visible == true && !_battleRuntimeEnabled && !_postBattleSettlementDialogOpen && CanOpenManagedCityDetail(_siteHudSiteId),
            placementActive || _battlePreparationHudRetreated,
            this, reason);
    }

    private void SetSiteManagementPanelVisible(bool visible)
    {
        if (_sitePeacetimePanel == null)
        {
            return;
        }

        _sitePeacetimePanel.Visible = visible;
        _sitePeacetimePanel.MouseFilter = visible
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }

    private void SetSiteManagementTabRailVisible(bool visible)
    {
        if (_siteManagementTabRail == null)
        {
            return;
        }

        _siteManagementTabRail.Visible = visible;
        _siteManagementTabRail.MouseFilter = visible
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }

    private void CloseSiteManagementPanelToRail()
    {
        CloseSiteManagementPanelWithBounce();
    }

    private void CloseSiteManagementPanelWithBounce()
    {
        _siteManagementPanelOpen = false;
        if (!ShouldShowSiteManagementEntry() || _battleRuntimeEnabled)
        {
            UpdateSiteManagementEntryVisibility("panel_closed");
            return;
        }

        SiteManagementDrawerAnimator.ClosePanelThenShowRail(
            this,
            _sitePeacetimePanel,
            _siteManagementTabRail,
            () => SetSiteManagementPanelVisible(false),
            () => SetSiteManagementTabRailVisible(true));
    }

    private void WireSiteManagementTabHover(Button button, string expandedText)
    {
        SiteManagementDrawerAnimator.ApplyTabDrawerState(button, expandedText, expanded: false, animated: false);
        button.MouseEntered += () => SiteManagementDrawerAnimator.ApplyTabDrawerState(button, expandedText, expanded: true, animated: true);
        button.MouseExited += () => SiteManagementDrawerAnimator.ApplyTabDrawerState(button, expandedText, expanded: false, animated: true);
    }

    private bool ShouldShowSiteManagementEntry()
    {
        bool siteHudVisible = _siteHudRoot?.Visible == true;
        if (!siteHudVisible ||
            _postBattleSettlementDialogOpen ||
            _isBattlePreparationActive ||
            _battleRuntimeEnabled)
        {
            return false;
        }

        // The build/recruit/overview entry is city-management UI. Strategic
        // Management owns city control; legacy WorldSite ownership is presentation cache.
        return CanOpenManagedCityDetail(_siteHudSiteId);
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
            EnableBattlePerceptionOverlayForRuntime();
        }

        if (enabled && _siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            UpdateSiteManagementEntryVisibility("battle_runtime_enabled");

            SetBattlePreparationHudVisible(false);
        }
        else if (!enabled)
        {
            SetBattleRuntimeCommandPauseActive(false, "runtime_disabled");
            _battlePerceptionOverlayVisible = false;
            _selectedBattleRuntimeGroupKey = "";
            _battleRuntimeRequest = null;
            _activeBattleGroupRuntimeResolution = null;
            ClearBattleMovementTweenProbe();
            SetHoveredBattleRuntimeEntity("");
            _unitRoot?.ClearCommandSelection();
            ClearBattlePerceptionOverlay();
            _battleDestinationBeaconMarkerPresenter.Clear();
            RefreshBattleRuntimeHeroFrame();
            SetBattlePreparationHudVisible(false);

            if (_siteBottomCommandHost != null)
            {
                _siteBottomCommandHost.Visible = false;
            }

            if (_battleRuntimeSummaryBar != null)
            {
                _battleRuntimeSummaryBar.Visible = false;
            }

            if (_battleRuntimeCommandBar != null)
            {
                _battleRuntimeCommandBar.Visible = false;
            }
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

    private static bool TryResolveStrategicManagementCityId(string worldSiteId, out string cityId)
    {
        StrategicManagementRuntime.EnsureInitialized();
        return StrategicManagementRuntime.LocationMappings.TryResolveCityIdForMapSite(worldSiteId, out cityId);
    }

    private static bool TryResolveStrategicManagementLocationId(string worldSiteId, out string locationId)
    {
        StrategicManagementRuntime.EnsureInitialized();
        return StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(worldSiteId, out locationId);
    }

    private static string BuildStrategicManagementCityUnavailableNotice(string worldSiteId)
    {
        StrategicManagementRuntime.EnsureInitialized();
        return StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(worldSiteId, out _)
            ? "当前战略地点不是可经营城市。"
            : "当前场景尚未映射到战略经营地点。";
    }

    private static bool CanOpenSiteDetail(WorldSiteState site)
    {
        return site != null &&
               site.OwnerFactionId == StrategicWorldRuntime.State.PlayerFactionId &&
               site.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged;
    }

    private static bool CanOpenManagedCityDetail(string worldSiteId)
    {
        StrategicManagementRuntime.EnsureInitialized();
        if (!TryResolveStrategicManagementCityId(worldSiteId, out string cityId))
        {
            return false;
        }

        return StrategicManagementRuntime.State.Locations.TryGetValue(cityId, out StrategicLocationState location) &&
               location.OwnerFactionId == StrategicManagementIds.FactionPlayer &&
               location.ControlState == StrategicLocationControlState.PlayerHeld &&
               StrategicManagementRuntime.State.Cities.ContainsKey(cityId);
    }

    private void RefreshSiteManagementUi(string notice = "", BattleOutcome outcome = BattleOutcome.None)
    {
        BindSiteManagementPanel(notice, outcome);
    }

    private void OnStrategicBuildBuildingSelected(string buildingDefinitionId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!StrategicManagementRuntime.Definitions.Buildings.TryGetValue(
                buildingDefinitionId ?? "",
                out StrategicBuildingDefinition building))
        {
            RefreshSiteManagementUi($"建筑放置失败：{StrategicManagementDashboardPanelBinder.FormatFailureReason(StrategicFailureReasons.MissingBuilding)}");
            return;
        }

        _selectedStrategicBuildingDefinitionId = building.BuildingDefinitionId;
        _selectedPlacementId = "";
        _siteManagementPanelOpen = false;
        UpdateSiteManagementEntryVisibility("building_placement_selected");
        UpdateStrategicBuildingPlacementPreview();
        RefreshSiteManagementUi($"{building.DisplayName}已选择，请在地图建设区域点击放置。");
    }

    private void OpenStrategicMilitaryWorkbench()
    {
        _siteRecruitTabButton?.SetPressedNoSignal(false);
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out _))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        _selectedMilitaryWorkbenchHeroId = "";
        _siteManagementPanelOpen = false;
        SetSiteManagementPanelVisible(false);
        SiteManagementDrawerAnimator.ApplyTabDrawerState(_siteRecruitTabButton, "招兵", expanded: false, animated: true);
        _siteResourceBarAnimator.SetModalOverlayBypass(true);
        SiteManagementCenteredModalAnimator.OpenCenteredModalAfterDelay(this, _militaryWorkbenchPanel, _militaryWorkbenchBackdrop, () => { SetSiteManagementTabRailVisible(false); BindStrategicMilitaryWorkbench(); });
    }

    private void CloseStrategicMilitaryWorkbench()
    {
        bool wasOpen = _militaryWorkbenchPanel?.Visible == true || _militaryWorkbenchBackdrop?.Visible == true;
        _selectedMilitaryWorkbenchHeroId = "";
        if (!wasOpen)
        {
            SiteManagementCenteredModalAnimator.CancelCenteredModal(_militaryWorkbenchPanel, _militaryWorkbenchBackdrop, () =>
            {
                _siteResourceBarAnimator.SetModalOverlayBypass(false);
                _strategicMilitaryWorkbenchBinder?.Hide();
            });
            return;
        }

        SiteManagementCenteredModalAnimator.CloseCenteredModal(this, _militaryWorkbenchPanel, _militaryWorkbenchBackdrop, () => { _strategicMilitaryWorkbenchBinder?.Hide(); _siteResourceBarAnimator.SetModalOverlayBypass(false); UpdateSiteManagementEntryVisibility("military_workbench_closed"); SiteManagementDrawerAnimator.AnimateRailTabsIn(this, _siteManagementTabRail); });
    }

    private void BindStrategicMilitaryWorkbench(string notice = "")
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            CloseStrategicMilitaryWorkbench();
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicManagementDashboardViewModel heroSelectionDashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            cityId);
        if (string.IsNullOrWhiteSpace(_selectedMilitaryWorkbenchHeroId) ||
            !heroSelectionDashboard.Heroes.Any(hero => hero.HeroId == _selectedMilitaryWorkbenchHeroId))
        {
            _selectedMilitaryWorkbenchHeroId = heroSelectionDashboard.Heroes.FirstOrDefault()?.HeroId ?? "";
        }

        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildHeroCorpsWorkbenchDashboard(
            StrategicManagementIds.FactionPlayer,
            cityId,
            _selectedMilitaryWorkbenchHeroId);
        _strategicMilitaryWorkbenchBinder?.Bind(dashboard, _selectedMilitaryWorkbenchHeroId, notice);
        UpdateSiteManagementEntryVisibility("military_workbench_bound");
    }

    private void OnStrategicMilitaryHeroSelected(string heroId)
    {
        _selectedMilitaryWorkbenchHeroId = heroId ?? "";
        BindStrategicMilitaryWorkbench();
    }

    private void OnStrategicRecruitCorpsForHeroPressed(string corpsDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(_selectedMilitaryWorkbenchHeroId))
        {
            BindStrategicMilitaryWorkbench("请先选择要调整编制的英雄。");
            return;
        }

        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            CloseStrategicMilitaryWorkbench();
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.RecruitCorpsForHero(
            StrategicManagementRuntime.State,
            cityId,
            _selectedMilitaryWorkbenchHeroId,
            corpsDefinitionId);
        if (result.Success)
        {
            StrategicManagementRuntime.SaveCurrentState();
        }

        string notice = BuildStrategicManagementCommandNotice("招募编制", result);
        RefreshSiteManagementUi(notice);
        BindStrategicMilitaryWorkbench(notice);
    }

    private void HandleStrategicManagementCommandResult(string actionName, StrategicCommandResult result)
    {
        RefreshSiteManagementUi(BuildStrategicManagementCommandNotice(actionName, result));
    }

    private static string BuildStrategicManagementCommandNotice(string actionName, StrategicCommandResult result)
    {
        string commandName = string.IsNullOrWhiteSpace(actionName) ? "战略经营命令" : actionName.Trim();
        if (result == null)
        {
            return $"{commandName}失败：定义缺失";
        }

        if (!result.Success)
        {
            return $"{commandName}失败：{StrategicManagementDashboardPanelBinder.FormatFailureReason(result.FailureReason)}";
        }

        return string.IsNullOrWhiteSpace(result.CreatedEntityId)
            ? $"{commandName}完成"
            : $"{commandName}完成：{result.CreatedEntityId}";
    }

    private void BindSiteManagementPanel(string notice = "", BattleOutcome outcome = BattleOutcome.None)
    {
        StrategicWorldRuntime.EnsureInitialized();
        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = false;
        }

        if (_battleRuntimeSummaryBar != null)
        {
            _battleRuntimeSummaryBar.Visible = false;
        }

        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = false;
        }

        RefreshBattleRuntimeHeroFrame();

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        EnsureLegacySiteGarrisonPlacementsForPresentation(site, definition);
        EnsureSitePlacementsRespectTerrain(site, definition);

        if (TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
                StrategicManagementIds.FactionPlayer,
                cityId);
            _siteHudTitle.Text = outcome == BattleOutcome.None
                ? $"{dashboard.SelectedCity.DisplayName} · 战略经营"
                : $"{dashboard.SelectedCity.DisplayName} · {GetBattleOutcomeLabel(outcome)}";
            _strategicManagementDashboardPanelBinder.Bind(dashboard);
            _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice) ? StrategicWorldRuntime.LastNotice : notice.Trim();
        }
        else if (TryResolveStrategicManagementLocationId(_siteHudSiteId, out string locationId))
        {
            StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildLocationDashboard(
                StrategicManagementIds.FactionPlayer,
                locationId);
            _siteHudTitle.Text = outcome == BattleOutcome.None
                ? $"{dashboard.SelectedLocation.DisplayName} · 战略地点"
                : $"{dashboard.SelectedLocation.DisplayName} · {GetBattleOutcomeLabel(outcome)}";
            _strategicManagementDashboardPanelBinder.BindLocation(dashboard);
            _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId)
                : notice.Trim();
        }
        else
        {
            _siteHudTitle.Text = outcome == BattleOutcome.None
                ? $"{ResolveSiteName(_siteHudSiteId)} · 战略经营未开放"
                : $"{ResolveSiteName(_siteHudSiteId)} · {GetBattleOutcomeLabel(outcome)}";
            _strategicManagementDashboardPanelBinder.Bind(new StrategicManagementDashboardViewModel());
            _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId)
                : notice.Trim();
        }

        SetBattlePreparationHudVisible(false);

        RefreshSiteMapEntities(site, definition);
        UpdateSitePeacetimePanelVisibility("refresh");
    }

    private void EnsureLegacySiteGarrisonPlacementsForPresentation(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (CanOpenManagedCityDetail(_siteHudSiteId))
        {
            return;
        }

        _deploymentService.EnsureGarrisonPlacements(site, definition);
    }

    private void BindSettlementReportPanel(BattleOutcome outcome, WorldActionResult applyResult)
    {
        // V0 settlement report still reuses the site-management panel layout. Keeping
        // this binder boundary prevents settlement presentation from growing inside
        // unrelated management refresh call sites.
        BindSiteManagementPanel(applyResult?.Message, outcome);
    }

    private void SetBattlePreparationHudVisible(bool visible)
    {
        if (_battlePreparationRosterDock != null)
        {
            _battlePreparationRosterDock.Visible = visible;
        }

        if (_battlePreparationLaunchDock != null)
        {
            _battlePreparationLaunchDock.Visible = visible;
        }

        if (_siteMinimapHost != null)
        {
            _siteMinimapHost.Visible = visible;
        }

        if (_battlePreparationObjectiveThumbnailDock != null)
        {
            _battlePreparationObjectiveThumbnailDock.Visible = false;
        }

        if (!visible)
        {
            _battleDestinationBeaconMarkerPresenter.Clear();
            SetBattlePreparationTopPrompt("");
            SetBattlePreparationHudRetreated(false, "battle_preparation_hidden");
            ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.None, "battle_preparation_hidden");
        }
        else
        {
            ApplyBattleMapOperationHudSuppressionVisibility("battle_preparation_visible");
        }
    }

    private void SetBattlePreparationHudRetreated(bool retreated, string reason)
    {
        if (_battlePreparationHudRetreated == retreated)
        {
            if (!retreated)
            {
                _battlePreparationHudRetreatTween?.Kill();
                _battlePreparationHudRetreatTween = null;
            }
            UpdateSiteResourceBarVisibility("battle_preparation_hud_retreat:" + (reason ?? ""));
            return;
        }
        _battlePreparationHudRetreatTween?.Kill();
        _battlePreparationHudRetreatTween = null;
        Control[] controls =
        {
            _battlePreparationRosterDock,
            _battlePreparationLaunchDock,
            _battlePreparationObjectiveThumbnailDock
        };
        Vector2[] offsets =
        {
            new(-240.0f, 0.0f),
            new(0.0f, 140.0f),
            new(340.0f, 0.0f)
        };
        for (int index = 0; index < controls.Length; index++)
        {
            Control control = controls[index];
            if (control == null)
            {
                continue;
            }

            _battlePreparationHudRestPositions.TryAdd(control, control.Position);
            control.MouseFilter = retreated
                ? Control.MouseFilterEnum.Ignore
                : Control.MouseFilterEnum.Stop;
        }

        if (!IsInsideTree())
        {
            for (int index = 0; index < controls.Length; index++)
            {
                Control control = controls[index];
                if (control != null && _battlePreparationHudRestPositions.TryGetValue(control, out Vector2 restPosition))
                {
                    control.Position = retreated ? restPosition + offsets[index] : restPosition;
                }
            }

            _battlePreparationHudRetreated = retreated;
            UpdateSiteResourceBarVisibility("battle_preparation_hud_retreat:" + (reason ?? ""));
            return;
        }

        Tween tween = CreateTween().BindNode(this);
        tween.SetParallel(true);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);
        for (int index = 0; index < controls.Length; index++)
        {
            Control control = controls[index];
            if (control != null && _battlePreparationHudRestPositions.TryGetValue(control, out Vector2 restPosition))
            {
                tween.TweenProperty(control, "position", retreated ? restPosition + offsets[index] : restPosition, 0.22);
            }
        }

        _battlePreparationHudRetreated = retreated;
        _battlePreparationHudRetreatTween = tween;
        UpdateSiteResourceBarVisibility("battle_preparation_hud_retreat:" + (reason ?? ""));
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationHudRetreatChanged active={retreated} reason={reason ?? ""}");
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

    private void SetBattlePreparationTopPrompt(string text)
    {
        if (_battlePreparationTopPromptLabel == null)
        {
            return;
        }

        string prompt = text?.Trim() ?? "";
        Control promptDock = _battlePreparationTopPromptLabel.GetParent() as Control;
        if (promptDock != null)
        {
            promptDock.Visible = _isBattlePreparationActive && !string.IsNullOrWhiteSpace(prompt);
            promptDock.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        _battlePreparationTopPromptLabel.Text = prompt;
        _battlePreparationTopPromptLabel.Visible = !string.IsNullOrWhiteSpace(prompt);
        _battlePreparationTopPromptLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

}
