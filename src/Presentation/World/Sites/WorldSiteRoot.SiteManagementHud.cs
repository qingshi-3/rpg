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
        Conscription,
        Corps,
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
        _battleRuntimeCommandBar = hudRefs.BattleRuntimeCommandBar;
        _battleRuntimeHeroFrame = hudRefs.BattleRuntimeHeroFrame;
        _battleRuntimeHeroSelectorPresenter = new BattleRuntimeHeroSelectorPresenter(hudRefs.BattleRuntimeHeroSelectorList, SelectBattleRuntimeCommandGroup);
        _battleRuntimeHeroNameLabel = hudRefs.BattleRuntimeHeroNameLabel;
        _battleRuntimeHeroStateLabel = hudRefs.BattleRuntimeHeroStateLabel;
        _battleRuntimeHeroHealthBar = hudRefs.BattleRuntimeHeroHealthBar;
        _battleRuntimeHeroManaBar = hudRefs.BattleRuntimeHeroManaBar;
        _battleRuntimeHeroSkillList = hudRefs.BattleRuntimeHeroSkillList;
        _battleRuntimeRegroupButton = hudRefs.BattleRuntimeRegroupButton;
        _battleRuntimeHeroFramePresenter = new BattleRuntimeHeroFramePresenter(
            _battleRuntimeHeroFrame,
            _battleRuntimeHeroNameLabel,
            _battleRuntimeHeroStateLabel,
            _battleRuntimeHeroHealthBar,
            _battleRuntimeHeroManaBar,
            _battleRuntimeHeroSelectorPresenter,
            _battleRuntimeHeroSkillList,
            _battleRuntimeRegroupButton,
            OnBattleRuntimeSkillSlotPressed);
        ApplySiteHudFullRect("bound");
        _siteHudTitle = hudRefs.SiteHudTitle;
        _siteResourceLabel = hudRefs.SiteResourceLabel;
        _returnMapButton = hudRefs.ReturnMapButton;
        _siteBuildTabButton = hudRefs.BuildTabButton;
        _siteConscriptionTabButton = hudRefs.ConscriptionTabButton;
        _siteRecruitTabButton = hudRefs.RecruitTabButton;
        _siteCorpsTabButton = hudRefs.CorpsTabButton;
        _siteOverviewTabButton = hudRefs.OverviewTabButton;
        _siteBuildSection = hudRefs.SiteBuildSection;
        _siteConscriptionSection = hudRefs.SiteConscriptionSection;
        _siteCorpsSection = hudRefs.SiteCorpsSection;
        _siteOverviewSection = hudRefs.SiteOverviewSection;
        _militaryWorkbenchPanel = hudRefs.MilitaryWorkbenchPanel;
        _militaryHeroList = hudRefs.MilitaryHeroList;
        _militaryMusterGrid = hudRefs.MilitaryMusterGrid;
        _militaryHeroSummaryLabel = hudRefs.MilitaryHeroSummaryLabel;
        _militaryNoticeLabel = hudRefs.MilitaryNoticeLabel;
        _militaryBackButton = hudRefs.MilitaryBackButton;
        _militaryCloseButton = hudRefs.MilitaryCloseButton;
        _siteHudBody = hudRefs.SiteHudBody;
        _siteSelectionLabel = hudRefs.SiteSelectionLabel;
        _battlePreparationRosterDock = hudRefs.BattlePreparationRosterDock;
        _battlePreparationRosterList = hudRefs.BattlePreparationRosterList;
        _battlePreparationPlanBar = hudRefs.BattlePreparationPlanBar;
        _battlePreparationCompanyLabel = hudRefs.BattlePreparationCompanyLabel;
        _battlePreparationObjectiveLabel = hudRefs.BattlePreparationObjectiveLabel;
        _battlePreparationRuleButtonRow = hudRefs.BattlePreparationRuleButtonRow;
        _battlePreparationMoveFirstButton = hudRefs.BattlePreparationMoveFirstButton;
        _battlePreparationAttackFirstButton = hudRefs.BattlePreparationAttackFirstButton;
        _battlePreparationHoldButton = hudRefs.BattlePreparationHoldButton;
        _battlePreparationStartButton = hudRefs.BattlePreparationStartButton;
        _battlePreparationObjectiveThumbnailDock = hudRefs.BattlePreparationObjectiveThumbnailDock;
        _battlePreparationObjectiveThumbnail = hudRefs.BattlePreparationObjectiveThumbnail;
        _siteBuildingBuildTitle = hudRefs.SiteBuildingBuildTitle;
        _siteBuildingOptionGrid = hudRefs.SiteBuildingOptionGrid;
        _siteConscriptionList = hudRefs.SiteConscriptionList;
        _siteBuildingList = hudRefs.SiteBuildingList;
        _siteGarrisonList = hudRefs.SiteGarrisonList;
        _strategicManagementDashboardPanelBinder = new StrategicManagementDashboardPanelBinder(
            _siteResourceLabel,
            _siteHudBody,
            _siteSelectionLabel,
            _siteBuildingList,
            _siteBuildingBuildTitle,
            _siteBuildingOptionGrid,
            _siteConscriptionList,
            _siteGarrisonList,
            OnStrategicBuildBuildingSelected,
            OnStrategicManualConscriptPressed,
            OnStrategicAutoConscriptionIntensityPressed,
            OnStrategicReplenishCorpsPressed,
            OnStrategicHeroAssignmentPressed);
        _strategicMilitaryWorkbenchBinder = new StrategicMilitaryWorkbenchBinder(
            _militaryWorkbenchPanel,
            _militaryHeroList,
            _militaryMusterGrid,
            _militaryHeroSummaryLabel,
            _militaryNoticeLabel,
            _militaryBackButton,
            OnStrategicMilitaryHeroSelected,
            OnStrategicRecruitCorpsForHeroPressed);
        _siteNoticeLabel = hudRefs.SiteNoticeLabel;
        Label buildingTitleLabel = hudRefs.BuildingTitleLabel;
        Label garrisonTitleLabel = hudRefs.GarrisonTitleLabel;
        Label noticeTitleLabel = hudRefs.NoticeTitleLabel;

        if (buildingTitleLabel != null)
        {
            buildingTitleLabel.Text = "建筑总览";
        }

        if (garrisonTitleLabel != null)
        {
            garrisonTitleLabel.Text = "部队配置";
        }

        if (noticeTitleLabel != null)
        {
            noticeTitleLabel.Text = "最近反馈";
        }

        if (_returnMapButton != null)
        {
            _returnMapButton.Text = "返回";
            _returnMapButton.TooltipText = "返回大地图";
            _returnMapButton.Pressed += () => ReturnToReturnScene(_siteHudReturnScenePath);
        }

        if (_siteBuildTabButton != null)
        {
            _siteBuildTabButton.Pressed += () => SelectSiteManagementSection(SiteManagementSection.Build);
        }

        if (_siteConscriptionTabButton != null)
        {
            _siteConscriptionTabButton.Pressed += () => SelectSiteManagementSection(SiteManagementSection.Conscription);
        }

        if (_siteRecruitTabButton != null)
        {
            _siteRecruitTabButton.Pressed += OpenStrategicMilitaryWorkbench;
        }

        if (_siteCorpsTabButton != null)
        {
            _siteCorpsTabButton.Pressed += () => SelectSiteManagementSection(SiteManagementSection.Corps);
        }

        if (_siteOverviewTabButton != null)
        {
            _siteOverviewTabButton.Pressed += () => SelectSiteManagementSection(SiteManagementSection.Overview);
        }

        if (_militaryBackButton != null)
        {
            _militaryBackButton.Pressed += () =>
            {
                _selectedMilitaryWorkbenchHeroId = "";
                BindStrategicMilitaryWorkbench();
            };
        }

        if (_militaryCloseButton != null)
        {
            _militaryCloseButton.Pressed += CloseStrategicMilitaryWorkbench;
        }

        ApplySiteManagementSectionVisibility();

        if (_battlePreparationMoveFirstButton != null)
        {
            _battlePreparationMoveFirstButton.Pressed += () => SelectBattlePreparationEngagementRule(BattleEngagementRule.MoveFirst);
        }

        if (_battlePreparationAttackFirstButton != null)
        {
            _battlePreparationAttackFirstButton.Pressed += () => SelectBattlePreparationEngagementRule(BattleEngagementRule.AttackFirst);
        }

        if (_battlePreparationHoldButton != null)
        {
            _battlePreparationHoldButton.Pressed += () => SelectBattlePreparationEngagementRule(BattleEngagementRule.Hold);
        }

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

        if (_battleRuntimeRegroupButton != null)
        {
            _battleRuntimeRegroupButton.Pressed += OnBattleRuntimeRegroupPressed;
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
        ClearStrategicBuildingPlacementPreview();
        _postBattleSettlementDialog?.Close();
        _postBattleSettlementDialogOpen = false;
        CloseStrategicMilitaryWorkbench();
        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = string.IsNullOrWhiteSpace(_siteHudReturnScenePath);
            _returnMapButton.TooltipText = _returnMapButton.Disabled ? "没有可返回的大地图场景。" : "返回大地图";
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
        _siteHudRoot.Size = GetViewportRect().Size;
        _battlePreparationHudRestPositions.Clear();
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

        // Site management is a hard split-screen workspace: the left panel touches
        // the window edge and the world viewport fills every pixel to its right.
        _sitePeacetimePanel.AnchorLeft = 0.0f;
        _sitePeacetimePanel.AnchorTop = 0.0f;
        _sitePeacetimePanel.AnchorRight = 0.0f;
        _sitePeacetimePanel.AnchorBottom = 1.0f;
        _sitePeacetimePanel.OffsetLeft = 0.0f;
        _sitePeacetimePanel.OffsetTop = 0.0f;
        _sitePeacetimePanel.OffsetRight = 520.0f;
        _sitePeacetimePanel.OffsetBottom = 0.0f;
        _sitePeacetimePanel.CustomMinimumSize = new Vector2(520.0f, 0.0f);
    }

    private void SelectSiteManagementSection(SiteManagementSection section)
    {
        _selectedSiteManagementSection = section;
        ApplySiteManagementSectionVisibility();
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
            _siteConscriptionSection,
            _siteConscriptionTabButton,
            SiteManagementSection.Conscription);
        ApplySiteManagementSectionVisibility(
            _siteCorpsSection,
            _siteCorpsTabButton,
            SiteManagementSection.Corps);
        ApplySiteManagementSectionVisibility(
            _siteOverviewSection,
            _siteOverviewTabButton,
            SiteManagementSection.Overview);
        _siteRecruitTabButton?.SetPressedNoSignal(false);
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

    private void UpdateSitePeacetimePanelVisibility(string reason)
    {
        if (_sitePeacetimePanel == null)
        {
            return;
        }

        if (IsBattleRuntimeHudActive())
        {
            if (_sitePeacetimePanel.Visible)
            {
                _sitePeacetimePanel.Visible = false;
                GameLog.Info(
                    nameof(WorldSiteRoot),
                    $"SitePeacetimePanelVisibilityChanged visible=False reason={reason} battleRuntime=true panelRect={DescribeControlRect(_sitePeacetimePanel)}");
            }

            return;
        }

        bool shouldShow = ShouldShowSitePeacetimePanel();
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
            $"SitePeacetimePanelVisibilityChanged visible={shouldShow} reason={reason} panelRect={DescribeControlRect(_sitePeacetimePanel)}");
    }

    private bool ShouldShowSitePeacetimePanel()
    {
        bool siteHudVisible = _siteHudRoot?.Visible == true;
        if (!siteHudVisible ||
            _isBattlePreparationActive ||
            _battleRuntimeEnabled)
        {
            return false;
        }

        // The build/recruit/corps/overview panel is city-management UI. Strategic
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
            if (_sitePeacetimePanel != null)
            {
                _sitePeacetimePanel.Visible = false;
            }

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
            _unitRoot?.ClearCommandSelection();
            ClearBattlePerceptionOverlay();
            RefreshBattleRuntimeHeroFrame();
            SetBattlePreparationHudVisible(false);

            if (_siteBottomCommandHost != null)
            {
                _siteBottomCommandHost.Visible = false;
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
        UpdateStrategicBuildingPlacementPreview();
        RefreshSiteManagementUi($"{building.DisplayName}已选择，请在地图建设区域点击放置。");
    }

    private void OnStrategicManualConscriptPressed()
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.ManualConscriptReserveForces(
            StrategicManagementRuntime.State,
            cityId);
        if (result.Success)
        {
            StrategicManagementRuntime.SaveCurrentState();
        }

        HandleStrategicManagementCommandResult("手动征兵", result);
    }

    private void OnStrategicAutoConscriptionIntensityPressed(string intensityId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.SetAutoConscriptionIntensity(
            StrategicManagementRuntime.State,
            cityId,
            intensityId);
        if (result.Success)
        {
            StrategicManagementRuntime.SaveCurrentState();
        }

        HandleStrategicManagementCommandResult("征兵力度", result);
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
        BindStrategicMilitaryWorkbench();
    }

    private void CloseStrategicMilitaryWorkbench()
    {
        _selectedMilitaryWorkbenchHeroId = "";
        _strategicMilitaryWorkbenchBinder?.Hide();
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
        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            cityId);
        _strategicMilitaryWorkbenchBinder?.Bind(dashboard, _selectedMilitaryWorkbenchHeroId, notice);
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

    private void OnStrategicReplenishCorpsPressed(string corpsInstanceId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.ReplenishCorps(
            StrategicManagementRuntime.State,
            cityId,
            corpsInstanceId,
            100);
        HandleStrategicManagementCommandResult("补员编制", result);
    }

    private void OnStrategicHeroAssignmentPressed(string heroId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            cityId);
        StrategicHeroAssignmentViewModel hero = dashboard.Heroes.FirstOrDefault(item => item.HeroId == heroId);

        StrategicCommandResult result;
        if (hero?.HasAssignedCorps == true)
        {
            result = StrategicManagementRuntime.Commands.UnassignCorpsFromHero(
                StrategicManagementRuntime.State,
                heroId);
            HandleStrategicManagementCommandResult("解除英雄编制", result);
            return;
        }

        StrategicCorpsInstanceViewModel availableCorps = dashboard.SelectedCity.CorpsInstances.FirstOrDefault(corps =>
            corps.Status == StrategicCorpsInstanceStatus.Garrisoned &&
            string.IsNullOrWhiteSpace(corps.AssignedHeroId));
        result = StrategicManagementRuntime.Commands.AssignCorpsToHero(
            StrategicManagementRuntime.State,
            heroId,
            availableCorps?.CorpsInstanceId ?? "");
        HandleStrategicManagementCommandResult("分配英雄编制", result);
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

        if (_battlePreparationPlanBar != null)
        {
            _battlePreparationPlanBar.Visible = visible;
        }

        if (_siteMinimapHost != null)
        {
            _siteMinimapHost.Visible = visible;
        }

        if (_battlePreparationObjectiveThumbnailDock != null)
        {
            _battlePreparationObjectiveThumbnailDock.Visible = visible;
        }

        if (!visible)
        {
            SetBattlePreparationHudRetreated(false, "battle_preparation_hidden");
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
            return;
        }
        _battlePreparationHudRetreatTween?.Kill();
        _battlePreparationHudRetreatTween = null;
        Control[] controls =
        {
            _battlePreparationRosterDock,
            _battlePreparationPlanBar,
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

}
