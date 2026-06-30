using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Maps;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Infrastructure.Scenes;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.Debug;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot : Control, IBattleMapBoundsSource
{
	private const float SitePlacementPickRadiusPixels = 42.0f;
	private const int DeploymentDragZIndex = 4096;

	[Signal]
	public delegate void SiteMapLoadedEventHandler(Node activeSiteMap);

	[Export]
	public NodePath MainWorldViewportHostPath { get; set; } = new("MainWorldViewportHost");

	[Export]
	public NodePath MainWorldViewportPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport");

	[Export]
	public NodePath MapRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/MapRoot");

	[Export]
	public NodePath CityBuildingRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/CityBuildingRoot");

	[Export]
	public NodePath UnitRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/UnitRoot");

	[Export]
	public NodePath HighlightOverlayPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/OverlayRoot/GridHighlightOverlay");

	[Export]
	public NodePath DeploymentZoneOverlayPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/OverlayRoot/DeploymentZoneOverlay");

	[Export]
	public NodePath StrategicBuildingPlacementPreviewPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/OverlayRoot/StrategicBuildingPlacementPreview");

	[Export]
	public NodePath SelectionVignetteOverlayPath { get; set; } = new("CanvasLayer/SelectionVignetteOverlay");

	[Export]
	public NodePath BattleCameraPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/Camera2D");

	[Export]
	public PackedScene SiteMapScene { get; set; }

	[Export]
	public PackedScene FieldInterceptMapScene { get; set; }

	[Export]
	public PackedScene StrategicCityBuildingMapEntityScene { get; set; }

	private Node _mapRoot;
	private Control _mainWorldViewportHost;
	private SubViewport _mainWorldViewport;
	private Node2D _cityBuildingRoot;
	private BattleUnitRoot _unitRoot;
	private Node _activeSiteMap;
	private BattleGridMap _activeGridMap;
	private BattleMapLayer _coordinateLayer;
	private BattleGridHighlightOverlay _highlightOverlay;
	private BattleDeploymentZoneOverlay _deploymentZoneOverlay;
	private StrategicBuildingPlacementPreview _strategicBuildingPlacementPreview;
	private BattleSelectionVignetteOverlay _selectionVignetteOverlay;
	private BattleCameraController _battleCamera;
	private Control _siteHudRoot;
	private Control _sitePeacetimePanel;
	private Control _siteModalHost;
	private Control _siteBottomCommandHost;
	private Control _battleRuntimeCommandBar;
	private Control _battleRuntimeHeroFrame;
	private Node2D _sitePlacementEntityRoot;
	private Label _siteHudTitle;
	private Label _siteHudBody;
	private Label _siteResourceLabel;
	private Label _siteNoticeLabel;
	private Label _siteSelectionLabel;
	private Label _battleRuntimeHeroNameLabel;
	private Label _battleRuntimeHeroStateLabel;
	private ProgressBar _battleRuntimeHeroHealthBar;
	private ProgressBar _battleRuntimeHeroManaBar;
	private HBoxContainer _battleRuntimeHeroSkillList;
	private BattleRuntimeHeroSelectorPresenter _battleRuntimeHeroSelectorPresenter;
	private BattleRuntimeHeroFramePresenter _battleRuntimeHeroFramePresenter;
	private Button _battleRuntimeRegroupButton;
	private Button _siteBuildTabButton;
	private Button _siteConscriptionTabButton;
	private Button _siteRecruitTabButton;
	private Button _siteCorpsTabButton;
	private Button _siteOverviewTabButton;
	private Control _siteBuildSection;
	private Control _siteConscriptionSection;
	private Control _siteCorpsSection;
	private Control _siteOverviewSection;
	private Control _militaryWorkbenchPanel;
	private VBoxContainer _militaryHeroList;
	private GridContainer _militaryMusterGrid;
	private Label _militaryHeroSummaryLabel;
	private Label _militaryNoticeLabel;
	private Button _militaryBackButton;
	private Button _militaryCloseButton;
	private Label _siteBuildingBuildTitle;
	private GridContainer _siteBuildingOptionGrid;
	private VBoxContainer _siteConscriptionList;
	private Control _siteMinimapHost;
	private Control _battlePreparationRosterDock;
	private VBoxContainer _battlePreparationRosterList;
	private Control _battlePreparationPlanBar;
	private Label _battlePreparationCompanyLabel;
	private Label _battlePreparationObjectiveLabel;
	private HBoxContainer _battlePreparationRuleButtonRow;
	private Button _battlePreparationMoveFirstButton;
	private Button _battlePreparationAttackFirstButton;
	private Button _battlePreparationHoldButton;
	private Button _battlePreparationStartButton;
	private Control _battlePreparationObjectiveThumbnailDock;
	private BattlePreparationObjectiveThumbnail _battlePreparationObjectiveThumbnail;
	private Button _returnMapButton;
	private BattleObjectiveMapDialog _battleObjectiveMapDialog;
	private PostBattleSettlementDialog _postBattleSettlementDialog;
	private VBoxContainer _siteBuildingList;
	private VBoxContainer _siteGarrisonList;
	private StrategicManagementDashboardPanelBinder _strategicManagementDashboardPanelBinder;
	private StrategicMilitaryWorkbenchBinder _strategicMilitaryWorkbenchBinder;
	private readonly BattleObjectivePlanningHudBinder _battleObjectivePlanningHudBinder = new();
	private readonly BattlePreparationHudBinder _battlePreparationHudBinder = new();
	private readonly Dictionary<string, StrategicCityBuildingMapEntity> _cityBuildingEntities = new();
	private readonly Dictionary<string, Node2D> _sitePlacementEntities = new();
	private SemanticMapMarkerExtractionResult _semanticMapMarkers = new();
	private WorldSiteRuntimeDeploymentCache _deploymentCache;
	private bool _battleRuntimeEnabled = true;
	private bool _battleRuntimeCommandPauseActive;
	private bool _battleRuntimeScenePauseApplied;
	private bool _battleRuntimeTreeWasPausedBeforeTacticalPause;
	private string _battleStartBlockedReason = "";
	private bool _isBattlePreparationActive;
	private BattleStartRequest _battlePreparationRequest;
	private BattleStartRequest _battleRuntimeRequest;
	private StrategicBattleActiveContext _activeStrategicBattleContext;
	private WorldSiteBattleGroupRuntimeResolveResult _activeBattleGroupRuntimeResolution;
	private string _siteHudReturnScenePath = "";
	private string _siteHudSiteId = "";
	private string _postBattleSettlementReturnScenePath = "";
	private string _postBattleSettlementSiteId = "";
	private BattleOutcome _postBattleSettlementOutcome = BattleOutcome.None;
	private BattleStartRequest _postBattleSettlementRequest;
	private WorldActionResult _postBattleSettlementApplyResult;
	private bool _postBattleSettlementDialogOpen;
	private string _selectedPlacementId = "";
	private string _selectedStrategicBuildingDefinitionId = "";
	private string _selectedMilitaryWorkbenchHeroId = "";
	private BattleCorpsCommand _selectedBattleCorpsCommand = BattleCorpsCommand.Assault;
	private string _selectedBattleRuntimeGroupKey = "";
	private SiteManagementSection _selectedSiteManagementSection = SiteManagementSection.Build;
	private bool _battleRuntimeHeroSkillTargetPickingActive;
	private BattleRuntimeCommandGroupView _battleRuntimeHeroSkillTargetPickingGroup;
	private string _battleRuntimeHeroSkillTargetPickingSkillId = "";
	private string _battleRuntimeHeroSkillPreviewTargetActorId = "";
	private ThunderFoldTargetingStage _battleRuntimeThunderFoldTargetingStage = ThunderFoldTargetingStage.None;
	private string _battleRuntimeThunderFoldSelectedMarkId = "";
	private GridSurfacePosition _battleRuntimeThunderFoldSelectedMarkSurface;
	private string _selectedBattlePreparationPlanGroupKey = "";
	private readonly HashSet<string> _explicitBattlePreparationRuleGroups = new(System.StringComparer.Ordinal);
	private readonly Dictionary<Node, ProcessModeEnum> _battleRuntimePauseProcessModeRestore = new();
	private string _draggedPlacementId = "";
	private Vector2 _draggedPlacementOriginGlobalPosition;
	private string _draggedBattlePreparationGroupKey = "";
	private readonly List<BattlePreparationCompanyPreviewEntity> _draggedBattlePreparationCompanyEntities = new();
	private readonly List<BattlePreparationCompanyPlacementSnapshot> _draggedBattlePreparationPreviousPlacements = new();
	private BattlePreparationCompanyFormationDraft _draggedBattlePreparationDraft;
	private bool _lastBattlePreparationCompanyDragValid;
	private string _lastBattlePreparationCompanyDragReason = "";
	private Tween _battlePreparationHudRetreatTween;
	private bool _battlePreparationHudRetreated;
	private readonly Dictionary<Control, Vector2> _battlePreparationHudRestPositions = new();
	private readonly BattleUnitFactory _battleUnitFactory = new();
	private readonly WorldBattleResultApplier _worldBattleResultApplier = new();
	private readonly WorldArmyCommandService _armyCommandService = new();
	private readonly WorldSiteDeploymentService _deploymentService = new();
	private readonly WorldSiteRuntimeDeploymentCacheBuilder _deploymentCacheBuilder = new();
	private readonly WorldSiteDeploymentTargetEvaluator _deploymentTargetEvaluator = new();
	private readonly WorldSiteDeploymentTerrainReconciler _deploymentTerrainReconciler = new();
	private readonly WorldSiteBattleDeploymentPreparer _battleDeploymentPreparer = new();
	private readonly BattlePreparationCompanyFormationPlanner _battlePreparationCompanyFormationPlanner = new();
	private readonly WorldSiteBattleLauncher _battleLauncher = new();
	private readonly BattlePerformanceCounters _battlePerformanceCounters = new();
	private readonly WorldSiteBattleGroupRuntimeAdapter _battleGroupRuntimeAdapter;
	private readonly BattleRuntimeLivePresentationObserver _battleRuntimeLivePresentationObserver;
	private readonly BattlePreparationDeploymentDragController _battlePreparationDeploymentDragController;
	private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
	private readonly SemanticMapMarkerExtractor _semanticMapMarkerExtractor = new();
	private readonly StrategicBuildingPlacementResolver _strategicBuildingPlacementResolver = new();
	private readonly SceneTransitionRouter _sceneTransitionRouter;

	public Node ActiveSiteMap => _activeSiteMap;
	public Node ActiveBattleMap => _activeSiteMap;
	public BattleGridMap ActiveGridMap => _activeGridMap;
	public BattleEntity SelectedEntity => null;
	public bool AllowsDebugHoverInfo => RuntimeMode != WorldSiteRuntimeMode.Battle;
	public bool IsEnemyPhaseRunning => false;
	public WorldSiteRuntimeMode RuntimeMode => ResolveRuntimeMode();
	public event System.Action<Node> BattleMapLoaded;

	public WorldSiteRoot()
	{
		_battleGroupRuntimeAdapter = new(_battlePerformanceCounters);
		_battleRuntimeLivePresentationObserver = new(
			() => _unitRoot,
			WaitSiteBattlePresentationSeconds,
			(entity, force) => _battleCamera?.FollowActionEntityIfNeeded(entity, force),
			QueueBattlePerceptionOverlayRefresh,
			_battlePerformanceCounters.RecordPresentationObserveElapsedTicks);
		_sceneTransitionRouter = new SceneTransitionRouter(new GodotSceneTransitionGateway(() => GetTree()));
		_battlePreparationDeploymentDragController = new BattlePreparationDeploymentDragController(
			() => _isBattlePreparationActive,
			() => _battlePreparationRequest,
			() => _activeGridMap,
			_deploymentTargetEvaluator,
			ResolveForceFootprintSize,
			ResolveForceCanEnterWater,
			ResolveUnitFootprintSize,
			ResolvePlacementCanEnterWater,
			ResolveBattleFaction,
			SetDeploymentDragEntityToFootprintCenter,
			ResolveEntitySurfaceHeight,
			ApplyEntityRenderSort);
	}

	private bool TryResolveActiveBattleContext(out StrategicBattleActiveContext activeContext)
	{
		if (_activeStrategicBattleContext != null)
		{
			activeContext = _activeStrategicBattleContext;
			return true;
		}

		if (StrategicBattleActiveContextStore.TryPeek(out activeContext))
		{
			_activeStrategicBattleContext = activeContext;
			return true;
		}

		activeContext = null;
		return false;
	}

	private bool TryResolveActiveBattleRequest(out BattleStartRequest request)
	{
		if (TryResolveActiveBattleContext(out StrategicBattleActiveContext activeContext) &&
			activeContext.CompatibilityRequest != null)
		{
			request = activeContext.CompatibilityRequest;
			return true;
		}

		return TryPeekLegacyNonStrategicBattleRequest(out request);
	}

	private static bool TryPeekLegacyNonStrategicBattleRequest(out BattleStartRequest request)
	{
		if (!BattleSessionHandoff.TryPeekActiveRequest(out request))
		{
			return false;
		}

		if (!IsStrategicBattleRequest(request))
		{
			return true;
		}

		// Strategic battle requests are valid only through StrategicBattleActiveContext.
		// A lone legacy handoff here means a previous bridge transition leaked state.
		ClearLegacyStrategicBattleHandoff("stale_strategic_legacy_request");
		request = null;
		return false;
	}

	private static bool IsStrategicBattleRequest(BattleStartRequest request)
	{
		return request != null &&
			   (!string.IsNullOrWhiteSpace(request.StrategicBattleSessionId) ||
				!string.IsNullOrWhiteSpace(request.StrategicExpeditionId) ||
				!string.IsNullOrWhiteSpace(request.StrategicSourceLocationId) ||
				!string.IsNullOrWhiteSpace(request.StrategicTargetLocationId));
	}

	private static void ClearLegacyStrategicBattleHandoff(string reason)
	{
		if (!BattleSessionHandoff.HasActiveLaunch)
		{
			return;
		}

		BattleSessionHandoff.CancelBattle();
		GameLog.Warn(
			nameof(WorldSiteRoot),
			$"StaleLegacyStrategicBattleHandoffCleared reason={reason ?? ""}");
	}

	private bool HasActiveBattleLaunch()
	{
		return TryResolveActiveBattleRequest(out _);
	}

	private void CancelActiveBattleLaunch(string reason)
	{
		if (_activeStrategicBattleContext != null || StrategicBattleActiveContextStore.HasActiveContext)
		{
			StrategicBattleActiveContextStore.Clear(reason);
			_activeStrategicBattleContext = null;
			ClearLegacyStrategicBattleHandoff(reason);
			return;
		}

		BattleSessionHandoff.CancelBattle();
	}

	public override void _Ready()
	{
		GameLog.StartSession(nameof(WorldSiteRoot));
		GameUiSkin.ApplyGameCursorTheme();
		BattlePerformanceMonitorRegistry.Register(
			_battlePerformanceCounters,
			() => _unitRoot?.ActiveMovementTweenCount ?? 0);
		MouseFilter = MouseFilterEnum.Stop;
		SetFullRect(this);

		ResolveMainWorldViewportNodes();
		_mapRoot = GetNode<Node>(MapRootPath);
		_cityBuildingRoot = GetNodeOrNull<Node2D>(CityBuildingRootPath);
		_unitRoot = GetNodeOrNull<BattleUnitRoot>(UnitRootPath);
		_highlightOverlay = GetNodeOrNull<BattleGridHighlightOverlay>(HighlightOverlayPath);
		_deploymentZoneOverlay = GetNodeOrNull<BattleDeploymentZoneOverlay>(DeploymentZoneOverlayPath);
		_strategicBuildingPlacementPreview = GetNodeOrNull<StrategicBuildingPlacementPreview>(StrategicBuildingPlacementPreviewPath);
		_selectionVignetteOverlay = GetNodeOrNull<BattleSelectionVignetteOverlay>(SelectionVignetteOverlayPath);
		_battleCamera = GetNodeOrNull<BattleCameraController>(BattleCameraPath);
		GetViewport().SizeChanged += OnViewportSizeChanged;
		BuildSiteHud();
		UpdateMainWorldViewportLayout("ready");
		EnsureBattleRenderSortDomain();

		bool hasActiveBattleLaunch = HasActiveBattleLaunch();
		if (_unitRoot != null)
		{
			_unitRoot.Initialize(TryGetCellGlobalPosition, TryGetFootprintCenterGlobalPosition, ApplyEntityRenderSort);
		}
		else
		{
			GameLog.Warn(nameof(WorldSiteRoot), $"Unit root missing or missing BattleUnitRoot script path={UnitRootPath}");
		}

		GameLog.Info(
			nameof(WorldSiteRoot),
			$"Ready mapRoot={_mapRoot?.GetPath()} cityBuildingRoot={_cityBuildingRoot?.GetPath()} unitRoot={_unitRoot?.GetPath()} highlight={_highlightOverlay != null}");

		LoadConfiguredSiteMap();
		_isBattlePreparationActive = hasActiveBattleLaunch;
		ApplyBattleStartRequest();

		if (hasActiveBattleLaunch && string.IsNullOrWhiteSpace(_battleStartBlockedReason))
		{
			EnterBattlePreparation();
		}
		else if (hasActiveBattleLaunch)
		{
			_isBattlePreparationActive = false;
			_battlePreparationRequest = null;
			CancelActiveBattleLaunch(_battleStartBlockedReason);
			StrategicWorldRuntime.LastNotice = _battleStartBlockedReason;
			SwitchToNonBattleUi(BattleOutcome.None, null, null, _battleStartBlockedReason);
		}
		else
		{
			SwitchToNonBattleUi(BattleOutcome.None, null, null, "");
		}
	}

	public override void _ExitTree()
	{
		SetBattleRuntimeCommandPauseActive(false, "exit_tree");
		BattlePerformanceMonitorRegistry.Unregister();
		if (GetViewport() != null)
		{
			GetViewport().SizeChanged -= OnViewportSizeChanged;
		}
	}

	public override void _Process(double delta)
	{
		WorldSiteRuntimeMode runtimeMode = RuntimeMode;
		if (runtimeMode == WorldSiteRuntimeMode.Management)
		{
			UpdateSiteMapEntities();
			return;
		}
	}

	public override void _Input(InputEvent @event)
	{
		WorldSiteRuntimeMode runtimeMode = RuntimeMode;
		if (runtimeMode == WorldSiteRuntimeMode.Battle)
		{
			if (TryHandleBattlePerceptionOverlayInput(@event))
			{
				return;
			}

			if (TryHandleBattleRuntimeHeroSkillTargetInput(@event))
			{
				return;
			}

			if (TryHandleBattleRuntimePauseInput(@event))
			{
				return;
			}

			return;
		}

		if (TryHandleStrategicBuildingPlacementInput(@event))
		{
			return;
		}

		if (TryHandleSiteContextClearInput(@event))
		{
			return;
		}

		HandleSiteDeploymentDragInput(@event);
	}

	private WorldSiteRuntimeMode ResolveRuntimeMode()
	{
		if (_battleRuntimeEnabled)
		{
			return WorldSiteRuntimeMode.Battle;
		}

		return WorldSiteRuntimeMode.Management;
	}

	private bool IsBattleRuntimeHudActive()
	{
		return _battleRuntimeEnabled &&
			!_isBattlePreparationActive &&
			(_battleRuntimeRequest != null || _activeBattleGroupRuntimeResolution != null);
	}

	private void ResolveMainWorldViewportNodes()
	{
		_mainWorldViewportHost = GetNodeOrNull<Control>(MainWorldViewportHostPath);
		_mainWorldViewport = GetNodeOrNull<SubViewport>(MainWorldViewportPath);
		if (_mainWorldViewportHost == null || _mainWorldViewport == null)
		{
			GameLog.Error(
				nameof(WorldSiteRoot),
				$"SiteMainWorldViewportMissing host={_mainWorldViewportHost != null} viewport={_mainWorldViewport != null}");
			return;
		}

		_mainWorldViewportHost.MouseFilter = Control.MouseFilterEnum.Pass;
		_mainWorldViewportHost.ClipContents = true;
	}

	private void SetBattleRuntimeCommandPauseActive(bool paused, string reason)
	{
		if (_battleRuntimeCommandPauseActive == paused && _battleRuntimeScenePauseApplied == paused)
		{
			return;
		}

		_battleRuntimeCommandPauseActive = paused;
		if (paused)
		{
			EnsureSelectedBattleRuntimeCommandGroup();
		}
		else
		{
			CancelBattleRuntimeHeroSkillTargetPicking("pause_off");
			_unitRoot?.ClearCommandSelection();
		}

		ApplyBattleRuntimeScenePause(paused, reason);
		RefreshBattleRuntimeCommandPausePresentation();
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeCommandPauseToggled paused={_battleRuntimeCommandPauseActive} selectedGroup={_selectedBattleRuntimeGroupKey} reason={reason ?? ""}");
	}

	private void ApplyBattleRuntimeScenePause(bool paused, string reason)
	{
		if (paused)
		{
			if (!_battleRuntimeScenePauseApplied)
			{
				_battleRuntimeTreeWasPausedBeforeTacticalPause = IsInsideTree() && GetTree().Paused;
				CaptureBattleRuntimePauseProcessMode(this, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_siteHudRoot, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_siteModalHost, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_mainWorldViewportHost, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_mainWorldViewport, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_battleCamera, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_highlightOverlay, ProcessModeEnum.Always);
				CaptureBattleRuntimePauseProcessMode(_mapRoot, ProcessModeEnum.Pausable);
				CaptureBattleRuntimePauseProcessMode(_activeSiteMap, ProcessModeEnum.Pausable);
				CaptureBattleRuntimePauseProcessMode(_unitRoot, ProcessModeEnum.Pausable);
				CaptureBattleRuntimePauseProcessMode(_sitePlacementEntityRoot, ProcessModeEnum.Pausable);
				_battleRuntimeScenePauseApplied = true;
			}

			_unitRoot?.SetBattlePresentationPaused(paused);
			_battleCamera?.SetTacticalPauseActive(paused);
			_highlightOverlay?.SetTacticalPauseVisualsStatic(paused);
			if (IsInsideTree())
			{
				// Tactical pause freezes the world through Godot while command UI keeps running.
				GetTree().Paused = paused;
			}
		}
		else
		{
			if (!_battleRuntimeScenePauseApplied)
			{
				return;
			}

			_battleCamera?.SetTacticalPauseActive(paused);
			_highlightOverlay?.SetTacticalPauseVisualsStatic(paused);
			RestoreBattleRuntimePauseProcessModes();
			if (IsInsideTree())
			{
				GetTree().Paused = _battleRuntimeTreeWasPausedBeforeTacticalPause;
			}

			_unitRoot?.SetBattlePresentationPaused(paused);
			_battleRuntimeScenePauseApplied = false;
			_battleRuntimeTreeWasPausedBeforeTacticalPause = false;
		}

		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeScenePauseApplied paused={paused} reason={reason ?? ""}");
	}

	private void CaptureBattleRuntimePauseProcessMode(Node node, ProcessModeEnum processMode)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}

		if (!_battleRuntimePauseProcessModeRestore.ContainsKey(node))
		{
			_battleRuntimePauseProcessModeRestore[node] = node.ProcessMode;
		}

		node.ProcessMode = processMode;
	}

	private void RestoreBattleRuntimePauseProcessModes()
	{
		foreach ((Node node, ProcessModeEnum processMode) in _battleRuntimePauseProcessModeRestore.ToArray())
		{
			if (node != null && GodotObject.IsInstanceValid(node))
			{
				node.ProcessMode = processMode;
			}
		}

		_battleRuntimePauseProcessModeRestore.Clear();
	}

	private void UpdateMainWorldViewportLayout(string reason)
	{
		if (_mainWorldViewportHost == null || _mainWorldViewport == null)
		{
			return;
		}

		Rect2 worldViewportRect = ResolveMainWorldViewportRect();
		bool reserveUiWorkspace = ShouldReserveSiteHudWorkspace();

		// The site/battle map is a separate world viewport. HUD and modal UI stay on
		// the outer CanvasLayer, so root mouse positions must cross this boundary.
		SetFixedRect(_mainWorldViewportHost, worldViewportRect);
		Vector2I viewportSize = new(Mathf.RoundToInt(worldViewportRect.Size.X), Mathf.RoundToInt(worldViewportRect.Size.Y));
		if (_mainWorldViewport.Size != viewportSize)
		{
			_mainWorldViewport.Size = viewportSize;
		}

		_battleCamera?.SetViewportSizeOverride(worldViewportRect.Size);
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"SiteMainWorldViewportLayoutApplied reason={reason} reserveUi={reserveUiWorkspace} position={_mainWorldViewportHost.Position} size={worldViewportRect.Size}");
	}

	private Rect2 ResolveMainWorldViewportRect()
	{
		Vector2 rootViewportSize = GetViewportRect().Size;
		if (IsBattleRuntimeHudActive())
		{
			return new Rect2(Vector2.Zero, rootViewportSize);
		}

		if (_isBattlePreparationActive && !_battleRuntimeEnabled)
		{
			return new Rect2(Vector2.Zero, rootViewportSize);
		}

		if (ShouldReserveSiteHudWorkspace())
		{
			return ResolveWorldSiteHudViewportRect(rootViewportSize);
		}

		return ResolveAuthoredMainWorldViewportRect(rootViewportSize);
	}

	private bool ShouldReserveSiteHudWorkspace()
	{
		return _siteHudRoot?.Visible == true &&
		       !_postBattleSettlementDialogOpen &&
		       !IsBattleRuntimeHudActive() &&
		       !(_isBattlePreparationActive && !_battleRuntimeEnabled);
	}

	private Rect2 ResolveWorldSiteHudViewportRect(Vector2 rootViewportSize)
	{
		Rect2 authoredWorldRect = ResolveAuthoredMainWorldViewportRect(rootViewportSize);
		if (_sitePeacetimePanel == null)
		{
			return authoredWorldRect;
		}

		Rect2 panelRect = _sitePeacetimePanel.GetGlobalRect();
		if (panelRect.Size.X <= 1.0f || panelRect.Size.Y <= 1.0f)
		{
			return authoredWorldRect;
		}

		Vector2 position = new(Mathf.Clamp(panelRect.End.X, 0.0f, rootViewportSize.X - 1.0f), 0.0f);
		float bottomLimit = rootViewportSize.Y;

		if (_battleRuntimeCommandBar?.Visible == true)
		{
			Rect2 commandRect = _battleRuntimeCommandBar.GetGlobalRect();
			if (commandRect.Size.Y > 1.0f && commandRect.Position.Y > position.Y)
			{
				bottomLimit = Mathf.Min(bottomLimit, commandRect.Position.Y);
			}
		}

		// Site management uses a no-gutter split screen: UI owns the left strip and
		// the map viewport owns the full remaining right-side rectangle.
		return new Rect2(
			position,
			new Vector2(
				Mathf.Max(1.0f, rootViewportSize.X - position.X),
				Mathf.Max(1.0f, bottomLimit - position.Y)));
	}

	private Rect2 ResolveAuthoredMainWorldViewportRect(Vector2 rootViewportSize)
	{
		Rect2 hostRect = _mainWorldViewportHost?.GetGlobalRect() ?? new Rect2(Vector2.Zero, rootViewportSize);
		return hostRect.Size.X > 1.0f && hostRect.Size.Y > 1.0f
			? hostRect
			: new Rect2(Vector2.Zero, rootViewportSize);
	}

	private static void SetFullRect(Control control)
	{
		if (control == null)
		{
			return;
		}

		control.AnchorLeft = 0.0f;
		control.AnchorTop = 0.0f;
		control.AnchorRight = 1.0f;
		control.AnchorBottom = 1.0f;
		control.OffsetLeft = 0.0f;
		control.OffsetTop = 0.0f;
		control.OffsetRight = 0.0f;
		control.OffsetBottom = 0.0f;
	}

	private static void SetFixedRect(Control control, Rect2 rect)
	{
		if (control == null)
		{
			return;
		}

		control.AnchorLeft = 0.0f;
		control.AnchorTop = 0.0f;
		control.AnchorRight = 0.0f;
		control.AnchorBottom = 0.0f;
		control.OffsetLeft = rect.Position.X;
		control.OffsetTop = rect.Position.Y;
		control.OffsetRight = rect.End.X;
		control.OffsetBottom = rect.End.Y;
	}

	private Vector2 GetWorldViewportMousePosition()
	{
		return WorldViewportLocalToWorld(ToWorldViewportLocal(GetGlobalMousePosition()));
	}

	private Vector2 ToWorldViewportLocal(Vector2 rootGlobalPosition)
	{
		return rootGlobalPosition - (_mainWorldViewportHost?.GlobalPosition ?? Vector2.Zero);
	}

	private Vector2 WorldViewportLocalToWorld(Vector2 viewportLocalPosition)
	{
		if (_mainWorldViewport == null)
		{
			return viewportLocalPosition;
		}

		// Site input originates outside MainWorldViewport; drag/drop needs the
		// SubViewport world coordinate after Camera2D pan and zoom are applied.
		return _mainWorldViewport.GetCanvasTransform().AffineInverse() * viewportLocalPosition;
	}

	public void LoadConfiguredSiteMap()
	{
		PackedScene mapScene = ResolveConfiguredSiteMapScene();
		if (mapScene == null)
		{
			GameLog.Warn(nameof(WorldSiteRoot), "No site map scene configured.");
			GD.PushWarning("WorldSiteRoot has no site map scene configured.");
			return;
		}

		LoadSiteMap(mapScene);
	}

	private PackedScene ResolveConfiguredSiteMapScene()
	{
		if (!TryResolveActiveBattleRequest(out BattleStartRequest request))
		{
			return SiteMapScene;
		}

		if (request.MapDefinitionId == "field_intercept_v1" && FieldInterceptMapScene != null)
		{
			return FieldInterceptMapScene;
		}

		return SiteMapScene;
	}

	public void LoadSiteMap(PackedScene mapScene)
	{
		_activeSiteMap?.QueueFree();
		_activeGridMap = null;
		_coordinateLayer = null;

		_activeSiteMap = mapScene.Instantiate<Node>();
		EnsureActiveSiteMapRenderSortDomain();
		_mapRoot.AddChild(_activeSiteMap);
		GameLog.Info(nameof(WorldSiteRoot), $"Loaded site map scene={mapScene.ResourcePath} activeSiteMap={_activeSiteMap.GetPath()}");

		if (_activeSiteMap is BattleMapView battleMapView)
		{
			battleMapView.EnsureRuntimeData();
			_activeGridMap = battleMapView.GridMap;
			_coordinateLayer = battleMapView.CoordinateLayer;
			GameLog.Info(nameof(WorldSiteRoot), $"Map runtime ready={battleMapView.RuntimeDataReady}");
		}
		else
		{
			GameLog.Warn(nameof(WorldSiteRoot), "Loaded site map is not a BattleMapView; grid map is unavailable.");
		}

		ExtractSemanticMapMarkers(ResolveActiveWorldSiteId());
		RebuildSiteDeploymentRuntimeCache(ResolveActiveWorldSiteId());
		EmitSignal(SignalName.SiteMapLoaded, _activeSiteMap);
		BattleMapLoaded?.Invoke(_activeSiteMap);

		PlaceBattleEntitiesOnGrid();
	}

	private void EnsureActiveSiteMapRenderSortDomain()
	{
		if (_activeSiteMap is CanvasItem activeSiteMapItem)
		{
			activeSiteMapItem.YSortEnabled = true;
		}
	}

	private void RegisterBattleEntities()
	{
		if (_unitRoot == null)
		{
			GameLog.Warn(nameof(WorldSiteRoot), "Cannot register battle entities because UnitRoot is missing.");
			return;
		}

		int count = _unitRoot.GetEntitiesSnapshot().Count;
		GameLog.Info(nameof(WorldSiteRoot), $"Registered battle entities count={count}");
	}

	private string ResolveActiveWorldSiteId()
	{
		if (TryResolveActiveBattleRequest(out BattleStartRequest request) &&
			!string.IsNullOrWhiteSpace(request.TargetSiteId))
		{
			return request.TargetSiteId;
		}

		if (!string.IsNullOrWhiteSpace(_siteHudSiteId))
		{
			return _siteHudSiteId;
		}

		return StrategicWorldRuntime.Definition?.StartingSiteId ?? "";
	}

	private void RebuildSiteDeploymentRuntimeCache(string siteId)
	{
		SemanticMapMarkerData[] deploymentZoneMarkers = ResolveSemanticDeploymentZoneMarkers();
		_deploymentCache = _deploymentCacheBuilder.Build(siteId, _activeGridMap, deploymentZoneMarkers);
		if (_activeGridMap == null)
		{
			GameLog.Warn(nameof(WorldSiteRoot), $"Cannot build site deployment cache site={siteId} reason=grid_missing");
			return;
		}

		string counts = string.Join(
			" ",
			WorldSiteRuntimeDeploymentCacheBuilder.SupportedDirections
				.Select(direction => $"{direction}={_deploymentCache.GetCandidates(direction).Count}"));
		GameLog.Info(nameof(WorldSiteRoot), $"SiteDeploymentCacheBuilt site={siteId} surfaces={_deploymentCache.CandidateSurfaceCount} authoredZones={_deploymentCache.AuthoredDeploymentZoneSurfaceCount} {counts}");
	}

	public void ReturnToReturnScene(string scenePath)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			return;
		}

		SceneTransitionResult transition = _sceneTransitionRouter.ReturnFromSite(new SceneTransitionReturnRequest
		{
			TargetScenePath = scenePath,
			// The large-map scene entry is the Strategic Management resume boundary.
			OnEntered = StrategicManagementRuntime.ResumeWorldMapTime
		});
		if (!transition.Success)
		{
			GameLog.Warn(
				nameof(WorldSiteRoot),
				$"Cannot return to campaign scene path={scenePath} error={transition.Error} reason={transition.FailureReason}");
		}
	}

}
