using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Maps;
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

public partial class WorldSiteRoot : Node2D, IBattleMapBoundsSource
{
	private const float SitePlacementPickRadiusPixels = 42.0f;
	private const int DeploymentDragZIndex = 4096;
	private const string SiteExplorationHudScenePath = "res://scenes/world/sites/SiteExplorationHud.tscn";
	private const string FacilitySlotsRootName = "FacilitySlots";
	private const string SiteExplorationPauseReady = "exploration_ready";
	private const string SiteExplorationPauseMovePreview = "exploration_move_preview";
	private const string SiteExplorationPauseAlertRadius = "exploration_alert_radius";

	[Signal]
	public delegate void SiteMapLoadedEventHandler(Node activeSiteMap);

	[Export]
	public NodePath MainWorldViewportHostPath { get; set; } = new("MainWorldViewportHost");

	[Export]
	public NodePath MainWorldViewportPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport");

	[Export]
	public NodePath MapRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/MapRoot");

	[Export]
	public NodePath UnitRootPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/UnitRoot");

	[Export]
	public NodePath HighlightOverlayPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/OverlayRoot/GridHighlightOverlay");

	[Export]
	public NodePath SelectionVignetteOverlayPath { get; set; } = new("CanvasLayer/SelectionVignetteOverlay");

	[Export]
	public NodePath BattleCameraPath { get; set; } = new("MainWorldViewportHost/MainWorldViewport/Camera2D");

	[Export]
	public PackedScene SiteMapScene { get; set; }

	[Export]
	public PackedScene FieldInterceptMapScene { get; set; }

	private Node _mapRoot;
	private Control _mainWorldViewportHost;
	private SubViewport _mainWorldViewport;
	private BattleUnitRoot _unitRoot;
	private Node _activeSiteMap;
	private BattleGridMap _activeGridMap;
	private BattleMapLayer _coordinateLayer;
	private BattleGridHighlightOverlay _highlightOverlay;
	private BattleSelectionVignetteOverlay _selectionVignetteOverlay;
	private BattleCameraController _battleCamera;
	private Control _siteHudRoot;
	private Control _siteHudTopBar;
	private Control _sitePeacetimePanel;
	private Node2D _sitePlacementEntityRoot;
	private Label _siteHudTitle;
	private Label _siteHudBody;
	private Label _siteResourceLabel;
	private Label _siteNoticeLabel;
	private Label _siteSelectionLabel;
	private Control _siteFacilityBuildCard;
	private Control _siteFacilityCard;
	private Control _siteDefenseCard;
	private Control _siteActionCard;
	private Control _siteBattlePreparationContent;
	private Label _siteFacilityBuildTitle;
	private VBoxContainer _siteFacilityBuildList;
	private VBoxContainer _siteBattlePreparationRosterList;
	private Label _siteBattlePreparationEnemySummary;
	private Label _siteBattlePreparationStatus;
	private VBoxContainer _siteBattlePreparationActionList;
	private Button _returnMapButton;
	private VBoxContainer _siteFacilityList;
	private VBoxContainer _siteGarrisonList;
	private VBoxContainer _siteThreatList;
	private VBoxContainer _siteActionList;
	private readonly Dictionary<string, Node2D> _sitePlacementEntities = new();
	private readonly Dictionary<string, Node2D> _siteExplorationPatrolMarkers = new(System.StringComparer.Ordinal);
	private readonly Dictionary<string, WorldFacilitySlotEntity> _siteFacilitySlotEntities = new();
	private readonly Dictionary<string, WorldFacilitySlotRuntimeLayout> _siteFacilitySlotLayouts = new();
	private SemanticMapMarkerExtractionResult _semanticMapMarkers = new();
	private WorldSiteRuntimeDeploymentCache _deploymentCache;
	private bool _battleRuntimeEnabled = true;
	private string _battleStartBlockedReason = "";
	private bool _isBattlePreparationActive;
	private BattleStartRequest _battlePreparationRequest;
	private string _siteHudReturnScenePath = "";
	private string _siteHudSiteId = "";
	private string _selectedPlacementId = "";
	private string _selectedFacilitySlotId = "";
	private string _draggedPlacementId = "";
	private Vector2 _draggedPlacementOriginGlobalPosition;
	private string _draggedBattleForceId = "";
	private int _draggedBattleForceIndex = -1;
	private BattleFaction _draggedBattleForceFallbackFaction = BattleFaction.Neutral;
	private BattleEntity _draggedBattleRosterEntity;
	private Node2D _siteExplorationPartyMarker;
	private Node2D _siteExplorationAlertRangeRoot;
	private Control _siteExplorationHud;
	private Control _siteExplorationHudPanel;
	private Control _siteExplorationAlertActions;
	private Label _siteExplorationAlertLabel;
	private Button _siteExplorationWaitButton;
	private Button _siteExplorationEngageButton;
	private Button _siteExplorationRetreatButton;
	private string _lastSiteExplorationButtonRole = "";
	private ulong _lastSiteExplorationButtonMsec;
	private readonly BattleUnitFactory _battleUnitFactory = new();
	private readonly WorldBattleResultApplier _worldBattleResultApplier = new();
	private readonly WorldActionResolver _worldActionResolver;
	private readonly WorldSiteDeploymentService _deploymentService = new();
	private readonly WorldSiteRuntimeDeploymentCacheBuilder _deploymentCacheBuilder = new();
	private readonly WorldSiteDeploymentTargetEvaluator _deploymentTargetEvaluator = new();
	private readonly WorldSiteDeploymentTerrainReconciler _deploymentTerrainReconciler = new();
	private readonly WorldSiteBattleDeploymentPreparer _battleDeploymentPreparer = new();
	private readonly WorldSiteBattleLauncher _battleLauncher = new();
	private readonly WorldSiteBattleGroupRuntimeAdapter _battleGroupRuntimeAdapter = new();
	private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
	private readonly SemanticMapMarkerExtractor _semanticMapMarkerExtractor = new();

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
		_worldActionResolver = new WorldActionResolver(_battleUnitFactory.ResolveUnitDisplayName);
	}

	public override void _Ready()
	{
		GameLog.StartSession(nameof(WorldSiteRoot));

		ResolveMainWorldViewportNodes();
		_mapRoot = GetNode<Node>(MapRootPath);
		_unitRoot = GetNodeOrNull<BattleUnitRoot>(UnitRootPath);
		_highlightOverlay = GetNodeOrNull<BattleGridHighlightOverlay>(HighlightOverlayPath);
		_selectionVignetteOverlay = GetNodeOrNull<BattleSelectionVignetteOverlay>(SelectionVignetteOverlayPath);
		_battleCamera = GetNodeOrNull<BattleCameraController>(BattleCameraPath);
		GetViewport().SizeChanged += OnViewportSizeChanged;
		BuildSiteHud();
		UpdateMainWorldViewportLayout("ready");
		EnsureBattleRenderSortDomain();

		bool hasActiveBattleLaunch = BattleSessionHandoff.HasActiveLaunch;
		if (_unitRoot != null)
		{
			_unitRoot.Initialize(TryGetCellGlobalPosition, ApplyEntityRenderSort);
		}
		else
		{
			GameLog.Warn(nameof(WorldSiteRoot), $"Unit root missing or missing BattleUnitRoot script path={UnitRootPath}");
		}

		GameLog.Info(
			nameof(WorldSiteRoot),
			$"Ready mapRoot={_mapRoot?.GetPath()} unitRoot={_unitRoot?.GetPath()} highlight={_highlightOverlay != null}");

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
			BattleSessionHandoff.CancelBattle();
			StrategicWorldRuntime.LastNotice = _battleStartBlockedReason;
			SwitchToNonBattleUi(BattleOutcome.None, null, null, _battleStartBlockedReason);
		}
		else
		{
			SwitchToNonBattleUi(BattleOutcome.None, null, null, "");
		}
	}

	public override void _Process(double delta)
	{
		WorldSiteRuntimeMode runtimeMode = RuntimeMode;
		if (runtimeMode == WorldSiteRuntimeMode.Exploration)
		{
			ContinueConfirmedSiteExplorationMoveIfReady();
			UpdateSiteMapEntities();
			return;
		}

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
			return;
		}

		if (runtimeMode == WorldSiteRuntimeMode.Exploration)
		{
			TryHandleSiteExplorationInput(@event);
			return;
		}

		if (TryHandleFacilitySlotInput(@event))
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

		WorldSiteState site = ResolveSiteState(_siteHudSiteId);
		WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
		return IsSiteExplorationActive(site, definition)
			? WorldSiteRuntimeMode.Exploration
			: WorldSiteRuntimeMode.Management;
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
	}

	private void UpdateMainWorldViewportLayout(string reason)
	{
		if (_mainWorldViewportHost == null || _mainWorldViewport == null)
		{
			return;
		}

		Rect2 worldViewportRect = ResolveMainWorldViewportRect();
		bool reserveUiWorkspace = _siteHudRoot?.Visible == true;

		// The site/battle map is a separate world viewport. HUD and modal UI stay on
		// the outer CanvasLayer, so root mouse positions must cross this boundary.
		_mainWorldViewportHost.Position = worldViewportRect.Position;
		_mainWorldViewportHost.Size = worldViewportRect.Size;
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
		if (_siteHudRoot?.Visible != true || _sitePeacetimePanel == null)
		{
			return new Rect2(Vector2.Zero, rootViewportSize);
		}

		Rect2 panelRect = _sitePeacetimePanel.GetGlobalRect();
		float sideMargin = Mathf.Max(0.0f, panelRect.Position.X);
		float bottomMargin = Mathf.Max(0.0f, rootViewportSize.Y - panelRect.End.Y);
		Vector2 position = new(panelRect.End.X + sideMargin, panelRect.Position.Y);
		Vector2 size = new(
			Mathf.Max(1.0f, rootViewportSize.X - position.X - sideMargin),
			Mathf.Max(1.0f, rootViewportSize.Y - position.Y - bottomMargin));

		return new Rect2(position, size);
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
		if (!BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
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
		SetFacilitySlotsVisible(true);
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
		if (BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request) &&
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

		StrategicWorldRuntime.MarkWorldResumeAfterSiteReturn();
		Error error = GetTree().ChangeSceneToFile(scenePath);
		if (error != Error.Ok)
		{
			GameLog.Warn(nameof(WorldSiteRoot), $"Cannot return to campaign scene path={scenePath} error={error}");
		}
	}

}
