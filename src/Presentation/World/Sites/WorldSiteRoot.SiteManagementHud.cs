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
        _siteBottomCommandHost = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "BottomCommandHost",
            nameof(WorldSiteRoot));
        _siteMinimapHost = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "MinimapHost",
            nameof(WorldSiteRoot));
        _siteModalHost = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "ModalHost",
            nameof(WorldSiteRoot));
        _battleRuntimeCommandBar = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroFrame = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroSelectorPresenter = new BattleRuntimeHeroSelectorPresenter(GameUiSceneFactory.GetRequiredNode<HBoxContainer>(_siteHudRoot, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroSelectorList", nameof(WorldSiteRoot)), SelectBattleRuntimeCommandGroup);
        _battleRuntimeHeroNameLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroNameLabel",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroStateLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroStateLabel",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroHealthBar = GameUiSceneFactory.GetRequiredNode<ProgressBar>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroHealthBar",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroManaBar = GameUiSceneFactory.GetRequiredNode<ProgressBar>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroManaBar",
            nameof(WorldSiteRoot));
        _battleRuntimeHeroSkillList = GameUiSceneFactory.GetRequiredNode<HBoxContainer>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroSkillList",
            nameof(WorldSiteRoot));
        _battleRuntimeRegroupButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeRegroupButton",
            nameof(WorldSiteRoot));
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
        _siteOverviewCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard",
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
        _battlePreparationRosterDock = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationRosterDock",
            nameof(WorldSiteRoot));
        _battlePreparationRosterList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationRosterDock/RosterMargin/BattlePreparationRosterList",
            nameof(WorldSiteRoot));
        _battlePreparationPlanBar = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar",
            nameof(WorldSiteRoot));
        _battlePreparationCompanyLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationCompanyLabel",
            nameof(WorldSiteRoot));
        _battlePreparationObjectiveLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationObjectiveLabel",
            nameof(WorldSiteRoot));
        _battlePreparationRuleButtonRow = GameUiSceneFactory.GetRequiredNode<HBoxContainer>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow",
            nameof(WorldSiteRoot));
        _battlePreparationMoveFirstButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/MoveFirstRuleButton",
            nameof(WorldSiteRoot));
        _battlePreparationAttackFirstButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/AttackFirstRuleButton",
            nameof(WorldSiteRoot));
        _battlePreparationHoldButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/HoldRuleButton",
            nameof(WorldSiteRoot));
        _battlePreparationStartButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationStartButton",
            nameof(WorldSiteRoot));
        _battlePreparationObjectiveThumbnailDock = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "MinimapHost/BattlePreparationObjectiveThumbnailDock",
            nameof(WorldSiteRoot));
        _battlePreparationObjectiveThumbnail = GameUiSceneFactory.GetRequiredNode<BattlePreparationObjectiveThumbnail>(
            _siteHudRoot,
            "MinimapHost/BattlePreparationObjectiveThumbnailDock/BattlePreparationObjectiveThumbnail",
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
        _siteActionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteActionList",
            nameof(WorldSiteRoot));
        _strategicManagementDashboardPanelBinder = new StrategicManagementDashboardPanelBinder(
            _siteResourceLabel,
            _siteHudBody,
            _siteSelectionLabel,
            _siteFacilityList,
            _siteFacilityBuildCard,
            _siteFacilityBuildTitle,
            _siteFacilityBuildList,
            _siteGarrisonList,
            _siteActionList,
            OnStrategicBuildFacilityPressed,
            OnStrategicCreateCorpsPressed,
            OnStrategicHeroAssignmentPressed);
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
            operationHintLabel.Text = "场域经营：点击建筑点管理；点击地图格设置移动意图。";
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
        _selectedFacilitySlotId = "";
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

        bool shouldShow = _siteHudRoot?.Visible == true &&
                          (_battleRuntimeCommandPauseActive ||
                           !string.IsNullOrWhiteSpace(_selectedFacilitySlotId));
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
            EnableBattlePerceptionOverlayForRuntime();
        }

        if (enabled && _siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            if (_siteHudTopBar != null)
            {
                _siteHudTopBar.Visible = false;
            }

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

    private void RefreshSiteManagementUi(string notice = "", BattleOutcome outcome = BattleOutcome.None)
    {
        BindSiteManagementPanel(notice, outcome);
    }

    private void OnStrategicBuildFacilityPressed(string facilityDefinitionId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.BuildFacility(
            StrategicManagementRuntime.State,
            cityId,
            facilityDefinitionId);
        HandleStrategicManagementCommandResult("建设设施", result);
    }

    private void OnStrategicCreateCorpsPressed(string corpsDefinitionId)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicCommandResult result = StrategicManagementRuntime.Commands.CreateCorps(
            StrategicManagementRuntime.State,
            cityId,
            corpsDefinitionId);
        HandleStrategicManagementCommandResult("创建编制", result);
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
        if (_siteHudTopBar != null)
        {
            _siteHudTopBar.Visible = true;
        }

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
        _deploymentService.EnsureGarrisonPlacements(site, definition);
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
            _siteHudTopBar,
            _battlePreparationRosterDock,
            _battlePreparationPlanBar,
            _battlePreparationObjectiveThumbnailDock
        };
        Vector2[] offsets =
        {
            new(0.0f, -88.0f),
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
