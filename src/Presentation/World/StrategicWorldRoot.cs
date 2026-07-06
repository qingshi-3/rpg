using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Infrastructure.Scenes;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot : Control
{
	public static StrategicWorldRoot Current { get; private set; }

	private const float SiteIconRadius = 24.0f;
	private const float SiteVisualHitPadding = 10.0f;
	private const float SiteVisualLabelGap = 5.0f;
	private const float OpportunityMarkerRadius = 18.0f;
	private const float DefaultFogTexelWorldSize = StrategicFogOfWarService.DefaultFogTexelWorldSize;
	private const float DefaultSiteVisionRadius = 480.0f;
	private const float DefaultArmyVisionRadius = 260.0f;
	private const float SiteApproachVisualOffset = 8.0f;
	private const float SiteApproachEdgeNudge = 2.0f;
	private const float SiteNavigationPointSnapDistance = 96.0f;
	private const int SiteNavigationPointSearchCellRadius = 8;
	private const double DefaultWorldTickIntervalSeconds = 8.0;
	private const float SiteDetailPanelSlidePixels = 30.0f;
	private const float SiteDetailPanelOvershootPixels = 8.0f;
	private const double SiteDetailPanelEnterSeconds = 0.16;
	private const double SiteDetailPanelSettleSeconds = 0.10;
	private const double SiteDetailPanelExitSeconds = 0.14;
	// Deferring a forced battle should only step the army off the trigger point,
	// not send it into a retreat-like strategic move.
	private const float DeferredBattleStandbyDistance = 32.0f;
	// 战略行军只允许查询这一层；视觉层不能作为寻路兜底，否则会重新产生多权威路径问题。
	// 场景当前使用 StrategicNavigationLayer，保留旧名兼容老分支和临时场景。
	private const string StrategicNavigationLayerName = "StrategicNavigationLayer";
	private const string StrategicNavigationTileLayerName = "StrategicNavigationTileLayer";
	private const string StrategicNavigationRootName = "StrategicNavigationRoot";
	private static readonly Vector2 SiteButtonSize = new(132.0f, 106.0f);
	private static readonly Vector2 SiteLabelFallbackSize = new(SiteButtonSize.X + 28.0f, 44.0f);
	private static readonly double[] WorldClockSpeedMultipliers = { 1.0, 2.0, 4.0 };
	private static readonly Vector2I[] SiteVisualScanDirections =
	{
		new(-1, 0),
		new(1, 0),
		new(0, -1),
		new(0, 1)
	};

	[Export]
	public string SiteScenePath { get; set; } = "res://scenes/world/sites/WorldSiteRoot.tscn";

	[Export]
	public NodePath MainWorldViewportHostPath { get; set; } = new("MainWorldViewportHost");

	[Export]
	public NodePath MainWorldViewportPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport");

	[Export]
	public NodePath WorldMapOverlayPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapOverlay");

	[Export]
	public NodePath WorldMapRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapRoot");

	[Export]
	public NodePath WorldCameraPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldCamera");

	[Export]
	public NodePath SiteAnchorRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapRoot/MapAnchors/Sites");

	[Export]
	public NodePath SiteVisualLayerPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapRoot/SiteVisualLayer");

	[Export]
	public NodePath ArmySpawnPointRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapRoot/MapAnchors/ArmySpawnPoints");

	[Export]
	public NodePath EncounterZoneRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/WorldMapRoot/MapAnchors/EncounterZones");

	[Export]
	public bool AutoWorldClockEnabled { get; set; } = true;

	[Export]
	public double WorldTickIntervalSeconds { get; set; } = DefaultWorldTickIntervalSeconds;

	[ExportGroup("Fog Of War")]

	[Export]
	public bool FogOfWarEnabled { get; set; } = true;

	[Export]
	public float FogTexelWorldSize { get; set; } = DefaultFogTexelWorldSize;

	[Export]
	public float SiteVisionRadius { get; set; } = DefaultSiteVisionRadius;

	[Export]
	public float ArmyVisionRadius { get; set; } = DefaultArmyVisionRadius;

	private readonly WorldActionResolver _actionResolver;
	private readonly WorldBattleResultApplier _battleResultApplier = new();
	private readonly WorldBattleRequestBuilder _battleRequestBuilder = new();
	private readonly WorldArmyCommandService _armyCommandService = new();
	private readonly WorldArmyMovementService _armyMovementService = new();
	private readonly StrategicExpeditionWorldArmyAdapter _strategicExpeditionWorldArmyAdapter = new();
	private readonly WorldOpportunityService _opportunityService = new();
	private readonly WorldSiteDeploymentService _deploymentService = new();
	private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
	private readonly WorldTickService _worldTickService = new();
	private readonly BattleUnitFactory _battleUnitFactory = new();
	private readonly SceneTransitionRouter _sceneTransitionRouter;

	private readonly Dictionary<string, SiteVisualFootprint> _siteVisualFootprints = new();
	private readonly HashSet<string> _reportedSiteVisualFootprintFailures = new();
	private readonly HashSet<string> _reportedSiteNavigationPointResolutions = new();
	private Control _mainWorldViewportHost;
	private SubViewport _mainWorldViewport;
	private Control _worldMapOverlay;
	private Control _worldMapDynamicOverlay;
	private Control _worldSiteNameOverlay;
	private Control _strategicHudRoot;
	private Control _topBarHost;
	private Control _leftPrimaryPanelHost;
	private Control _modalHost;
	private Node2D _worldMapRoot;
	private MapCameraController _worldCamera;
	private Node2D _siteAnchorRoot;
	private Node2D _armySpawnPointRoot;
	private TileMapLayer _siteVisualLayer;
	private Node2D _strategicNavigationRoot;
	private TileMapLayer _strategicNavigationTileLayer;
	private StrategicWorldFogOverlay _fogOverlay;
	private StrategicNavigationContext _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable("strategic_navigation_not_configured");
	private Label _worldClockLabel;
	private Label _noticeLabel;
	private TextureButton _worldClockToggleButton;
	private TextureButton _worldClockSpeedButton;
	private Control _siteDetailPanel;
	private ScrollContainer _siteDetailBodyScroll;
	private Tween _siteDetailPanelTween;
	private bool _siteDetailPanelVisibleRequested;
	private bool _siteDetailPanelAuthoredLayoutCaptured;
	private Vector2 _siteDetailPanelAuthoredMinimumSize;
	private float _siteDetailPanelAuthoredOffsetLeft;
	private float _siteDetailPanelAuthoredOffsetTop;
	private float _siteDetailPanelAuthoredOffsetRight;
	private float _siteDetailPanelAuthoredOffsetBottom;
	private Label _siteTitleLabel;
	private Label _siteBodyLabel;
	private VBoxContainer _actionList;
	private Control _siteSummaryCard;
	private Control _actionCard;
	private Control _opportunityCard;
	private VBoxContainer _opportunityDetailContent;
	private Label _actionTitleLabel;
	private WorldOpportunityDetailPanel _opportunityDetailPanel;
	private WorldSiteHoverSummaryPanel _siteHoverSummaryPanel;
	private readonly Dictionary<string, WorldSiteNameBadge> _worldSiteNameBadges = new();

	private string _selectedSiteId = "";
	private string _selectedOpportunityId = "";
	private string _hoveredSiteId = "";
	private readonly HashSet<string> _selectedArmyIds = new();
	private bool _isExpeditionDrafting;
	private bool _isExpeditionTargeting;
	private string _expeditionSourceSiteId = "";
	private readonly HashSet<string> _expeditionHeroIds = new();
	private bool _isArmyBoxSelecting;
	private Vector2 _armySelectionStartScreen;
	private Vector2 _armySelectionCurrentScreen;
	private bool _worldClockPaused;
	private int _worldClockSpeedIndex = 2;
	private double _worldClockAccumulator;
	private bool _reportedStrategicNavigationNotSynchronized;
	private bool _worldMapOverlaySignalsConnected;
	private bool _worldMapDynamicOverlaySignalsConnected;
	private BattleStartRequest _pendingBattleRequest;
	private StrategicBattleActiveContext _pendingStrategicBattleActiveContext;
	private PendingBattleLaunchRollback _pendingBattleRollback;
	private StrategicBattleGateDialog _preBattleDialog;
	private string _activeBattleGateDialog = "";
	private Vector2 _lastWorldMapRootPosition = new(float.NaN, float.NaN);
	private Vector2 _lastWorldMapRootScale = new(float.NaN, float.NaN);
	private bool _strategicFogMaskReady;
	private string _lastStrategicFogRefreshSignature = "";

	private StrategicWorldDefinition Definition => StrategicWorldRuntime.Definition;
	private StrategicWorldState State => StrategicWorldRuntime.State;
	private StrategicRuntimeStage _runtimeStage = StrategicRuntimeStage.Bootstrapping;

	public StrategicWorldRoot()
	{
		_actionResolver = new WorldActionResolver(_battleUnitFactory.ResolveUnitDisplayName);
		_sceneTransitionRouter = new SceneTransitionRouter(new GodotSceneTransitionGateway(() => GetTree()));
	}

	public override void _Ready()
	{
		Current = this;
		GameLog.StartSession(nameof(StrategicWorldRoot));
		GameUiSkin.ApplyGameCursorTheme();
		MouseFilter = MouseFilterEnum.Stop;
		SetFullRect(this);

		StrategicWorldRuntime.EnsureInitialized();
		ResolveMainWorldViewportNodes();
		ResolveWorldMapNodes();
		ResolveWorldCamera();
		_worldCamera?.ResetNavigationInputState("strategic_world_ready");
		BuildStrategicFogOverlay();
		ConfigureStrategicNavigationContext();
		SyncDefinitionMapPositionsFromAnchors();
		RebuildSiteVisualFootprints();
		bool hasUnsupportedAssaultState = ReportUnsupportedPlayerAssaultArmies();
		BuildUi();
		ConfigureWorldCamera();
		UpdateWorldCameraView(true);
		_worldClockPaused = hasUnsupportedAssaultState || HasNavigationBlockedArmy();
		ConsumeBattleResult();
		_worldClockPaused = hasUnsupportedAssaultState || HasNavigationBlockedArmy();
		RefreshAll();
		_runtimeStage = StrategicRuntimeStage.WaitingForNavigation;
		GameLog.Info(nameof(StrategicWorldRoot), "Strategic world static initialization complete; waiting for runtime readiness.");
	}

	public override void _ExitTree()
	{
		_siteDetailPanelTween?.Kill();
		_siteDetailPanelTween = null;
		if (Current == this)
		{
			Current = null;
		}
	}

	public override void _Process(double delta)
	{
		GameUiSkin.UpdateCursorAnimation();

		if (!EnsureStrategicRuntimeReady())
		{
			UpdateWorldCameraView();
			return;
		}

		UpdateWorldArmyMovement(delta);
		UpdateWorldClock(delta);

		UpdateWorldCameraView();
	}

	public override void _Input(InputEvent @event)
	{
		GameUiSkin.HandleCursorInput(@event);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (!IsRootScreenMapInput(@event))
		{
			return;
		}

		if (TryHandleWorldCameraPointerInput(@event))
		{
			AcceptEvent();
			return;
		}

		HandleWorldArmyInput(@event);
	}

	private bool IsRootScreenMapInput(InputEvent @event)
	{
		if (!TryGetRootScreenPointerPosition(@event, out Vector2 screenPosition))
		{
			return false;
		}

		if (_isArmyBoxSelecting || _worldCamera?.IsPointerNavigationActive == true)
		{
			return true;
		}

		// HUD controls can pass scroll or drag events to this root Control. Only the
		// resolved map viewport may drive camera navigation or map selection.
		if (IsPointerOverNonMapUi())
		{
			return false;
		}

		return ResolveMainWorldViewportRect().HasPoint(screenPosition);
	}

	private bool IsPointerOverNonMapUi()
	{
		Control hoveredControl = GetViewport()?.GuiGetHoveredControl();
		if (hoveredControl == null || _strategicHudRoot == null)
		{
			return false;
		}

		if (hoveredControl == _strategicHudRoot)
		{
			return false;
		}

		if (!IsControlInTree(_strategicHudRoot, hoveredControl))
		{
			return false;
		}

		if (IsControlInTree(_mainWorldViewportHost, hoveredControl) || IsControlInTree(_worldMapOverlay, hoveredControl))
		{
			return false;
		}

		return true;
	}

	private static bool IsControlInTree(Control root, Control candidate)
	{
		return root != null &&
			candidate != null &&
			(root == candidate || root.IsAncestorOf(candidate));
	}

	private static bool TryGetRootScreenPointerPosition(InputEvent @event, out Vector2 screenPosition)
	{
		switch (@event)
		{
			case InputEventMouseButton mouseButton:
				screenPosition = mouseButton.Position;
				return true;
			case InputEventMouseMotion mouseMotion:
				screenPosition = mouseMotion.Position;
				return true;
			default:
				screenPosition = Vector2.Zero;
				return false;
		}
	}

	private bool TryHandleWorldCameraPointerInput(InputEvent @event)
	{
		if (_worldCamera == null)
		{
			return false;
		}

		bool handled = _worldCamera.TryHandlePointerNavigationAndZoomInput(@event);
		if (handled)
		{
			UpdateWorldCameraView();
		}

		return handled;
	}

}
