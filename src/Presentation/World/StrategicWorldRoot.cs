using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
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
	// 战略行军只允许查询这一层；视觉层不能作为寻路兜底，否则会重新产生多权威路径问题。
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
	private readonly WorldArmyMovementService _armyMovementService = new();
	private readonly WorldExpeditionService _expeditionService = new();
	private readonly WorldOpportunityService _opportunityService = new();
	private readonly WorldSiteDeploymentService _deploymentService = new();
	private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
	private readonly StrategicWorldSaveService _saveService = new();
	private readonly WorldTickService _worldTickService = new();
	private readonly WorldBattleProgressionService _worldBattleProgressionService = new();
	private readonly BattleUnitFactory _battleUnitFactory = new();

	private readonly Dictionary<string, Button> _siteButtons = new();
	private readonly Dictionary<string, Label> _siteLabels = new();
	private readonly Dictionary<string, SiteVisualFootprint> _siteVisualFootprints = new();
	private readonly HashSet<string> _reportedThreatNavigationFailures = new();
	private readonly HashSet<string> _reportedSiteVisualFootprintFailures = new();
	private readonly HashSet<string> _reportedSiteNavigationPointResolutions = new();
	private Control _mainWorldViewportHost;
	private SubViewport _mainWorldViewport;
	private Control _worldMapOverlay;
	private Control _topBarHost;
	private Control _leftPrimaryPanelHost;
	private Node2D _worldMapRoot;
	private MapCameraController _worldCamera;
	private Node2D _siteAnchorRoot;
	private Node2D _armySpawnPointRoot;
	private TileMapLayer _siteVisualLayer;
	private Node2D _strategicNavigationRoot;
	private TileMapLayer _strategicNavigationTileLayer;
	private StrategicWorldFogOverlay _fogOverlay;
	private StrategicNavigationContext _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable("strategic_navigation_not_configured");
	private Label _resourceLabel;
	private Label _worldClockLabel;
	private Label _noticeLabel;
	private Button _worldClockToggleButton;
	private Button _worldClockSpeedButton;
	private Label _siteTitleLabel;
	private Label _siteBodyLabel;
	private VBoxContainer _facilityList;
	private VBoxContainer _garrisonList;
	private VBoxContainer _threatList;
	private VBoxContainer _actionList;
	private Control _siteSummaryCard;
	private Control _facilityCard;
	private Control _defenseCard;
	private Control _actionCard;
	private Control _opportunityCard;
	private VBoxContainer _opportunityDetailContent;
	private Label _facilityTitleLabel;
	private Label _garrisonTitleLabel;
	private Label _actionTitleLabel;
	private WorldOpportunityDetailPanel _opportunityDetailPanel;
	private WorldSiteHoverSummaryPanel _siteHoverSummaryPanel;

	private string _selectedSiteId = "";
	private string _selectedThreatId = "";
	private string _selectedOpportunityId = "";
	private string _hoveredSiteId = "";
	private readonly HashSet<string> _selectedArmyIds = new();
	private bool _isExpeditionDrafting;
	private bool _isExpeditionTargeting;
	private string _expeditionSourceSiteId = "";
	private readonly Dictionary<string, int> _expeditionUnitCounts = new();
	private bool _isArmyBoxSelecting;
	private Vector2 _armySelectionStartScreen;
	private Vector2 _armySelectionCurrentScreen;
	private bool _worldClockPaused;
	private int _worldClockSpeedIndex = 2;
	private double _worldClockAccumulator;
	private bool _reportedStrategicNavigationNotSynchronized;
	private bool _worldMapOverlaySignalsConnected;
	private BattleStartRequest _pendingBattleRequest;
	private PendingBattleLaunchRollback _pendingBattleRollback;
	private AcceptDialog _battleAlertDialog;
	private AcceptDialog _preBattleDialog;
	private string _activeBattleGateDialog = "";
	private Vector2 _lastWorldMapRootPosition = new(float.NaN, float.NaN);
	private Vector2 _lastWorldMapRootScale = new(float.NaN, float.NaN);
	private bool _strategicFogMaskReady;

	private StrategicWorldDefinition Definition => StrategicWorldRuntime.Definition;
	private StrategicWorldState State => StrategicWorldRuntime.State;
	private StrategicRuntimeStage _runtimeStage = StrategicRuntimeStage.Bootstrapping;

	public StrategicWorldRoot()
	{
		_actionResolver = new WorldActionResolver(_battleUnitFactory.ResolveUnitDisplayName);
	}

	public override void _Ready()
	{
		Current = this;
		GameLog.StartSession(nameof(StrategicWorldRoot));
		MouseFilter = MouseFilterEnum.Stop;
		SetFullRect(this);

		StrategicWorldRuntime.EnsureInitialized();
		ResolveMainWorldViewportNodes();
		ResolveWorldMapNodes();
		ResolveWorldCamera();
		BuildStrategicFogOverlay();
		ConfigureStrategicNavigationContext();
		SyncDefinitionMapPositionsFromAnchors();
		RebuildSiteVisualFootprints();
		bool hasUnsupportedAssaultState = ReportUnsupportedPlayerAssaultArmies();
		BuildUi();
		ConfigureWorldCamera();
		UpdateWorldCameraView(true);
		_worldClockPaused = hasUnsupportedAssaultState || HasAttackingThreat() || HasNavigationBlockedArmy();
		ConsumeBattleResult();
		_worldClockPaused = hasUnsupportedAssaultState || HasAttackingThreat() || HasNavigationBlockedArmy();
		RefreshAll();
		_runtimeStage = StrategicRuntimeStage.WaitingForNavigation;
		GameLog.Info(nameof(StrategicWorldRoot), "Strategic world static initialization complete; waiting for runtime readiness.");
	}

	public override void _ExitTree()
	{
		if (Current == this)
		{
			Current = null;
		}
	}

	public override void _Process(double delta)
	{
		if (!EnsureStrategicRuntimeReady())
		{
			UpdateWorldCameraView();
			return;
		}

		bool worldArmyPresentationChanged = UpdateWorldArmyMovement(delta);
		UpdateWorldClock(delta);
		if (worldArmyPresentationChanged)
		{
			// 迷雾覆盖层会重建 CPU mask 和 ImageTexture；空闲帧不能无条件刷新。
			RefreshWorldIntel();
		}

		UpdateWorldCameraView();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (TryHandleWorldCameraPointerInput(@event))
		{
			AcceptEvent();
			return;
		}

		HandleWorldArmyInput(@event);
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
