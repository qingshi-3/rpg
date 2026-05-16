using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.InputSystem;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Battle.UI;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot : Node2D
{
    private const float SitePlacementPickRadiusPixels = 42.0f;
    private const string SiteExplorationHudScenePath = "res://scenes/world/sites/SiteExplorationHud.tscn";
    private const string FacilitySlotsRootName = "FacilitySlots";
    private const string SiteExplorationPauseReady = "exploration_ready";
    private const string SiteExplorationPauseMovePreview = "exploration_move_preview";
    private const string SiteExplorationPauseAlertRadius = "exploration_alert_radius";

    private sealed class WorldSiteRuntimeDeploymentCache
    {
        public WorldSiteRuntimeDeploymentCache(
            string siteId,
            Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>> candidatesByDirection)
        {
            SiteId = siteId ?? "";
            CandidatesByDirection = candidatesByDirection ?? new Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>>();
        }

        public string SiteId { get; }
        public Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>> CandidatesByDirection { get; }

        public IReadOnlyList<WorldSiteDeploymentCell> GetCandidates(WorldSiteAttackDirection direction)
        {
            if (CandidatesByDirection.TryGetValue(direction, out List<WorldSiteDeploymentCell> candidates) &&
                candidates.Count > 0)
            {
                return candidates;
            }

            return CandidatesByDirection.TryGetValue(WorldSiteAttackDirection.Any, out List<WorldSiteDeploymentCell> anyCandidates)
                ? anyCandidates
                : System.Array.Empty<WorldSiteDeploymentCell>();
        }
    }

    private sealed class WorldSiteLivePlacementSnapshot
    {
        public string PlacementId { get; set; } = "";
        public string UnitTypeId { get; set; } = "";
        public int UnitIndex { get; set; }
        public BattleFaction Faction { get; set; } = BattleFaction.Neutral;
        public int CellX { get; set; }
        public int CellY { get; set; }
        public int CellHeight { get; set; }
    }

    private sealed class WorldFacilitySlotRuntimeLayout
    {
        public string SlotId { get; set; } = "";
        public GridPosition SortCell { get; set; }
        public GridSurfacePosition SortSurface { get; set; }
        public int FootprintWidth { get; set; } = 1;
        public int FootprintHeight { get; set; } = 1;
        public int ZIndex { get; set; }
        public List<GridPosition> FootprintCells { get; } = new();
    }

    private sealed class BattleCorpsRuntimeState
    {
        public string CorpsId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public BattleFaction Faction { get; set; } = BattleFaction.Neutral;
        public BattleCorpsCommand Command { get; set; } = BattleCorpsCommand.Assault;
        public string CommanderEntityId { get; set; } = "";
        public List<string> MemberEntityIds { get; } = new();
    }

    private sealed class SiteBattleLaunchRollback
    {
        public string SiteId { get; set; } = "";
        public bool HasPreviousSiteMode { get; set; }
        public WorldSiteMode PreviousSiteMode { get; set; } = WorldSiteMode.Peacetime;
        public bool HasPreviousExplorationState { get; set; }
        public bool PreviousExplorationPaused { get; set; }
        public string PreviousExplorationPauseReason { get; set; } = "";
        public string PreviousActiveAlertPatrolId { get; set; } = "";
        public List<string> PreviousPendingPathCellKeys { get; } = new();
    }

    [Signal]
    public delegate void SiteMapLoadedEventHandler(Node activeSiteMap);

    [Export]
    public NodePath MapRootPath { get; set; } = new("MapRoot");

    [Export]
    public NodePath UnitRootPath { get; set; } = new("UnitRoot");

    [Export]
    public NodePath HighlightOverlayPath { get; set; } = new("OverlayRoot/GridHighlightOverlay");

    [Export]
    public NodePath PreviewControllerPath { get; set; } = new("OverlayRoot/BattlePreviewController");

    [Export]
    public NodePath SelectionVignetteOverlayPath { get; set; } = new("CanvasLayer/SelectionVignetteOverlay");

    [Export]
    public NodePath HudRootPath { get; set; } = new("CanvasLayer/BattleHudRoot");

    [Export]
    public NodePath BattleCameraPath { get; set; } = new("Camera2D");

    [Export]
    public NodePath InputRouterPath { get; set; } = new("InputRoot/BattleInputRouter");

    [Export]
    public NodePath CommandControllerPath { get; set; } = new("FlowRoot/BattleCommandController");

    [Export]
    public NodePath TurnControllerPath { get; set; } = new("FlowRoot/BattleTurnController");

    [Export]
    public NodePath IntentControllerPath { get; set; } = new("FlowRoot/BattleIntentController");

    [Export]
    public PackedScene SiteMapScene { get; set; }

    [Export]
    public PackedScene FieldInterceptMapScene { get; set; }

    private Node _mapRoot;
    private BattleUnitRoot _unitRoot;
    private Node _activeSiteMap;
    private BattleGridMap _activeGridMap;
    private BattleMapLayer _coordinateLayer;
    private BattleGridHighlightOverlay _highlightOverlay;
    private BattlePreviewController _previewController;
    private BattleSelectionVignetteOverlay _selectionVignetteOverlay;
    private BattleCameraController _battleCamera;
    private BattleHudRoot _hudRoot;
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
    private Label _siteFacilityBuildTitle;
    private VBoxContainer _siteFacilityBuildList;
    private Button _returnMapButton;
    private VBoxContainer _siteFacilityList;
    private VBoxContainer _siteGarrisonList;
    private VBoxContainer _siteThreatList;
    private VBoxContainer _siteActionList;
    private readonly Dictionary<string, Node2D> _sitePlacementEntities = new();
    private readonly Dictionary<string, Node2D> _siteExplorationPatrolMarkers = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, WorldFacilitySlotEntity> _siteFacilitySlotEntities = new();
    private readonly Dictionary<string, WorldFacilitySlotRuntimeLayout> _siteFacilitySlotLayouts = new();
    private WorldSiteRuntimeDeploymentCache _deploymentCache;
    private BattleInputRouter _inputRouter;
    private BattleCommandController _commandController;
    private BattleTurnController _turnController;
    private BattleIntentController _intentController;
    private int _battleStateVersion;
    private bool _battleEndHandled;
    private bool _battleRuntimeEnabled = true;
    private string _battleStartBlockedReason = "";
    private string _siteHudReturnScenePath = "";
    private string _siteHudSiteId = "";
    private string _selectedPlacementId = "";
    private string _selectedFacilitySlotId = "";
    private string _draggedPlacementId = "";
    private Vector2 _draggedPlacementOriginGlobalPosition;
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
    private readonly BattleActionExecutor _actionExecutor = new();
    private readonly BattleUnitFactory _battleUnitFactory = new();
    private readonly WorldBattleResultApplier _worldBattleResultApplier = new();
    private readonly WorldActionResolver _worldActionResolver;
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly Dictionary<string, BattleCorpsRuntimeState> _battleCorpsStates = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, string> _battleEntityToCorpsId = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, BattleUnitControlMode> _battleEntityControlModes = new(System.StringComparer.Ordinal);

    public Node ActiveSiteMap => _activeSiteMap;
    public BattleGridMap ActiveGridMap => _activeGridMap;
    public BattleEntity SelectedEntity => _commandController?.SelectedEntity;
    public bool AllowsDebugHoverInfo => _commandController?.AllowsDebugHoverInfo == true;
    public bool IsEnemyPhaseRunning => _turnController?.IsEnemyPhaseRunning == true;

    public WorldSiteRoot()
    {
        _worldActionResolver = new WorldActionResolver(_battleUnitFactory.ResolveUnitDisplayName);
    }

    public override void _Ready()
    {
        GameLog.StartSession(nameof(WorldSiteRoot));

        _mapRoot = GetNode<Node>(MapRootPath);
        _unitRoot = GetNodeOrNull<BattleUnitRoot>(UnitRootPath);
        _highlightOverlay = GetNodeOrNull<BattleGridHighlightOverlay>(HighlightOverlayPath);
        _previewController = GetNodeOrNull<BattlePreviewController>(PreviewControllerPath);
        _selectionVignetteOverlay = GetNodeOrNull<BattleSelectionVignetteOverlay>(SelectionVignetteOverlayPath);
        _battleCamera = GetNodeOrNull<BattleCameraController>(BattleCameraPath);
        _hudRoot = GetNodeOrNull<BattleHudRoot>(HudRootPath);
        _inputRouter = GetNodeOrNull<BattleInputRouter>(InputRouterPath);
        _commandController = GetNodeOrNull<BattleCommandController>(CommandControllerPath);
        _turnController = GetNodeOrNull<BattleTurnController>(TurnControllerPath);
        _intentController = GetNodeOrNull<BattleIntentController>(IntentControllerPath);
        GetViewport().SizeChanged += OnViewportSizeChanged;
        BuildSiteHud();
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
        if (_inputRouter != null)
        {
            _inputRouter.Initialize(TryGetMouseGridPosition);
        }
        else
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Input router missing path={InputRouterPath}");
        }

        if (_hudRoot != null)
        {
            _hudRoot.CommandSelected += commandId => _commandController?.OnBattleCommandRequested(BattleCommand.HudCommandSelected(commandId));
            _hudRoot.CommandCancelled += () =>
            {
                _commandController?.OnBattleCommandRequested(BattleCommand.HudCommandCancelled());
            };
            _hudRoot.ConfigureCorpsResolvers(
                ResolveCorpsLabelForEntity,
                ResolveCorpsCommandLabelForEntity);
        }

        if (_intentController != null)
        {
            _intentController.Initialize(
                _unitRoot,
                _previewController,
                CreateAiContext,
                ExecuteActionRequest,
                () => _turnController?.EvaluateBattleOutcome() == true,
                text => _hudRoot?.ShowActionHint(text),
                MarkBattleStateChanged,
                () => _unitRoot?.UnitMoveDuration ?? 0.28);
        }
        else
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Intent controller missing path={IntentControllerPath}");
        }

        if (_previewController != null)
        {
            _previewController.Initialize(
                _highlightOverlay,
                _selectionVignetteOverlay,
                () => _activeGridMap,
                TryGetMouseGridPosition,
                GetBattleEntitiesSnapshot,
                FindEntityAt,
                BuildBlockedMovementSurfaces,
                entity => _turnController?.HandleEntityDefeated(entity),
                CreateAiContext,
                _intentController == null ? null : _intentController.GetEnemyIntent,
                text => _hudRoot?.ShowActionHint(text),
                () => _unitRoot?.HasActiveMovementTweens == true);
        }
        else
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Preview controller missing path={PreviewControllerPath}");
        }

        if (_turnController != null)
        {
            _turnController.Initialize(
                _unitRoot,
                _commandController,
                _previewController,
                GetBattleEntitiesSnapshot,
                GetAlliedAutoActionOrder,
                _intentController == null ? null : _intentController.GenerateEnemyIntents,
                _intentController == null ? null : _intentController.ExecuteEnemyAction,
                ExecuteAlliedAutoAction,
                _intentController == null ? null : _intentController.ClearEnemyIntentBookkeeping,
                MarkBattleStateChanged,
                text => _hudRoot?.ShowActionHint(text),
                () => _hudRoot?.ClearActiveCommand(),
                OnBattleEnded,
                OnTurnQueueUpdated,
                (entity, faction, duration) => _unitRoot?.ShowActionCueAsync(entity, faction, duration) ?? System.Threading.Tasks.Task.CompletedTask,
                entity => _unitRoot?.HideActionCueAsync(entity) ?? System.Threading.Tasks.Task.CompletedTask);
        }
        else
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Turn controller missing path={TurnControllerPath}");
        }

        if (_commandController != null)
        {
            _commandController.Initialize(
                _previewController,
                () => _turnController?.IsEnemyPhaseRunning == true,
                () => _activeGridMap,
                FindEntityAt,
                ExecuteActionRequest,
                hint => _turnController?.EndPlayerTurn(hint),
                CycleCorpsCommandForEntity,
                () => _turnController?.EvaluateBattleOutcome() == true,
                ShowBattleEntityInHud,
                text => _hudRoot?.ShowActionHint(text),
                () => _hudRoot?.ClearActiveCommand(),
                () => _hudRoot?.HideSelectedActionMenu(),
                entity => _turnController?.GetSelectionBlockReason(entity) ?? "",
                () => _unitRoot?.HasActiveMovementTweens == true);

            if (_inputRouter != null)
            {
                _inputRouter.CommandRequested += _commandController.OnBattleCommandRequested;
            }
        }
        else
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Command controller missing path={CommandControllerPath}");
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Ready mapRoot={_mapRoot?.GetPath()} unitRoot={_unitRoot?.GetPath()} preview={_previewController != null} highlight={_highlightOverlay != null} hud={_hudRoot != null} input={_inputRouter != null} command={_commandController != null} turn={_turnController != null} intent={_intentController != null}");

        LoadConfiguredSiteMap();
        ApplyBattleStartRequest();

        if (hasActiveBattleLaunch && string.IsNullOrWhiteSpace(_battleStartBlockedReason))
        {
            ActivateBattleRuntime();
        }
        else if (hasActiveBattleLaunch)
        {
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
        if (!_battleRuntimeEnabled)
        {
            ContinueConfirmedSiteExplorationMoveIfReady();
            UpdateSiteMapEntities();
            return;
        }

        _previewController?.UpdateMovementPathPreview(_commandController?.IsMoveTargeting == true);
        _previewController?.UpdateAbilityTargetPreview(_commandController?.IsAbilityTargeting == true);
        _previewController?.UpdateHoverIntentPreview(_commandController?.CanShowHoverIntentPreview == true, _battleStateVersion);
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleSiteExplorationInput(@event))
        {
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

        RebuildSiteDeploymentRuntimeCache(ResolveActiveWorldSiteId());
        SetFacilitySlotsVisible(true);
        EmitSignal(SignalName.SiteMapLoaded, _activeSiteMap);

        PlaceBattleEntitiesOnGrid();
    }

    private void EnsureBattleRenderSortDomain()
    {
        YSortEnabled = true;

        if (_mapRoot is CanvasItem mapRootItem)
        {
            mapRootItem.YSortEnabled = true;
        }

        if (_unitRoot != null)
        {
            _unitRoot.YSortEnabled = true;
        }
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
        _deploymentCache = null;
        if (_activeGridMap == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot build site deployment cache site={siteId} reason=grid_missing");
            return;
        }

        GridCellSurface[] surfaces = _activeGridMap.Surfaces.Values
            .Where(IsDeploymentCandidateSurface)
            .ToArray();
        var candidatesByDirection = new Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>>();
        WorldSiteAttackDirection[] directions =
        {
            WorldSiteAttackDirection.Any,
            WorldSiteAttackDirection.North,
            WorldSiteAttackDirection.South,
            WorldSiteAttackDirection.West,
            WorldSiteAttackDirection.East
        };

        foreach (WorldSiteAttackDirection direction in directions)
        {
            candidatesByDirection[direction] = surfaces.Length == 0
                ? new List<WorldSiteDeploymentCell>()
                : OrderDeploymentSurfaceCandidates(surfaces, direction)
                    .Select(surface => new WorldSiteDeploymentCell(
                        new Vector2I(surface.Position.X, surface.Position.Y),
                        surface.Height,
                        surface.TerrainTag ?? "",
                        BattleRuleQueries.IsWater(surface)))
                    .ToList();
        }

        _deploymentCache = new WorldSiteRuntimeDeploymentCache(siteId, candidatesByDirection);
        string counts = string.Join(
            " ",
            directions.Select(direction => $"{direction}={candidatesByDirection[direction].Count}"));
        GameLog.Info(nameof(WorldSiteRoot), $"SiteDeploymentCacheBuilt site={siteId} surfaces={surfaces.Length} {counts}");
    }

    private bool IsDeploymentCandidateSurface(GridCellSurface surface)
    {
        return surface is { IsWalkable: true, MoveCost: > 0 } &&
               _activeGridMap?.IsTopSurface(surface.SurfacePosition) == true;
    }

    private void ApplyBattleStartRequest()
    {
        _battleStartBlockedReason = "";
        if (_unitRoot == null || !BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            ClearBattleCorpsRuntime();
            return;
        }

        if (request.BattleKind is BattleKind.AssaultSite or BattleKind.DefenseRaid or BattleKind.FieldIntercept)
        {
            if (!EnsureBattleRequestSiteDeployments(request))
            {
                _battleStartBlockedReason = "场域部署数据缺失，无法进入战斗。";
                ClearBattleEntities();
                return;
            }

            ClearBattleEntities();
            ClearBattleCorpsRuntime();
            var reservedDeploymentSurfaces = new HashSet<GridSurfacePosition>();
            AddRequestedForces(request.PlayerForces, BattleFaction.Player, request, reservedDeploymentSurfaces);
            AddRequestedForces(request.EnemyForces, BattleFaction.Enemy, request, reservedDeploymentSurfaces);
            FinalizeBattleCorpsControlOwnership();
        }

        if (request.BattleKind == BattleKind.DefenseRaid)
        {
            ApplyBattleModifiers(request);
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle request consumed kind={request.BattleKind} target={request.TargetSiteId} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} modifiers={request.BattleModifiers.Count}");
    }

    private bool ActivateBattleRuntime()
    {
        if (!string.IsNullOrWhiteSpace(_battleStartBlockedReason))
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        PlaceBattleEntitiesOnGrid();
        RegisterBattleEntities();
        SetBattleRuntimeEnabled(true);
        _turnController?.StartBattle();
        return true;
    }

    private bool EnsureBattleRequestSiteDeployments(BattleStartRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TargetSiteId))
        {
            GameLog.Error(nameof(WorldSiteRoot), "Cannot prepare battle deployments because request target site is missing.");
            return false;
        }

        if (StrategicWorldRuntime.State?.SiteStates.TryGetValue(request.TargetSiteId, out WorldSiteState site) != true)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because WorldSiteState is missing site={request.TargetSiteId}");
            return false;
        }

        if (StrategicWorldRuntime.Definition == null)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because StrategicWorldDefinition is missing site={site.SiteId}");
            return false;
        }

        WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(StrategicWorldRuntime.Definition).GetSite(site.SiteId);
        if (siteDefinition == null)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because WorldSiteDefinition is missing site={site.SiteId}");
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        if (_deploymentCache == null ||
            _deploymentCache.GetCandidates(WorldSiteAttackDirection.Any).Count == 0)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because deployment cache is empty site={site.SiteId}");
            return false;
        }

        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
        bool success = EnsureSitePlacementsRespectTerrain(site, siteDefinition);

        foreach (BattleForceRequest force in request.PlayerForces)
        {
            success &= EnsureForceWorldSitePlacement(request, force, BattleFaction.Player, site);
        }

        foreach (BattleForceRequest force in request.EnemyForces)
        {
            success &= EnsureForceWorldSitePlacement(request, force, BattleFaction.Enemy, site);
        }

        success &= ApplyPreferredPlacementsFromWorldSite(site, siteDefinition, request.PlayerForces);
        success &= ApplyPreferredPlacementsFromWorldSite(site, siteDefinition, request.EnemyForces);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleDeploymentsPreparedFromWorldSite site={site.SiteId} request={request.RequestId} placements={site.UnitPlacements.Count} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count}");
        return success;
    }

    private bool EnsureForceWorldSitePlacement(
        BattleStartRequest request,
        BattleForceRequest force,
        BattleFaction fallbackFaction,
        WorldSiteState site)
    {
        if (force == null || force.Count <= 0 || IsResidentWorldSiteForceForSite(force, site))
        {
            return true;
        }

        WorldSiteAttackDirection desiredDirection = ResolveForceDeploymentDirection(request, force, fallbackFaction);
        BattleEntranceRequest entrance = ResolveForceEntrance(request, force, desiredDirection);
        WorldSiteAttackDirection deploymentDirection = entrance != null && entrance.Direction != WorldSiteAttackDirection.Any
            ? entrance.Direction
            : desiredDirection;
        string entranceId = entrance?.EntranceId ?? force.PreferredEntranceId ?? "";
        if (!string.IsNullOrWhiteSpace(entranceId))
        {
            force.PreferredEntranceId = entranceId;
        }

        WorldSiteUnitPlacementKind placementKind = request.BattleKind == BattleKind.FieldIntercept
            ? WorldSiteUnitPlacementKind.FieldArmy
            : IsAttackingForce(request, force, fallbackFaction)
                ? WorldSiteUnitPlacementKind.Attacker
                : WorldSiteUnitPlacementKind.Defender;
        bool canEnterWater = ResolveForceCanEnterWater(force);
        IReadOnlyList<WorldSiteDeploymentCell> candidates = (_deploymentCache?.GetCandidates(deploymentDirection) ??
                                                            System.Array.Empty<WorldSiteDeploymentCell>())
            .Where(candidate => CanUseDeploymentCell(candidate, canEnterWater))
            .ToArray();
        if (!_deploymentService.EnsureBattlePlacementsForForce(
                site,
                force,
                placementKind,
                deploymentDirection,
                candidates,
                request.ThreatId,
                entranceId,
                out string failureReason))
        {
            GameLog.Error(
                nameof(WorldSiteRoot),
                $"BattleDeploymentPrepareFailed site={site?.SiteId ?? ""} request={request.RequestId} force={force.ForceId} unit={force.UnitDefinitionId} reason={failureReason} direction={deploymentDirection} entrance={entranceId} canEnterWater={canEnterWater}");
            return false;
        }

        return true;
    }

    private bool ResolveForceCanEnterWater(BattleForceRequest force)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(force?.UnitDefinitionId, out BattleUnitDefinition definition))
        {
            return false;
        }

        return definition.CanEnterWater;
    }

    private static bool CanUseDeploymentCell(WorldSiteDeploymentCell candidate, bool canEnterWater)
    {
        return canEnterWater || !candidate.IsWater;
    }

    private bool ApplyPreferredPlacementsFromWorldSite(
        WorldSiteState site,
        WorldSiteDefinition definition,
        IEnumerable<BattleForceRequest> forces)
    {
        if (site == null || forces == null)
        {
            return false;
        }

        bool success = true;
        foreach (BattleForceRequest force in forces)
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            bool canEnterWater = ResolveForceCanEnterWater(force);
            WorldSiteUnitPlacement[] placements = ResolveWorldSitePlacementsForForce(site, force)
                .OrderBy(placement => placement.UnitIndex)
                .ThenBy(placement => placement.PlacementId)
                .Take(force.Count)
                .ToArray();
            force.PreferredPlacements.Clear();
            foreach (WorldSiteUnitPlacement placement in placements)
            {
                if (!CanUsePlacement(placement, canEnterWater) &&
                    !TryRelocatePlacementForTerrain(site, definition, placement, canEnterWater, out string failureReason))
                {
                    success = false;
                    GameLog.Error(
                        nameof(WorldSiteRoot),
                        $"BattleForcePlacementInvalidTerrain site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} placement={placement.PlacementId} cell=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={GetPlacementTerrainTag(placement)} canEnterWater={canEnterWater} reason={failureReason}");
                    continue;
                }

                force.PreferredPlacements.Add(new BattleForcePlacementRequest
                {
                    PlacementId = placement.PlacementId,
                    CellX = placement.CellX,
                    CellY = placement.CellY,
                    CellHeight = placement.CellHeight
                });
            }

            if (force.PreferredPlacements.Count < force.Count)
            {
                success = false;
                GameLog.Error(
                    nameof(WorldSiteRoot),
                    $"BattleForceMissingWorldSitePlacements site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} expected={force.Count} actual={force.PreferredPlacements.Count}");
            }
        }

        return success;
    }

    private bool EnsureSitePlacementsRespectTerrain(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site == null || definition == null)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        bool success = true;
        int relocated = 0;
        int heightSynced = 0;
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements.ToArray())
        {
            bool canEnterWater = ResolvePlacementCanEnterWater(placement);
            if (CanUsePlacementSurface(placement, canEnterWater, out string failureReason))
            {
                if (TrySyncPlacementSurfaceHeight(placement))
                {
                    heightSynced++;
                }

                continue;
            }

            if (TryRelocatePlacementForTerrain(site, definition, placement, canEnterWater, out failureReason))
            {
                relocated++;
                continue;
            }

            success = false;
            GameLog.Error(
                nameof(WorldSiteRoot),
                $"WorldSitePlacementInvalidTerrain site={site.SiteId} placement={placement.PlacementId} unit={placement.UnitTypeId} cell=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={GetPlacementTerrainTag(placement)} canEnterWater={canEnterWater} reason={failureReason}");
        }

        if (relocated > 0 || heightSynced > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"WorldSitePlacementsTerrainReconciled site={site.SiteId} relocated={relocated} heightSynced={heightSynced}");
        }

        return success;
    }

    private bool ResolvePlacementCanEnterWater(WorldSiteUnitPlacement placement)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(placement?.UnitTypeId, out BattleUnitDefinition definition))
        {
            return false;
        }

        return definition.CanEnterWater;
    }

    private bool TrySyncPlacementSurfaceHeight(WorldSiteUnitPlacement placement)
    {
        if (placement == null || !TryGetPlacementSurface(placement, out GridCellSurface surface))
        {
            return false;
        }

        if (placement.CellHeight == surface.Height)
        {
            return false;
        }

        placement.CellHeight = surface.Height;
        return true;
    }

    private bool CanUsePlacement(WorldSiteUnitPlacement placement, bool canEnterWater)
    {
        return CanUsePlacementSurface(placement, canEnterWater, out _);
    }

    private bool CanUsePlacementSurface(WorldSiteUnitPlacement placement, bool canEnterWater, out string failureReason)
    {
        failureReason = "";
        if (placement == null)
        {
            failureReason = "missing_placement";
            return false;
        }

        if (_activeGridMap == null)
        {
            return true;
        }

        if (!TryGetPlacementSurface(placement, out GridCellSurface surface))
        {
            failureReason = "placement_surface_missing";
            return false;
        }

        if (!IsDeploymentCandidateSurface(surface))
        {
            failureReason = "placement_surface_blocked";
            return false;
        }

        if (!canEnterWater && BattleRuleQueries.IsWater(surface))
        {
            failureReason = "placement_surface_water";
            return false;
        }

        return true;
    }

    private bool TryRelocatePlacementForTerrain(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (site == null || placement == null || _deploymentCache == null)
        {
            failureReason = "deployment_cache_missing";
            return false;
        }

        IReadOnlyList<WorldSiteDeploymentCell> candidates = BuildRelocationCandidates(definition, placement, canEnterWater);
        foreach (WorldSiteDeploymentCell candidate in candidates)
        {
            if (IsDeploymentCandidateOccupied(site, candidate, placement.PlacementId))
            {
                continue;
            }

            Vector2I oldCell = new(placement.CellX, placement.CellY);
            int oldHeight = placement.CellHeight;
            placement.CellX = candidate.Cell.X;
            placement.CellY = candidate.Cell.Y;
            placement.CellHeight = candidate.Height;
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"WorldSitePlacementRelocatedForTerrain site={site.SiteId} placement={placement.PlacementId} unit={placement.UnitTypeId} from=({oldCell.X},{oldCell.Y},h={oldHeight}) to=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={candidate.TerrainTag} isWater={candidate.IsWater}");
            return true;
        }

        failureReason = "non_water_deployment_cell_unavailable";
        return false;
    }

    private string GetPlacementTerrainTag(WorldSiteUnitPlacement placement)
    {
        if (TryGetPlacementSurface(placement, out GridCellSurface surface))
        {
            return surface.TerrainTag ?? "";
        }

        return TryGetDeploymentCellForPlacement(placement, out WorldSiteDeploymentCell cell)
            ? cell.TerrainTag ?? ""
            : "";
    }

    private IReadOnlyList<WorldSiteDeploymentCell> BuildRelocationCandidates(
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater)
    {
        var result = new List<WorldSiteDeploymentCell>();
        var seen = new HashSet<GridSurfacePosition>();
        AddZoneRelocationCandidates(definition, placement, canEnterWater, result, seen);

        WorldSiteAttackDirection direction = placement.AttackDirection == WorldSiteAttackDirection.Any
            ? WorldSiteAttackDirection.Any
            : placement.AttackDirection;
        foreach (WorldSiteDeploymentCell candidate in _deploymentCache.GetCandidates(direction))
        {
            if (!CanUseDeploymentCell(candidate, canEnterWater))
            {
                continue;
            }

            var surfacePosition = new GridSurfacePosition(new GridPosition(candidate.Cell.X, candidate.Cell.Y), candidate.Height);
            if (seen.Add(surfacePosition))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private void AddZoneRelocationCandidates(
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater,
        List<WorldSiteDeploymentCell> result,
        HashSet<GridSurfacePosition> seen)
    {
        if (definition == null || placement == null || _activeGridMap == null)
        {
            return;
        }

        SiteDeploymentZoneDefinition zone = definition.DeploymentZones.FirstOrDefault(item => item.ZoneId == placement.ZoneId) ??
                                            _deploymentService.GetDefaultGarrisonZone(definition);
        if (zone?.Cells == null || zone.Cells.Count == 0)
        {
            return;
        }

        foreach (Vector2I cell in zone.Cells)
        {
            var position = new GridPosition(cell.X, cell.Y);
            if (!_activeGridMap.TryGetTopSurface(position, out GridCellSurface surface) ||
                !IsDeploymentCandidateSurface(surface))
            {
                continue;
            }

            var candidate = new WorldSiteDeploymentCell(
                new Vector2I(surface.Position.X, surface.Position.Y),
                surface.Height,
                surface.TerrainTag ?? "",
                BattleRuleQueries.IsWater(surface));
            if (!CanUseDeploymentCell(candidate, canEnterWater))
            {
                continue;
            }

            if (seen.Add(surface.SurfacePosition))
            {
                result.Add(candidate);
            }
        }
    }

    private bool TryGetPlacementSurface(WorldSiteUnitPlacement placement, out GridCellSurface surface)
    {
        surface = null;
        if (placement == null || _activeGridMap == null)
        {
            return false;
        }

        var position = new GridPosition(placement.CellX, placement.CellY);
        if (placement.CellHeight == 0 && _activeGridMap.TryGetTopSurface(position, out surface))
        {
            return true;
        }

        if (_activeGridMap.TryGetSurface(new GridSurfacePosition(position, placement.CellHeight), out surface))
        {
            return true;
        }

        return _activeGridMap.TryGetTopSurface(position, out surface);
    }

    private bool TryGetDeploymentCellForPlacement(WorldSiteUnitPlacement placement, out WorldSiteDeploymentCell cell)
    {
        cell = default;
        if (placement == null || _deploymentCache == null)
        {
            return false;
        }

        foreach (WorldSiteDeploymentCell candidate in _deploymentCache.CandidatesByDirection.Values.SelectMany(candidates => candidates))
        {
            if (candidate.Cell.X == placement.CellX &&
                candidate.Cell.Y == placement.CellY &&
                candidate.Height == placement.CellHeight)
            {
                cell = candidate;
                return true;
            }
        }

        foreach (WorldSiteDeploymentCell candidate in _deploymentCache.CandidatesByDirection.Values.SelectMany(candidates => candidates))
        {
            if (candidate.Cell.X == placement.CellX &&
                candidate.Cell.Y == placement.CellY)
            {
                cell = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsDeploymentCandidateOccupied(
        WorldSiteState site,
        WorldSiteDeploymentCell candidate,
        string ignorePlacementId)
    {
        return site.UnitPlacements.Any(placement =>
            placement.PlacementId != ignorePlacementId &&
            placement.CellX == candidate.Cell.X &&
            placement.CellY == candidate.Cell.Y);
    }

    private static IEnumerable<WorldSiteUnitPlacement> ResolveWorldSitePlacementsForForce(
        WorldSiteState site,
        BattleForceRequest force)
    {
        if (IsResidentGarrisonForceForSite(force, site.SiteId))
        {
            return site.UnitPlacements.Where(placement =>
                WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        if (IsResidentSitePlacementForce(force, site))
        {
            return site.UnitPlacements.Where(placement =>
                placement.PlacementId == force.SourceId &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        if (IsResidentPlayerArmySiteForce(force, site))
        {
            return site.UnitPlacements.Where(placement =>
                IsPlayerArmySitePlacement(placement) &&
                placement.SourceId == force.SourceId &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        string sourceKind = ResolveForceSourceKind(force);
        string sourceId = ResolveForceSourceId(force);
        return site.UnitPlacements.Where(placement =>
            !WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
            placement.UnitTypeId == force.UnitDefinitionId &&
            placement.SourceKind == sourceKind &&
            placement.SourceId == sourceId);
    }

    private static IEnumerable<BattleForceRequest> EnumerateRequestForces(BattleStartRequest request)
    {
        if (request == null)
        {
            return System.Array.Empty<BattleForceRequest>();
        }

        return (request.PlayerForces ?? new List<BattleForceRequest>())
            .Concat(request.EnemyForces ?? new List<BattleForceRequest>());
    }

    private static bool IsResidentGarrisonForceForSite(BattleForceRequest force, string siteId)
    {
        if (force == null || string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        return (force.SourceKind == "Garrison" || force.SourceKind == "DefenderSite") &&
               force.SourceId == siteId;
    }

    private static bool IsResidentWorldSiteForceForSite(BattleForceRequest force, WorldSiteState site)
    {
        return site != null &&
               (IsResidentGarrisonForceForSite(force, site.SiteId) ||
                IsResidentSitePlacementForce(force, site) ||
                IsResidentPlayerArmySiteForce(force, site));
    }

    private static bool IsResidentSitePlacementForce(BattleForceRequest force, WorldSiteState site)
    {
        return force != null &&
               site != null &&
               force.SourceKind == "SitePlacement" &&
               !string.IsNullOrWhiteSpace(force.SourceId) &&
               site.UnitPlacements.Any(placement =>
                   placement.PlacementId == force.SourceId &&
                   placement.UnitTypeId == force.UnitDefinitionId);
    }

    private static string ResolveForceSourceKind(BattleForceRequest force)
    {
        return string.IsNullOrWhiteSpace(force?.SourceKind) ? "BattleForce" : force.SourceKind;
    }

    private static string ResolveForceSourceId(BattleForceRequest force)
    {
        if (force == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        if (!string.IsNullOrWhiteSpace(force.ForceId))
        {
            return force.ForceId;
        }

        return force.UnitDefinitionId ?? "";
    }

    private void AddRequestedForces(
        IEnumerable<BattleForceRequest> forces,
        BattleFaction fallbackFaction,
        BattleStartRequest request,
        ISet<GridSurfacePosition> reservedDeploymentSurfaces)
    {
        foreach (BattleForceRequest force in forces ?? System.Array.Empty<BattleForceRequest>())
        {
            if (force.Count <= 0)
            {
                continue;
            }

            string corpsId = BuildCorpsId(force, fallbackFaction);
            string corpsDisplayName = BuildCorpsDisplayName(force, fallbackFaction);
            BattleUnitControlMode defaultControlMode = ResolveForceControlMode(force, fallbackFaction);

            for (int i = 0; i < force.Count; i++)
            {
                BattleForcePlacementRequest placement = i < force.PreferredPlacements.Count
                    ? force.PreferredPlacements[i]
                    : null;
                if (placement == null)
                {
                    GameLog.Error(
                        nameof(WorldSiteRoot),
                        $"Skip battle unit without WorldSiteState placement force={force.ForceId} unit={force.UnitDefinitionId} index={i}");
                    continue;
                }

                GridPosition fallbackPosition = new(placement.CellX, placement.CellY);
                BattleEntity entity = _battleUnitFactory.Create(force, i, fallbackFaction, fallbackPosition);
                if (entity == null)
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"Skip battle unit force={force.ForceId} unit={force.UnitDefinitionId} index={i}");
                    continue;
                }

                ApplyBattleRequestDeployment(entity, force, i, fallbackFaction, request, reservedDeploymentSurfaces);
                _unitRoot.AddChild(entity);
                RegisterBattleCorpsEntity(entity, corpsId, corpsDisplayName, fallbackFaction, defaultControlMode);
            }
        }
    }

    private void ClearBattleCorpsRuntime()
    {
        _battleCorpsStates.Clear();
        _battleEntityToCorpsId.Clear();
        _battleEntityControlModes.Clear();
    }

    private BattleUnitControlMode ResolveForceControlMode(BattleForceRequest force, BattleFaction fallbackFaction)
    {
        if (_battleUnitFactory.TryGetUnitDefinition(force?.UnitDefinitionId, out BattleUnitDefinition definition))
        {
            return definition.ControlMode;
        }

        return fallbackFaction == BattleFaction.Player
            ? BattleUnitControlMode.AutoByFaction
            : BattleUnitControlMode.Ai;
    }

    private void RegisterBattleCorpsEntity(
        BattleEntity entity,
        string corpsId,
        string corpsDisplayName,
        BattleFaction fallbackFaction,
        BattleUnitControlMode defaultControlMode)
    {
        if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
        {
            return;
        }

        if (!_battleCorpsStates.TryGetValue(corpsId, out BattleCorpsRuntimeState corpsState))
        {
            corpsState = new BattleCorpsRuntimeState
            {
                CorpsId = corpsId,
                DisplayName = corpsDisplayName,
                Faction = fallbackFaction,
                Command = fallbackFaction == BattleFaction.Player
                    ? BattleCorpsCommand.Assault
                    : BattleCorpsCommand.HoldLine
            };
            _battleCorpsStates[corpsId] = corpsState;
        }

        if (!corpsState.MemberEntityIds.Contains(entity.EntityId))
        {
            corpsState.MemberEntityIds.Add(entity.EntityId);
        }

        _battleEntityToCorpsId[entity.EntityId] = corpsId;
        _battleEntityControlModes[entity.EntityId] = defaultControlMode;
    }

    private static string BuildCorpsId(BattleForceRequest force, BattleFaction fallbackFaction)
    {
        string side = fallbackFaction.ToString().ToLowerInvariant();
        string sourceKind = ResolveForceSourceKind(force);
        string sourceId = ResolveForceSourceId(force);
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            sourceId = force?.ForceId;
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            sourceId = force?.UnitDefinitionId;
        }

        return $"{side}:{sourceKind}:{sourceId}";
    }

    private static string BuildCorpsDisplayName(BattleForceRequest force, BattleFaction fallbackFaction)
    {
        string sideLabel = fallbackFaction == BattleFaction.Player ? "我方" : "敌方";
        string sourceKind = ResolveForceSourceKind(force);
        string roleLabel = sourceKind switch
        {
            "PlayerArmy" => "主力",
            "SourceSite" => "驻地",
            "DefenderSite" or "Garrison" => "守备",
            "ThreatArmy" or "EnemyArmy" or "ThreatRule" => "袭击",
            _ => sourceKind
        };
        return $"{sideLabel}{roleLabel}";
    }

    private void FinalizeBattleCorpsControlOwnership()
    {
        IReadOnlyList<BattleEntity> snapshot = _unitRoot?.GetEntitiesSnapshot() ?? System.Array.Empty<BattleEntity>();
        Dictionary<string, BattleEntity> entitiesById = snapshot
            .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.EntityId))
            .GroupBy(entity => entity.EntityId)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);

        foreach (BattleCorpsRuntimeState corpsState in _battleCorpsStates.Values.Where(state => state.Faction == BattleFaction.Player))
        {
            corpsState.CommanderEntityId = "";
            bool commanderAssigned = false;
            foreach (string memberEntityId in corpsState.MemberEntityIds)
            {
                if (!entitiesById.TryGetValue(memberEntityId, out BattleEntity entity))
                {
                    continue;
                }

                BattleUnitControlMode controlMode = _battleEntityControlModes.TryGetValue(memberEntityId, out BattleUnitControlMode configuredControlMode)
                    ? configuredControlMode
                    : BattleUnitControlMode.AutoByFaction;

                bool selectable = controlMode switch
                {
                    BattleUnitControlMode.Player => true,
                    BattleUnitControlMode.Ai => false,
                    BattleUnitControlMode.Passive => false,
                    _ => !commanderAssigned
                };

                if (selectable && !commanderAssigned)
                {
                    commanderAssigned = true;
                    corpsState.CommanderEntityId = memberEntityId;
                }

                SetEntitySelectable(entity, selectable);
            }
        }
    }

    private static void SetEntitySelectable(BattleEntity entity, bool selectable)
    {
        SelectableComponent selectableComponent = entity?.GetComponent<SelectableComponent>();
        if (selectableComponent != null)
        {
            selectableComponent.IsSelectable = selectable;
        }
    }

    private IReadOnlyList<BattleEntity> GetAlliedAutoActionOrder()
    {
        IReadOnlyList<BattleEntity> snapshot = GetBattleEntitiesSnapshot();
        var entitiesById = snapshot
            .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.EntityId))
            .GroupBy(entity => entity.EntityId)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);
        var result = new List<BattleEntity>();
        var includedEntityIds = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (BattleCorpsRuntimeState corpsState in _battleCorpsStates.Values
                     .Where(state => state.Faction == BattleFaction.Player)
                     .OrderBy(state => state.CorpsId))
        {
            foreach (string memberEntityId in corpsState.MemberEntityIds)
            {
                if (!entitiesById.TryGetValue(memberEntityId, out BattleEntity entity) ||
                    !IsAutoControlledPlayerEntity(entity) ||
                    !includedEntityIds.Add(memberEntityId))
                {
                    continue;
                }

                result.Add(entity);
            }
        }

        foreach (BattleEntity entity in snapshot.Where(IsAutoControlledPlayerEntity))
        {
            if (string.IsNullOrWhiteSpace(entity.EntityId) ||
                !includedEntityIds.Add(entity.EntityId))
            {
                continue;
            }

            result.Add(entity);
        }

        return result;
    }

    private bool IsAutoControlledPlayerEntity(BattleEntity entity)
    {
        if (entity == null ||
            BattleRuleQueries.IsDefeated(entity) ||
            entity.GetComponent<FactionComponent>()?.Faction != BattleFaction.Player ||
            entity.GetComponent<SelectableComponent>() is not { IsSelectable: false })
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(entity.EntityId))
        {
            return true;
        }

        BattleUnitControlMode controlMode = _battleEntityControlModes.TryGetValue(entity.EntityId, out BattleUnitControlMode configuredControlMode)
            ? configuredControlMode
            : BattleUnitControlMode.AutoByFaction;
        return controlMode != BattleUnitControlMode.Passive;
    }

    private string ResolveCorpsLabelForEntity(BattleEntity entity)
    {
        if (entity == null ||
            string.IsNullOrWhiteSpace(entity.EntityId) ||
            !_battleEntityToCorpsId.TryGetValue(entity.EntityId, out string corpsId) ||
            !_battleCorpsStates.TryGetValue(corpsId, out BattleCorpsRuntimeState corpsState))
        {
            return "";
        }

        return corpsState.DisplayName ?? "";
    }

    private string ResolveCorpsCommandLabelForEntity(BattleEntity entity)
    {
        if (entity == null ||
            entity.GetComponent<FactionComponent>()?.Faction != BattleFaction.Player ||
            entity.GetComponent<SelectableComponent>() is not { IsSelectable: true } ||
            string.IsNullOrWhiteSpace(entity.EntityId) ||
            !_battleEntityToCorpsId.TryGetValue(entity.EntityId, out string corpsId) ||
            !_battleCorpsStates.TryGetValue(corpsId, out BattleCorpsRuntimeState corpsState) ||
            corpsState.Faction != BattleFaction.Player)
        {
            return "";
        }

        return BattleCorpsCommandLabels.ToDisplayText(corpsState.Command);
    }

    private string CycleCorpsCommandForEntity(BattleEntity entity)
    {
        if (entity == null ||
            string.IsNullOrWhiteSpace(entity.EntityId) ||
            !_battleEntityToCorpsId.TryGetValue(entity.EntityId, out string corpsId) ||
            !_battleCorpsStates.TryGetValue(corpsId, out BattleCorpsRuntimeState corpsState) ||
            corpsState.Faction != BattleFaction.Player)
        {
            return "该单位没有可用兵团指令";
        }

        corpsState.Command = BattleCorpsCommandLabels.Next(corpsState.Command);
        string commandLabel = BattleCorpsCommandLabels.ToDisplayText(corpsState.Command);
        return $"{corpsState.DisplayName} 指令 -> {commandLabel}";
    }

    private BattleCorpsCommand ResolveCorpsCommandForEntity(BattleEntity entity)
    {
        if (entity == null ||
            string.IsNullOrWhiteSpace(entity.EntityId) ||
            !_battleEntityToCorpsId.TryGetValue(entity.EntityId, out string corpsId) ||
            !_battleCorpsStates.TryGetValue(corpsId, out BattleCorpsRuntimeState corpsState))
        {
            return BattleCorpsCommand.Assault;
        }

        return corpsState.Command;
    }

    private System.Threading.Tasks.Task ExecuteAlliedAutoAction(BattleEntity entity)
    {
        if (_intentController == null || entity == null)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        BattleCorpsCommand command = ResolveCorpsCommandForEntity(entity);
        string corpsLabel = ResolveCorpsLabelForEntity(entity);
        if (string.IsNullOrWhiteSpace(corpsLabel))
        {
            corpsLabel = "我方兵团";
        }

        return _intentController.ExecuteAlliedAutoAction(entity, command, corpsLabel);
    }

    private void ApplyBattleRequestDeployment(
        BattleEntity entity,
        BattleForceRequest force,
        int forceIndex,
        BattleFaction fallbackFaction,
        BattleStartRequest request,
        ISet<GridSurfacePosition> reservedDeploymentSurfaces)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        BattleForcePlacementRequest placement = forceIndex < (force?.PreferredPlacements?.Count ?? 0)
            ? force.PreferredPlacements[forceIndex]
            : null;
        if (placement != null)
        {
            gridOccupant.GridX = placement.CellX;
            gridOccupant.GridY = placement.CellY;
            if (placement.CellHeight > 0)
            {
                gridOccupant.GridHeight = placement.CellHeight;
                gridOccupant.UseExplicitHeight = true;
            }

            ResolveEntitySurfaceHeight(gridOccupant);
            reservedDeploymentSurfaces?.Add(gridOccupant.SurfacePosition);
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"Battle unit placed from WorldSiteState entity={entity.EntityId} force={force?.ForceId} placement={placement.PlacementId} surface={gridOccupant.SurfacePosition}");
            return;
        }

        ResolveEntitySurfaceHeight(gridOccupant);
        reservedDeploymentSurfaces?.Add(gridOccupant.SurfacePosition);
        GameLog.Error(
            nameof(WorldSiteRoot),
            $"Battle unit missing WorldSiteState placement entity={entity.EntityId} force={force?.ForceId} faction={fallbackFaction} fallbackSurface={gridOccupant.SurfacePosition}");
    }

    private void ClearBattleEntities()
    {
        if (_unitRoot == null)
        {
            ClearBattleCorpsRuntime();
            return;
        }

        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            entity.GetParent()?.RemoveChild(entity);
            entity.QueueFree();
        }

        ClearBattleCorpsRuntime();
    }

    private static WorldSiteAttackDirection ResolveForceDeploymentDirection(
        BattleStartRequest request,
        BattleForceRequest force,
        BattleFaction fallbackFaction)
    {
        WorldSiteAttackDirection attackDirection = request?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (attackDirection == WorldSiteAttackDirection.Any)
        {
            return WorldSiteAttackDirection.Any;
        }

        return IsAttackingForce(request, force, fallbackFaction)
            ? attackDirection
            : GetOppositeDirection(attackDirection);
    }

    private static bool IsAttackingForce(
        BattleStartRequest request,
        BattleForceRequest force,
        BattleFaction fallbackFaction)
    {
        if (request != null &&
            !string.IsNullOrWhiteSpace(force?.FactionId) &&
            !string.IsNullOrWhiteSpace(request.AttackerFactionId))
        {
            return force.FactionId == request.AttackerFactionId;
        }

        return request?.BattleKind switch
        {
            BattleKind.AssaultSite => fallbackFaction == BattleFaction.Player,
            BattleKind.DefenseRaid => fallbackFaction == BattleFaction.Enemy,
            BattleKind.FieldIntercept => fallbackFaction == BattleFaction.Player,
            _ => fallbackFaction == BattleFaction.Enemy
        };
    }

    private static WorldSiteAttackDirection GetOppositeDirection(WorldSiteAttackDirection direction)
    {
        return direction switch
        {
            WorldSiteAttackDirection.North => WorldSiteAttackDirection.South,
            WorldSiteAttackDirection.South => WorldSiteAttackDirection.North,
            WorldSiteAttackDirection.West => WorldSiteAttackDirection.East,
            WorldSiteAttackDirection.East => WorldSiteAttackDirection.West,
            _ => WorldSiteAttackDirection.Any
        };
    }

    private static BattleEntranceRequest ResolveForceEntrance(
        BattleStartRequest request,
        BattleForceRequest force,
        WorldSiteAttackDirection desiredDirection)
    {
        if (request?.AvailableEntrances == null || request.AvailableEntrances.Count == 0)
        {
            return null;
        }

        BattleEntranceRequest[] candidates = request.AvailableEntrances
            .Where(entrance => IsEntranceForForce(entrance, force))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(force?.PreferredEntranceId))
        {
            BattleEntranceRequest preferred = candidates.FirstOrDefault(entrance => entrance.EntranceId == force.PreferredEntranceId);
            if (preferred != null)
            {
                return preferred;
            }
        }

        if (desiredDirection != WorldSiteAttackDirection.Any)
        {
            BattleEntranceRequest exact = candidates.FirstOrDefault(entrance => entrance.Direction == desiredDirection);
            if (exact != null)
            {
                return exact;
            }
        }

        BattleEntranceRequest anyEntrance = candidates.FirstOrDefault(entrance => entrance.Direction == WorldSiteAttackDirection.Any);
        if (anyEntrance != null)
        {
            return anyEntrance;
        }

        return desiredDirection == WorldSiteAttackDirection.Any
            ? candidates.FirstOrDefault()
            : null;
    }

    private static bool IsEntranceForForce(BattleEntranceRequest entrance, BattleForceRequest force)
    {
        if (entrance == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entrance.FactionId) ||
               string.IsNullOrWhiteSpace(force?.FactionId) ||
               entrance.FactionId == force.FactionId;
    }

    private static IEnumerable<GridCellSurface> OrderDeploymentSurfaceCandidates(
        IReadOnlyCollection<GridCellSurface> candidates,
        WorldSiteAttackDirection direction)
    {
        int minX = candidates.Min(surface => surface.Position.X);
        int maxX = candidates.Max(surface => surface.Position.X);
        int minY = candidates.Min(surface => surface.Position.Y);
        int maxY = candidates.Max(surface => surface.Position.Y);
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        return direction switch
        {
            WorldSiteAttackDirection.North => candidates
                .OrderBy(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.South => candidates
                .OrderByDescending(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.West => candidates
                .OrderBy(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.East => candidates
                .OrderByDescending(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            _ => candidates
                .OrderBy(surface => System.Math.Abs(surface.Position.X - centerX) + System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height)
        };
    }

    private void ApplyBattleModifiers(BattleStartRequest request)
    {
        int towerSupportCount = request.BattleModifiers.Count(modifier => modifier.Type == "tower_support" && modifier.Uses > 0);
        if (towerSupportCount > 0)
        {
            int damage = towerSupportCount * 2;
            BattleEntity target = _unitRoot.GetEntitiesSnapshot()
                .FirstOrDefault(entity =>
                    entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
                    !BattleRuleQueries.IsDefeated(entity));
            if (target != null)
            {
                int applied = target.GetComponent<HealthComponent>()?.ApplyDamage(damage) ?? 0;
                if (BattleRuleQueries.IsDefeated(target))
                {
                    _unitRoot.MarkEntityDefeated(target);
                }

                _hudRoot?.ShowActionHint($"防御塔开场支援，对 {target.DisplayName} 造成 {applied} 伤害");
                GameLog.Info(nameof(WorldSiteRoot), $"Tower support applied target={target.EntityId} damage={applied} supports={towerSupportCount}");
            }
        }

        ApplyWorldBattlePhaseModifiers(request);
    }

    private void ApplyWorldBattlePhaseModifiers(BattleStartRequest request)
    {
        foreach (BattleModifier modifier in request.BattleModifiers.Where(modifier =>
                     modifier.Type == "world_battle_phase" && modifier.Uses > 0))
        {
            int playerDamage = modifier.Values.TryGetValue("player_damage", out int playerValue) ? playerValue : 0;
            int enemyDamage = modifier.Values.TryGetValue("enemy_damage", out int enemyValue) ? enemyValue : 0;
            if (playerDamage > 0)
            {
                ApplyWorldBattlePhaseDamage(BattleFaction.Player, playerDamage, modifier.SourceId);
            }

            if (enemyDamage > 0)
            {
                ApplyWorldBattlePhaseDamage(BattleFaction.Enemy, enemyDamage, modifier.SourceId);
            }
        }
    }

    private void ApplyWorldBattlePhaseDamage(BattleFaction faction, int damage, string sourceId)
    {
        if (damage <= 0)
        {
            return;
        }

        BattleEntity target = _unitRoot.GetEntitiesSnapshot()
            .FirstOrDefault(entity =>
                entity.GetComponent<FactionComponent>()?.Faction == faction &&
                !BattleRuleQueries.IsDefeated(entity));
        if (target == null)
        {
            return;
        }

        int applied = target.GetComponent<HealthComponent>()?.ApplyDamage(damage) ?? 0;
        if (BattleRuleQueries.IsDefeated(target))
        {
            _unitRoot.MarkEntityDefeated(target);
        }

        _hudRoot?.ShowActionHint($"世界战斗阶段压力，对 {target.DisplayName} 造成 {applied} 伤害");
        GameLog.Info(nameof(WorldSiteRoot), $"WorldBattlePhaseModifierApplied source={sourceId} target={target.EntityId} faction={faction} damage={applied}");
    }

    private void PlaceBattleEntitiesOnGrid()
    {
        if (_activeSiteMap is not BattleMapView || _unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Skip entity placement activeSiteMapIsBattleMap={_activeSiteMap is BattleMapView} unitRoot={_unitRoot != null}");
            return;
        }

        if (_coordinateLayer == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Skip entity placement because coordinate layer is missing.");
            return;
        }

        int placedCount = 0;
        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
            if (gridOccupant == null)
            {
                GameLog.Info(nameof(WorldSiteRoot), $"Entity has no grid occupant entity={entity.EntityId} name={entity.DisplayName}");
                continue;
            }

            ResolveEntitySurfaceHeight(gridOccupant);
            var cell = new Vector2I(gridOccupant.GridX, gridOccupant.GridY);
            entity.GlobalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
            ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
            placedCount++;
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"Placed entity id={entity.EntityId} name={entity.DisplayName} surface={gridOccupant.SurfacePosition} global={entity.GlobalPosition} {DescribeGridCell(gridOccupant.Position)} {DescribeGridSurface(gridOccupant.SurfacePosition)}");

            WarnIfEntityStartsOnInvalidSurface(entity, gridOccupant.SurfacePosition);
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Entity placement complete count={placedCount}");
    }

    private void ResolveEntitySurfaceHeight(GridOccupantComponent gridOccupant)
    {
        if (gridOccupant == null || gridOccupant.UseExplicitHeight || _activeGridMap == null)
        {
            return;
        }

        if (_activeGridMap.TryGetTopSurfacePosition(gridOccupant.Position, out GridSurfacePosition topSurface))
        {
            gridOccupant.GridHeight = topSurface.Height;
            return;
        }

        if (_activeGridMap.TryGetCell(gridOccupant.Position, out GridCell cell))
        {
            gridOccupant.GridHeight = cell.Height;
        }
    }

    private void ApplyEntityRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition)
    {
        if (entity == null)
        {
            return;
        }

        int zIndex = BattleRenderSortPolicy.GetUnitZIndex(surfacePosition.Height);
        bool suppressPresentationRaise = false;
        if (_activeSiteMap is BattleMapView battleMapView &&
            battleMapView.RenderSortCache?.TryGetYSortOriginUnitZIndex(surfacePosition, out int ySortOriginZIndex) == true)
        {
            zIndex = ySortOriginZIndex;
            suppressPresentationRaise = true;
        }

        BattleUnitPresentationComponent presentation = entity.GetComponent<BattleUnitPresentationComponent>();
        if (presentation != null)
        {
            presentation.SetMapSortZIndex(zIndex, suppressPresentationRaise);
            return;
        }

        entity.ZIndex = zIndex;
    }

    private string DescribeGridCell(GridPosition position)
    {
        if (_activeGridMap == null)
        {
            return "grid=missing";
        }

        if (!_activeGridMap.TryGetCell(position, out GridCell cell))
        {
            return "cell=missing";
        }

        string terrain = string.IsNullOrWhiteSpace(cell.TerrainTag) ? "-" : cell.TerrainTag;
        return $"height={cell.Height} terrain={terrain} walkable={cell.IsWalkable} moveCost={cell.MoveCost} foundation={cell.HasFoundation} obstacle={cell.IsObstacle}";
    }

    private string DescribeGridSurface(GridSurfacePosition position)
    {
        if (_activeGridMap == null)
        {
            return "surfaceGrid=missing";
        }

        if (!_activeGridMap.TryGetSurface(position, out GridCellSurface surface))
        {
            return "surface=missing";
        }

        string terrain = string.IsNullOrWhiteSpace(surface.TerrainTag) ? "-" : surface.TerrainTag;
        return $"surfaceTerrain={terrain} surfaceWalkable={surface.IsWalkable} surfaceMoveCost={surface.MoveCost} surfaceFoundation={surface.HasFoundation}";
    }

    private void WarnIfEntityStartsOnInvalidSurface(BattleEntity entity, GridSurfacePosition position)
    {
        if (_activeGridMap == null ||
            !_activeGridMap.TryGetSurface(position, out GridCellSurface surface) ||
            IsValidMovementDestination(entity, surface))
        {
            return;
        }

        GameLog.Warn(
            nameof(WorldSiteRoot),
            $"Entity starts on invalid movement surface id={entity.EntityId} name={entity.DisplayName} surface={position} {DescribeGridSurface(position)} nearest={DescribeNearestValidMovementSurfaces(entity, position, 5)}");
    }

    private string DescribeNearestValidMovementSurfaces(BattleEntity entity, GridSurfacePosition origin, int count)
    {
        ISet<GridSurfacePosition> blockedSurfaces = BuildBlockedMovementSurfaces(entity);
        GridSurfacePosition[] candidates = _activeGridMap.Surfaces.Values
            .Where(surface => !blockedSurfaces.Contains(surface.SurfacePosition) && IsValidMovementDestination(entity, surface))
            .OrderBy(surface => BattleRuleQueries.GetManhattanDistance(origin.Position, surface.Position))
            .ThenBy(surface => surface.Height)
            .ThenBy(surface => surface.Position.Y)
            .ThenBy(surface => surface.Position.X)
            .Take(count)
            .Select(surface => surface.SurfacePosition)
            .ToArray();

        return candidates.Length == 0
            ? "none"
            : string.Join(", ", candidates.Select(position => $"{position} {DescribeGridSurface(position)}"));
    }

    private bool IsValidMovementDestination(BattleEntity entity, GridCellSurface surface)
    {
        return surface is { IsWalkable: true, MoveCost: > 0 } &&
               _activeGridMap?.IsTopSurface(surface.SurfacePosition) == true &&
               BattleRuleQueries.CanEnterSurface(entity, surface);
    }

    private BattleActionResult ExecuteActionRequest(BattleActionRequest request)
    {
        BattleActionResult result = _actionExecutor.Execute(CreateActionExecutionContext(), request);
        if (result.Success)
        {
            _unitRoot?.PlayActionResultAnimation(result);
            MarkBattleStateChanged();
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Action request executed kind={request?.Kind} actor={request?.Actor?.EntityId} target={request?.Target?.EntityId} destination={request?.Destination} success={result.Success} message={result.Message}");
        return result;
    }

    private void ShowBattleEntityInHud(BattleEntity entity)
    {
        _turnController?.RefreshTurnQueue(entity);
        _hudRoot?.ShowEntity(entity);
    }

    private void OnTurnQueueUpdated(
        int round,
        BattleFaction activeFaction,
        BattleEntity activeEntity,
        IReadOnlyList<BattleEntity> queue)
    {
        _hudRoot?.ShowTurnQueue(round, activeFaction, activeEntity, queue);
        if (activeEntity != null)
        {
            _battleCamera?.FollowActionEntityIfNeeded(activeEntity);
        }
    }

    private BattleActionExecutionContext CreateActionExecutionContext()
    {
        System.Action<BattleEntity, IReadOnlyList<GridSurfacePosition>> moveEntityTo =
            _unitRoot == null ? (_, _) => { } : (entity, path) => _unitRoot.MoveEntityTo(entity, path);

        return new BattleActionExecutionContext(
            _activeGridMap,
            GetBattleEntitiesSnapshot(),
            moveEntityTo,
            entity => _turnController?.HandleEntityDefeated(entity));
    }

    private BattleAiContext CreateAiContext()
    {
        return new BattleAiContext(_activeGridMap, GetBattleEntitiesSnapshot());
    }

    private void OnBattleEnded(BattleOutcome outcome)
    {
        if (_battleEndHandled)
        {
            return;
        }

        _battleEndHandled = true;
        BattleSessionResult sessionResult = BattleSessionHandoff.CompleteBattle(outcome);
        BattleStartRequest request = null;
        BattleResult battleResult = null;
        WorldActionResult applyResult = null;
        IReadOnlyList<WorldSiteLivePlacementSnapshot> livePlacementSnapshots = System.Array.Empty<WorldSiteLivePlacementSnapshot>();

        if (BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest consumedRequest, out BattleResult consumedResult))
        {
            request = consumedRequest;
            battleResult = consumedResult;
            livePlacementSnapshots = CaptureLivePlacementSnapshots(request);
            PopulateLiveBattleForceResults(request, battleResult);
            applyResult = ApplyBattleResultToWorld(request, battleResult);
            ReconcileWorldSitePlacementsAfterBattle(request, livePlacementSnapshots, outcome);
        }

        string returnScenePath = sessionResult?.ReturnScenePath ?? request?.ReturnScenePath ?? "";
        GameLog.Info(nameof(WorldSiteRoot), $"Battle ended outcome={outcome} encounter={request?.EncounterId ?? sessionResult?.EncounterId} returnScene={returnScenePath}");
        SwitchToNonBattleUi(outcome, request, applyResult, returnScenePath);
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

    private void MarkBattleStateChanged()
    {
        unchecked
        {
            _battleStateVersion++;
        }

        _previewController?.InvalidateHoverIntentPreview();
    }

    private IReadOnlyList<WorldSiteLivePlacementSnapshot> CaptureLivePlacementSnapshots(BattleStartRequest request)
    {
        if (request == null || _unitRoot == null)
        {
            return System.Array.Empty<WorldSiteLivePlacementSnapshot>();
        }

        var snapshots = new List<WorldSiteLivePlacementSnapshot>();
        IReadOnlyList<BattleEntity> entities = GetBattleEntitiesSnapshot();
        foreach (BattleForceRequest force in EnumerateBattleForces(request))
        {
            if (force == null ||
                force.PreferredPlacements == null ||
                force.PreferredPlacements.Count == 0)
            {
                continue;
            }

            int count = System.Math.Min(force.Count, force.PreferredPlacements.Count);
            for (int index = 0; index < count; index++)
            {
                BattleForcePlacementRequest requestedPlacement = force.PreferredPlacements[index];
                if (string.IsNullOrWhiteSpace(requestedPlacement?.PlacementId))
                {
                    continue;
                }

                BattleEntity entity = FindBattleForceEntity(entities, force, index);
                GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
                if (entity == null ||
                    BattleRuleQueries.IsDefeated(entity) ||
                    gridOccupant == null)
                {
                    continue;
                }

                snapshots.Add(new WorldSiteLivePlacementSnapshot
                {
                    PlacementId = requestedPlacement.PlacementId,
                    UnitTypeId = force.UnitDefinitionId,
                    UnitIndex = index + 1,
                    Faction = entity.GetComponent<FactionComponent>()?.Faction ?? BattleFaction.Neutral,
                    CellX = gridOccupant.GridX,
                    CellY = gridOccupant.GridY,
                    CellHeight = gridOccupant.GridHeight
                });
            }
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Live battle placement snapshots captured request={request.RequestId} count={snapshots.Count}");
        return snapshots;
    }

    private void PopulateLiveBattleForceResults(BattleStartRequest request, BattleResult result)
    {
        if (request == null || result == null)
        {
            return;
        }

        result.ForceResults.Clear();
        IReadOnlyList<BattleEntity> entities = GetBattleEntitiesSnapshot();
        foreach (BattleForceRequest force in EnumerateBattleForces(request))
        {
            if (force == null || force.Count <= 0 || string.IsNullOrWhiteSpace(force.UnitDefinitionId))
            {
                continue;
            }

            int survived = 0;
            for (int index = 0; index < force.Count; index++)
            {
                BattleEntity entity = FindBattleForceEntity(entities, force, index);
                if (entity != null && !BattleRuleQueries.IsDefeated(entity))
                {
                    survived++;
                }
            }

            result.ForceResults.Add(new BattleForceResult
            {
                ForceId = force.ForceId,
                SourceKind = force.SourceKind,
                SourceId = force.SourceId,
                UnitDefinitionId = force.UnitDefinitionId,
                InitialCount = force.Count,
                SurvivedCount = survived,
                DefeatedCount = System.Math.Max(0, force.Count - survived)
            });
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Live battle force results captured request={request.RequestId} count={result.ForceResults.Count} results={FormatBattleForceResultsForLog(result.ForceResults)}");
    }

    private void PersistLivePlacementSnapshots(
        BattleStartRequest request,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots)
    {
        if (request == null || snapshots == null || snapshots.Count == 0)
        {
            return;
        }

        StrategicWorldRuntime.EnsureInitialized();
        string siteId = ResolveRequestSiteId(request);
        if (string.IsNullOrWhiteSpace(siteId) ||
            StrategicWorldRuntime.State.SiteStates.TryGetValue(siteId, out WorldSiteState site) != true)
        {
            return;
        }

        int updated = ApplyLiveSnapshotsToMatchingPlacements(site, snapshots, null);
        if (updated > 0)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"Persisted live battle placements site={site.SiteId} request={request.RequestId} updated={updated}");
        }
    }

    private void ReconcileWorldSitePlacementsAfterBattle(
        BattleStartRequest request,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        BattleOutcome outcome)
    {
        if (request == null)
        {
            return;
        }

        StrategicWorldRuntime.EnsureInitialized();
        string siteId = ResolveRequestSiteId(request);
        if (string.IsNullOrWhiteSpace(siteId) ||
            StrategicWorldRuntime.State.SiteStates.TryGetValue(siteId, out WorldSiteState site) != true)
        {
            return;
        }

        WorldSiteDefinition definition = ResolveSiteDefinition(site.SiteId);
        if (definition == null)
        {
            return;
        }

        _deploymentService.EnsureGarrisonPlacements(site, definition);
        var usedSnapshotPlacementIds = new HashSet<string>();
        int matched = 0;
        int converted = 0;
        matched = ApplyLiveSnapshotsToMatchingPlacements(site, snapshots, usedSnapshotPlacementIds);
        converted = ApplyLiveSnapshotsToOwnerGarrisons(site, snapshots, usedSnapshotPlacementIds);
        EnsureSitePlacementsRespectTerrain(site, definition);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"WorldSitePlacementsReconciledAfterBattle site={site.SiteId} request={request.RequestId} snapshots={snapshots?.Count ?? 0} matched={matched} converted={converted} remaining={site.UnitPlacements.Count}");
    }

    private static int ApplyLiveSnapshotsToMatchingPlacements(
        WorldSiteState site,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        ISet<string> usedSnapshotPlacementIds)
    {
        if (site == null || snapshots == null || snapshots.Count == 0)
        {
            return 0;
        }

        int updated = 0;
        foreach (WorldSiteLivePlacementSnapshot snapshot in snapshots)
        {
            WorldSiteUnitPlacement placement = site.UnitPlacements
                .FirstOrDefault(item => item.PlacementId == snapshot.PlacementId);
            if (placement == null)
            {
                continue;
            }

            ApplySnapshotToPlacement(placement, snapshot);
            if (WorldSiteDeploymentService.IsGarrisonPlacement(placement))
            {
                usedSnapshotPlacementIds?.Add(snapshot.PlacementId);
            }

            updated++;
        }

        return updated;
    }

    private int ApplyLiveSnapshotsToOwnerGarrisons(
        WorldSiteState site,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        ISet<string> usedSnapshotPlacementIds)
    {
        if (site == null || snapshots == null || snapshots.Count == 0)
        {
            return 0;
        }

        BattleFaction ownerFaction = ResolveBattleFaction(site.OwnerFactionId);
        int updated = 0;
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements
                     .Where(WorldSiteDeploymentService.IsGarrisonPlacement)
                     .OrderBy(item => item.UnitTypeId)
                     .ThenBy(item => item.UnitIndex))
        {
            WorldSiteLivePlacementSnapshot snapshot = snapshots
                .Where(item =>
                    item.Faction == ownerFaction &&
                    item.UnitTypeId == placement.UnitTypeId &&
                    usedSnapshotPlacementIds?.Contains(item.PlacementId) != true)
                .OrderBy(item => item.UnitIndex)
                .ThenBy(item => item.PlacementId)
                .FirstOrDefault();
            if (snapshot == null)
            {
                continue;
            }

            ApplySnapshotToPlacement(placement, snapshot);
            usedSnapshotPlacementIds?.Add(snapshot.PlacementId);
            updated++;
        }

        return updated;
    }

    private static void ApplySnapshotToPlacement(
        WorldSiteUnitPlacement placement,
        WorldSiteLivePlacementSnapshot snapshot)
    {
        placement.CellX = snapshot.CellX;
        placement.CellY = snapshot.CellY;
        placement.CellHeight = snapshot.CellHeight;
    }

    private static IEnumerable<BattleForceRequest> EnumerateBattleForces(BattleStartRequest request)
    {
        if (request.PlayerForces != null)
        {
            foreach (BattleForceRequest force in request.PlayerForces)
            {
                yield return force;
            }
        }

        if (request.EnemyForces != null)
        {
            foreach (BattleForceRequest force in request.EnemyForces)
            {
                yield return force;
            }
        }
    }

    private static BattleEntity FindBattleForceEntity(
        IEnumerable<BattleEntity> entities,
        BattleForceRequest force,
        int forceIndex)
    {
        string entityId = BuildBattleForceEntityId(force, forceIndex);
        return entities.FirstOrDefault(entity =>
            entity != null &&
            string.Equals(entity.EntityId, entityId, System.StringComparison.Ordinal));
    }

    private static string BuildBattleForceEntityId(BattleForceRequest force, int forceIndex)
    {
        string source = string.IsNullOrWhiteSpace(force?.ForceId)
            ? force?.UnitDefinitionId
            : force.ForceId;
        return $"{source}:{forceIndex + 1}";
    }

    private IReadOnlyList<BattleEntity> GetBattleEntitiesSnapshot()
    {
        return _unitRoot == null
            ? System.Array.Empty<BattleEntity>()
            : _unitRoot.GetEntitiesSnapshot();
    }

    private ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(BattleEntity movingEntity)
    {
        return _unitRoot?.BuildBlockedMovementSurfaces(movingEntity) ?? new HashSet<GridSurfacePosition>();
    }

    private bool TryGetMouseGridPosition(out GridPosition position)
    {
        position = default;

        if (_coordinateLayer == null || _activeGridMap == null)
        {
            return false;
        }

        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(GetGlobalMousePosition()));
        position = new GridPosition(tilePosition.X, tilePosition.Y);
        return _activeGridMap.TryGetCell(position, out _);
    }

    private bool TryGetCellGlobalPosition(GridPosition position, out Vector2 globalPosition)
    {
        globalPosition = default;

        if (_coordinateLayer == null)
        {
            return false;
        }

        var cell = new Vector2I(position.X, position.Y);
        globalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
        return true;
    }

    public BattleEntity FindEntityAt(GridPosition position)
    {
        return _unitRoot?.FindEntityAt(position);
    }

    private WorldActionResult ApplyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)
    {
        if (request == null || battleResult == null || battleResult.BattleKind == BattleKind.Unknown)
        {
            return new WorldActionResult
            {
                Success = true,
                ActionId = "battle_result",
                Message = "战斗已结束，当前战斗没有绑定战略世界结算。"
            };
        }

        StrategicWorldRuntime.EnsureInitialized();
        WorldActionResult applyResult = _worldBattleResultApplier.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            battleResult);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        return applyResult;
    }

    private void BuildSiteHud()
    {
        if (_siteHudRoot != null)
        {
            return;
        }

        Node canvasLayer = _hudRoot?.GetParent() ?? GetNodeOrNull<Node>("CanvasLayer") ?? this;
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

        _siteHudTopBar = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "SiteTopBar",
            nameof(WorldSiteRoot));
        _sitePeacetimePanel = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "SitePeacetimePanel",
            nameof(WorldSiteRoot));
        ApplySiteHudFullRect("bound");
        _siteHudTitle = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SiteTopBar/TopMargin/TopBox/SiteHudTitle",
            nameof(WorldSiteRoot));
        _siteResourceLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SiteTopBar/TopMargin/TopBox/SiteResourceLabel",
            nameof(WorldSiteRoot));
        _returnMapButton = GameUiSceneFactory.GetRequiredNode<Button>(
            _siteHudRoot,
            "SiteTopBar/TopMargin/TopBox/ReturnMapButton",
            nameof(WorldSiteRoot));
        _siteHudBody = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteHudBody",
            nameof(WorldSiteRoot));
        _siteSelectionLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteSelectionLabel",
            nameof(WorldSiteRoot));
        _siteFacilityBuildCard = GameUiSceneFactory.GetRequiredNode<Control>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/BuildCard",
            nameof(WorldSiteRoot));
        _siteFacilityBuildTitle = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/BuildTitle",
            nameof(WorldSiteRoot));
        _siteFacilityBuildList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/SiteFacilityBuildList",
            nameof(WorldSiteRoot));
        _siteFacilityList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/SiteFacilityList",
            nameof(WorldSiteRoot));
        _siteGarrisonList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/SiteGarrisonList",
            nameof(WorldSiteRoot));
        _siteThreatList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/SiteThreatList",
            nameof(WorldSiteRoot));
        _siteActionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteActionList",
            nameof(WorldSiteRoot));
        _siteNoticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteNoticeLabel",
            nameof(WorldSiteRoot));
        Label operationHintLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SiteTopBar/TopMargin/TopBox/SiteOperationHintLabel",
            nameof(WorldSiteRoot));
        Label facilityTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/FacilityTitle",
            nameof(WorldSiteRoot));
        Label garrisonTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/GarrisonTitle",
            nameof(WorldSiteRoot));
        Label threatTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/ThreatTitle",
            nameof(WorldSiteRoot));
        Label actionTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/ActionTitle",
            nameof(WorldSiteRoot));
        Label noticeTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/NoticeTitle",
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

        RefreshSiteManagementUi(applyResult?.Message, outcome);
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

        _sitePeacetimePanel.AnchorLeft = 1.0f;
        _sitePeacetimePanel.AnchorTop = 0.0f;
        _sitePeacetimePanel.AnchorRight = 1.0f;
        _sitePeacetimePanel.AnchorBottom = 1.0f;
        _sitePeacetimePanel.OffsetLeft = -544.0f;
        _sitePeacetimePanel.OffsetTop = 82.0f;
        _sitePeacetimePanel.OffsetRight = -24.0f;
        _sitePeacetimePanel.OffsetBottom = -24.0f;
        _sitePeacetimePanel.CustomMinimumSize = new Vector2(520.0f, 0.0f);
    }

    private void UpdateSitePeacetimePanelVisibility(string reason)
    {
        if (_sitePeacetimePanel == null)
        {
            return;
        }

        bool shouldShow = _siteHudRoot?.Visible == true && !string.IsNullOrWhiteSpace(_selectedFacilitySlotId);
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

        if (_hudRoot != null)
        {
            _hudRoot.Visible = enabled;
            _hudRoot.SetProcessUnhandledInput(enabled);
        }

        if (!enabled)
        {
            _unitRoot?.PlayIdleForActiveEntities();
            _unitRoot?.ClearIntentMarkers();
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

        _inputRouter?.SetProcessUnhandledInput(enabled);
        _commandController?.SetProcess(enabled);
        _turnController?.SetProcess(enabled);
        _intentController?.SetProcess(enabled);
        _previewController?.SetProcess(enabled);

        if (!enabled)
        {
            _commandController?.ClearSelection();
            _hudRoot?.ClearActiveCommand();
            _previewController?.ClearActionPreviewHighlights();
            _previewController?.ClearSelectedHighlight();
        }
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

        RefreshSiteMapEntities(site, definition);
        RefreshFacilityList(site, definition);
        RefreshFacilityBuildList(site, definition);
        RefreshGarrisonList(site);
        RefreshThreatList(site);
        RefreshActionList(site);
        UpdateSitePeacetimePanelVisibility("refresh");
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

        int facilityCount = site.Facilities.Count(facility => facility.State != FacilityState.Destroyed);
        int garrisonCount = site.Garrison.Sum(garrison => garrison.Count);
        int activeThreatCount = site.PendingThreatIds
            .Select(id => StrategicWorldRuntime.State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .Count(threat => threat is { Stage: not ThreatStage.Resolved });

        return
            $"{definition?.Description ?? ResolveSiteName(siteId)}\n" +
            $"控制：{GetControlStateLabel(site.ControlState)}    模式：{GetSiteModeLabel(site.SiteMode)}\n" +
            $"归属：{StrategicWorldDisplayNames.GetFactionLabel(queries, site.OwnerFactionId)}    受损：{site.DamageLevel}\n" +
            $"建筑：{facilityCount}    驻军：{garrisonCount}    威胁：{activeThreatCount}";
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
        if (site == null || site.Garrison.Count == 0)
        {
            AddMutedLine(_siteGarrisonList, "无");
            return;
        }

        WorldSiteDefinition definition = ResolveSiteDefinition(site.SiteId);
        AddMutedLine(_siteGarrisonList, $"驻军区：{_deploymentService.BuildGarrisonSummary(site, definition)}");
        foreach (GarrisonState garrison in site.Garrison)
        {
            AddMutedLine(_siteGarrisonList, $"{GetUnitLabel(garrison.UnitTypeId)} x{garrison.Count}    士气 {garrison.Morale}");
        }
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

    private void RefreshSiteMapEntities(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearChildren(_sitePlacementEntityRoot);
        _sitePlacementEntities.Clear();
        _siteFacilitySlotEntities.Clear();
        _siteFacilitySlotLayouts.Clear();
        ClearBattleEntities();

        if (site == null || definition == null)
        {
            return;
        }

        RefreshFacilitySlotEntities(site, definition);

        if (_unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot rebuild site management units because UnitRoot is missing site={site.SiteId}");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteManagementInteractionsRebuilt site={site.SiteId} facility_slots={_siteFacilitySlotEntities.Count} placements={site.UnitPlacements.Count} animated=0 canInteract={CanOpenSiteDetail(site)}");
            return;
        }

        int animatedCount = 0;
        bool explorationActive = IsSiteExplorationActive(site, definition);
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
            if (explorationActive && IsPlayerArmySitePlacement(placement))
            {
                continue;
            }

            BattleEntity entity = CreateSitePlacementUnitEntity(placement, site);
            if (entity == null)
            {
                continue;
            }

            string placementId = placement.PlacementId;
            _unitRoot.AddChild(entity);
            entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
            ConfigureSitePlacementUnitEntity(entity, placement);
            _sitePlacementEntities[placementId] = entity;
            animatedCount++;
        }

        RefreshSiteExplorationPresentation(site, definition);

        if (_unitRoot != null)
        {
            _unitRoot.Visible = true;
            _unitRoot.PlayIdleForActiveEntities();
        }

        UpdateSiteMapEntities();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteManagementInteractionsRebuilt site={site.SiteId} facility_slots={_siteFacilitySlotEntities.Count} placements={site.UnitPlacements.Count} animated={animatedCount} canInteract={CanOpenSiteDetail(site)}");
    }

    private void RefreshFacilitySlotEntities(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (_sitePlacementEntityRoot == null || site == null || definition == null)
        {
            return;
        }

        Node slotsRoot = _activeSiteMap?.GetNodeOrNull<Node>(FacilitySlotsRootName);
        if (slotsRoot == null)
        {
            if (definition.FacilitySlots.Count > 0)
            {
                GameLog.Warn(nameof(WorldSiteRoot), $"Missing facility slot scene root site={site.SiteId} root={FacilitySlotsRootName}");
            }

            return;
        }

        var occupiedCells = new Dictionary<GridPosition, string>();
        foreach (WorldFacilitySlotEntity entity in slotsRoot.GetChildren().OfType<WorldFacilitySlotEntity>())
        {
            entity.SetFootprintPolygons(System.Array.Empty<Vector2[]>());
            string slotId = string.IsNullOrWhiteSpace(entity.SlotId) ? entity.Name : entity.SlotId;
            FacilitySlotDefinition slot = definition.FacilitySlots.FirstOrDefault(item => item.SlotId == slotId);
            if (slot == null)
            {
                entity.BindState(slotId, false, false, false, false, false, "slot_definition_missing");
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot scene node has no definition site={site.SiteId} slot={slotId}");
                continue;
            }

            string configurationError = "";
            if (!TrySnapFacilitySlotEntity(entity, slot, occupiedCells, out WorldFacilitySlotRuntimeLayout layout, out string layoutFailureReason))
            {
                configurationError = layoutFailureReason;
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot layout invalid site={site.SiteId} slot={slot.SlotId} reason={layoutFailureReason}");
            }
            else
            {
                _siteFacilitySlotLayouts[slot.SlotId] = layout;
            }

            FacilityInstance facility = ResolveFacilityInSlot(site, slot.SlotId);
            IReadOnlyList<WorldActionViewModel> buildActions = facility == null
                ? ResolveBuildActionsForSlot(site, slot)
                : System.Array.Empty<WorldActionViewModel>();
            int enabledBuildActionCount = buildActions.Count(action => action.IsEnabled);
            bool canInteract = string.IsNullOrWhiteSpace(configurationError) &&
                               (facility != null || buildActions.Count > 0);

            entity.BindState(
                slot.SlotId,
                facility != null,
                facility?.State == FacilityState.Building,
                enabledBuildActionCount > 0,
                canInteract,
                _selectedFacilitySlotId == slot.SlotId,
                configurationError,
                BuildFacilitySlotMapHint(facility, buildActions.Count, enabledBuildActionCount, configurationError));
            _siteFacilitySlotEntities[slot.SlotId] = entity;
        }

        foreach (FacilitySlotDefinition slot in definition.FacilitySlots)
        {
            if (!_siteFacilitySlotEntities.ContainsKey(slot.SlotId))
            {
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot definition has no scene entity site={site.SiteId} slot={slot.SlotId}");
            }
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"FacilitySlotsRegistered site={site.SiteId} rootVisible={(slotsRoot is CanvasItem item ? item.Visible : null)} definitions={definition.FacilitySlots.Count} sceneEntities={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count} footprints={BuildFacilitySlotFootprintLogSummary()} selectedSlot={_selectedFacilitySlotId}");
    }

    private static string BuildFacilitySlotMapHint(
        FacilityInstance facility,
        int buildActionCount,
        int enabledBuildActionCount,
        string configurationError)
    {
        if (!string.IsNullOrWhiteSpace(configurationError))
        {
            return "配置错误";
        }

        if (facility != null)
        {
            return facility.State == FacilityState.Building ? "建造中" : "已建";
        }

        if (enabledBuildActionCount > 0)
        {
            return $"可建 {enabledBuildActionCount}";
        }

        return buildActionCount > 0 ? "条件不足" : "";
    }

    private void UpdateSiteMapEntities()
    {
        if (_siteHudRoot?.Visible != true || string.IsNullOrWhiteSpace(_siteHudSiteId))
        {
            return;
        }

        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (site == null)
        {
            return;
        }

        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
            if (_sitePlacementEntities.TryGetValue(placement.PlacementId, out Node2D entity) &&
                IsLiveNode(entity) &&
                placement.PlacementId != _draggedPlacementId)
            {
                entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
                if (entity is BattleEntity battleEntity)
                {
                    SyncSitePlacementGridOccupant(battleEntity, placement);
                }
            }
        }

        RemoveDisposedSitePlacementEntityRefs();

        SyncSiteExplorationMarkerPositions(site);
    }

    private bool TryHandleSiteExplorationInput(InputEvent @event)
    {
        if (TryHandleSiteExplorationHudInput(@event))
        {
            return true;
        }

        if (_battleRuntimeEnabled ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            @event is not InputEventMouseButton { Pressed: true } mouseButton ||
            IsPointerOverSiteHud(@event) ||
            IsPointerOverSiteExplorationHud(@event))
        {
            return false;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            return TryCancelSiteExplorationMovePreview();
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return false;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition) ||
            !TryGetMouseGridPosition(out GridPosition destination))
        {
            return false;
        }

        EnsureSiteExplorationStateReady(site, definition);
        if (IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = $"巡逻单位接近：{ResolveExplorationPatrolName(definition, site.Exploration.ActiveAlertPatrolId)}。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            RefreshSiteExplorationHud(site, definition);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (!_activeGridMap.TryGetTopSurfacePosition(destination, out GridSurfacePosition target) ||
            (target.X == site.Exploration.CurrentCellX &&
             target.Y == site.Exploration.CurrentCellY &&
             target.Height == site.Exploration.CurrentCellHeight))
        {
            ClearSiteExplorationMovePreview(site);
            GetViewport().SetInputAsHandled();
            return true;
        }

        string destinationKey = WorldSiteExplorationService.ToCellKey(target);
        if (IsConfirmingSiteExplorationMove(site, destinationKey))
        {
            site.Exploration.IsSimulationPaused = false;
            site.Exploration.PauseReason = "";
            site.Exploration.ActiveAlertPatrolId = "";
            ClearSiteExplorationPathPreview();
            AdvanceSiteExplorationAction(site, definition, waitAction: false);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (!WorldSiteExplorationService.TryBuildPartyPath(
                site.Exploration,
                definition,
                _activeGridMap,
                destination,
                out IReadOnlyList<GridSurfacePosition> path,
                out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = $"探索移动失败：{failureReason}";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            ClearSiteExplorationMovePreview(site);
            GetViewport().SetInputAsHandled();
            return true;
        }

        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        site.Exploration.IsSimulationPaused = true;
        site.Exploration.PauseReason = SiteExplorationPauseMovePreview;
        site.Exploration.ActiveAlertPatrolId = "";
        SetSiteExplorationPendingPath(site, path);
        ShowSiteExplorationPathPreview(path);
        StrategicWorldRuntime.LastNotice = path.Count > 1
            ? $"探索移动已规划：{path.Count - 1} 格。再次点击目标格确认，右键取消。"
            : "探索队伍已在目标位置。";
        // Valid exploration clicks should not rebuild site-management UI; rebuilding recreates/binds unit presentation and resets animations.
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        GameLog.Info(nameof(WorldSiteRoot), $"Site exploration move intent site={site.SiteId} destination={destination} pathCells={path.Count}");
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool TryHandleSiteExplorationHudInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouseButton ||
            _siteExplorationHud?.Visible != true)
        {
            return false;
        }

        Vector2 screenPosition = mouseButton.Position;
        if (TryDispatchSiteExplorationButtonAt(_siteExplorationEngageButton, screenPosition, "engage", OnSiteExplorationEngagePressed) ||
            TryDispatchSiteExplorationButtonAt(_siteExplorationRetreatButton, screenPosition, "retreat", OnSiteExplorationRetreatPressed) ||
            TryDispatchSiteExplorationButtonAt(_siteExplorationWaitButton, screenPosition, "wait", OnSiteExplorationWaitPressed))
        {
            GetViewport().SetInputAsHandled();
            return true;
        }

        return false;
    }

    private bool TryDispatchSiteExplorationButtonAt(Button button, Vector2 screenPosition, string role, System.Action pressed)
    {
        if (button?.Visible != true ||
            button.Disabled ||
            !IsScreenPointInsideControl(button, screenPosition))
        {
            return false;
        }

        DispatchSiteExplorationButton(role, pressed, "manual_hit");
        return true;
    }

    private static bool IsConfirmingSiteExplorationMove(WorldSiteState site, string destinationKey)
    {
        // PendingPathCellKeys is reused after confirmation for automatic step-by-step execution.
        // Only the explicit preview pause state is confirmable; a moving path must not accept a second click as a new confirmation.
        return site?.Exploration?.PendingPathCellKeys?.Count > 0 &&
               site.Exploration.IsSimulationPaused &&
               site.Exploration.PauseReason == SiteExplorationPauseMovePreview &&
               !string.IsNullOrWhiteSpace(destinationKey) &&
               site.Exploration.PendingPathCellKeys[^1] == destinationKey;
    }

    private static void SetSiteExplorationPendingPath(WorldSiteState site, IReadOnlyList<GridSurfacePosition> path)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        site.Exploration.PendingPathCellKeys.Clear();
        if (path == null)
        {
            return;
        }

        foreach (GridSurfacePosition cell in path.Skip(1))
        {
            site.Exploration.PendingPathCellKeys.Add(WorldSiteExplorationService.ToCellKey(cell));
        }
    }

    private void EnterSiteAlertModeForVisit(WorldSiteState site, string armyId)
    {
        if (site == null)
        {
            return;
        }

        // Scene entry projects an already-arrived army into the site; the application service owns the mode transition.
        _siteModeTransitions.EnterExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "infiltration_visit",
            armyId);
    }

    private static void LogSiteUnitState(string phase, WorldSiteState site, string armyId = "")
    {
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"{phase} site={site?.SiteId ?? ""} mode={site?.SiteMode.ToString() ?? ""} army={armyId ?? ""} placements={FormatSitePlacementsForLog(site)}");
    }

    private static string FormatSitePlacementsForLog(WorldSiteState site)
    {
        return site?.UnitPlacements == null
            ? "none"
            : string.Join(
                "|",
                site.UnitPlacements
                    .OrderBy(placement => placement.PlacementId)
                    .Select(placement =>
                        $"{placement.PlacementId}[unit={placement.UnitTypeId},kind={placement.PlacementKind},source={placement.SourceKind}:{placement.SourceId},army={placement.ArmyId},faction={placement.FactionId},cell={placement.CellX}:{placement.CellY}:{placement.CellHeight}]"));
    }

    private static string FormatArmyUnitsForLog(WorldArmyState army)
    {
        return army?.GarrisonUnits == null
            ? "none"
            : string.Join(",", army.GarrisonUnits.Where(unit => unit != null).Select(unit => $"{unit.UnitTypeId}:{unit.Count}"));
    }

    private static string FormatForcesForLog(IEnumerable<BattleForceRequest> forces)
    {
        return forces == null
            ? "none"
            : string.Join(
                "|",
                forces.Select(force =>
                    $"{force.ForceId}[unit={force.UnitDefinitionId},count={force.Count},source={force.SourceKind}:{force.SourceId},faction={force.FactionId},placements={FormatForcePlacementsForLog(force)}]"));
    }

    private static string FormatForcePlacementsForLog(BattleForceRequest force)
    {
        return force?.PreferredPlacements == null
            ? "none"
            : string.Join(",", force.PreferredPlacements.Select(placement => $"{placement.PlacementId}@{placement.CellX}:{placement.CellY}:{placement.CellHeight}"));
    }

    private static string FormatBattleForceResultsForLog(IEnumerable<BattleForceResult> results)
    {
        return results == null
            ? "none"
            : string.Join(
                "|",
                results.Select(result =>
                    $"{result.ForceId}[unit={result.UnitDefinitionId},source={result.SourceKind}:{result.SourceId},initial={result.InitialCount},survived={result.SurvivedCount},defeated={result.DefeatedCount}]"));
    }

    private bool TryCancelSiteExplorationMovePreview()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (site?.Exploration == null || site.Exploration.PendingPathCellKeys.Count == 0)
        {
            return false;
        }

        ClearSiteExplorationMovePreview(site);
        StrategicWorldRuntime.LastNotice = "探索移动已取消。";
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ClearSiteExplorationMovePreview(WorldSiteState site)
    {
        if (site?.Exploration != null &&
            site.Exploration.PauseReason == SiteExplorationPauseMovePreview)
        {
            site.Exploration.PendingPathCellKeys.Clear();
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
            site.Exploration.ActiveAlertPatrolId = "";
        }

        ClearSiteExplorationPathPreview();
    }

    private void ShowSiteExplorationPathPreview(IReadOnlyList<GridSurfacePosition> path)
    {
        // BattleGridHighlightOverlay.SetPath owns start-cell filtering and arrow drawing.
        // Passing the full path keeps one-cell movement visible instead of double-skipping the target cell.
        _highlightOverlay?.SetPath(
            path?.Select(cell => new GridPosition(cell.X, cell.Y)) ?? System.Array.Empty<GridPosition>());
    }

    private void ClearSiteExplorationPathPreview()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
    }

    private void ContinueConfirmedSiteExplorationMoveIfReady()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition))
        {
            return;
        }

        EnsureSiteExplorationStateReady(site, definition);
        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        if (site.Exploration.IsSimulationPaused || site.Exploration.PendingPathCellKeys.Count == 0)
        {
            return;
        }

        AdvanceSiteExplorationAction(site, definition, waitAction: false);
    }

    private void AdvanceSiteExplorationAction(WorldSiteState site, WorldSiteDefinition definition, bool waitAction)
    {
        if (!IsSiteExplorationActive(site, definition) ||
            _unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(
            site.Exploration,
            definition,
            _activeGridMap,
            waitAction: waitAction);
        PresentSiteExplorationTickResult(site, result);
        RefreshSiteExplorationAlertRangePresentation(site, definition);
        if (result.Paused && result.PauseReason == SiteExplorationPauseAlertRadius)
        {
            site.Exploration.PendingPathCellKeys.Clear();
            ClearSiteExplorationPathPreview();
            StrategicWorldRuntime.LastNotice = $"巡逻单位接近：{ResolveExplorationPatrolName(definition, result.AlertPatrolId)}。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        }
        RefreshSiteExplorationHud(site, definition);
    }

    private bool EnsureVisitingArmyPlacement(WorldSiteState site, WorldSiteDefinition definition, string armyId)
    {
        if (site == null ||
            definition == null ||
            string.IsNullOrWhiteSpace(armyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(armyId, out WorldArmyState army) != true)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        bool createdAny = false;
        int unitIndex = 0;
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            for (int count = 0; count < unit.Count; count++)
            {
                unitIndex++;
                string placementId = BuildVisitingArmyPlacementId(army.ArmyId, unit.UnitTypeId, unitIndex);
                WorldSiteUnitPlacement existing = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
                if (existing != null)
                {
                    ApplyVisitingArmyPlacementMetadata(existing, army, unit.UnitTypeId, unitIndex);
                    continue;
                }

                bool canEnterWater = _battleUnitFactory.TryGetUnitDefinition(unit.UnitTypeId, out BattleUnitDefinition unitDefinition) &&
                                     unitDefinition.CanEnterWater;
                WorldSiteAttackDirection direction = army.TargetApproachDirection == WorldSiteAttackDirection.Any
                    ? WorldSiteAttackDirection.West
                    : army.TargetApproachDirection;
                bool foundCandidate = TryResolveFirstDeploymentCandidate(direction, canEnterWater, out WorldSiteDeploymentCell candidate);
                if (!foundCandidate)
                {
                    foundCandidate = TryResolveFirstDeploymentCandidate(WorldSiteAttackDirection.Any, canEnterWater, out candidate);
                }

                if (!foundCandidate)
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"VisitingArmyPlacementSkipped site={site.SiteId} army={army.ArmyId} unit={unit.UnitTypeId} reason=deployment_candidate_missing");
                    continue;
                }

                site.UnitPlacements.Add(new WorldSiteUnitPlacement
                {
                    PlacementId = placementId,
                    UnitTypeId = unit.UnitTypeId,
                    UnitIndex = unitIndex,
                    FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId,
                    PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy,
                    SourceKind = "PlayerArmy",
                    SourceId = army.ArmyId,
                    ArmyId = army.ArmyId,
                    AttackDirection = direction,
                    CellX = candidate.Cell.X,
                    CellY = candidate.Cell.Y,
                    CellHeight = candidate.Height
                });
                createdAny = true;
            }
        }

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement != null)
        {
            site.Exploration.CurrentCellX = partyPlacement.CellX;
            site.Exploration.CurrentCellY = partyPlacement.CellY;
            site.Exploration.CurrentCellHeight = partyPlacement.CellHeight;
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
        }

        if (createdAny)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"VisitingArmyPlacementEnsured site={site.SiteId} army={army.ArmyId} placements={unitIndex} armyUnits={FormatArmyUnitsForLog(army)} sitePlacements={FormatSitePlacementsForLog(site)}");
        }

        return unitIndex > 0;
    }

    private static string BuildVisitingArmyPlacementId(string armyId, string unitTypeId, int index)
    {
        return $"site_army:PlayerArmy:{armyId}:{unitTypeId}:{index}";
    }

    private static void ApplyVisitingArmyPlacementMetadata(WorldSiteUnitPlacement placement, WorldArmyState army, string unitTypeId, int index)
    {
        placement.UnitTypeId = unitTypeId;
        placement.UnitIndex = index;
        placement.FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId;
        placement.PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy;
        placement.SourceKind = "PlayerArmy";
        placement.SourceId = army.ArmyId;
        placement.ArmyId = army.ArmyId;
        placement.AttackDirection = army.TargetApproachDirection;
    }

    private static bool IsPlayerArmySitePlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null &&
               placement.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(placement.ArmyId) &&
               placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker;
    }

    private static bool IsResidentPlayerArmySiteForce(BattleForceRequest force, WorldSiteState site)
    {
        return force != null &&
               site != null &&
               force.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(force.SourceId) &&
               site.UnitPlacements.Any(placement =>
                   IsPlayerArmySitePlacement(placement) &&
                   placement.SourceId == force.SourceId &&
                   placement.UnitTypeId == force.UnitDefinitionId);
    }

    private WorldSiteUnitPlacement ResolveSiteExplorationPartyPlacement(WorldSiteState site)
    {
        return site?.UnitPlacements
            .Where(IsPlayerArmySitePlacement)
            .OrderBy(placement => placement.UnitIndex)
            .ThenBy(placement => placement.PlacementId)
            .FirstOrDefault();
    }

    private void SyncSiteExplorationPartyPlacement(WorldSiteState site)
    {
        WorldSiteUnitPlacement placement = ResolveSiteExplorationPartyPlacement(site);
        if (placement == null || site?.Exploration == null)
        {
            return;
        }

        placement.CellX = site.Exploration.CurrentCellX;
        placement.CellY = site.Exploration.CurrentCellY;
        placement.CellHeight = site.Exploration.CurrentCellHeight;
    }

    private void RefreshSiteExplorationPresentation(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearSiteExplorationMarkers();
        if (!IsSiteExplorationActive(site, definition) || _sitePlacementEntityRoot == null)
        {
            return;
        }

        EnsureSiteExplorationStateReady(site, definition);
        // Exploration entities are presentation/control projections; WorldSiteState.Exploration remains the authoritative tick state.
        _siteExplorationPartyMarker = CreateSiteExplorationPartyEntity(site);
        if (_siteExplorationPartyMarker != null)
        {
            _sitePlacementEntityRoot.AddChild(_siteExplorationPartyMarker);
        }

        WorldSiteExplorationService.ReconcilePatrolStates(site, definition);
        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits.Where(item => item is { IsRemoved: false }))
        {
            if (TryBindSiteExplorationPatrolEntity(definition, patrol, out Node2D marker))
            {
                _siteExplorationPatrolMarkers[patrol.PatrolId] = marker;
                continue;
            }

            GameLog.Warn(nameof(WorldSiteRoot), $"Exploration patrol has no configured site unit placement site={site.SiteId} patrol={patrol.PatrolId}");
        }

        SyncSiteExplorationMarkerPositions(site);
        RefreshSiteExplorationAlertRangePresentation(site, definition);
        EnsureSiteExplorationHud(site, definition);
    }

    private void PresentSiteExplorationTickResult(WorldSiteState site, SiteExplorationTickResult result)
    {
        if (site?.Exploration == null || result == null)
        {
            return;
        }

        bool animatedAnyMovement = false;
        if (result.PartyMoved && IsLiveNode(_siteExplorationPartyMarker) && _siteExplorationPartyMarker is BattleEntity partyEntity)
        {
            SyncSiteExplorationPartyPlacement(site);
            bool partyWillContinueMoving = !result.Paused && site.Exploration.PendingPathCellKeys.Count > 0;
            // Exploration advances a long path as discrete realtime steps; keep the battle move loop alive until the path stops.
            _unitRoot?.MoveEntityTo(
                partyEntity,
                result.PartyPathStep,
                restartMoveAnimation: !partyWillContinueMoving,
                returnToIdleOnComplete: !partyWillContinueMoving);
            animatedAnyMovement = true;
        }

        foreach (SiteExplorationPatrolMove patrolMove in result.PatrolMoves)
        {
            if (!_siteExplorationPatrolMarkers.TryGetValue(patrolMove.PatrolId, out Node2D marker) ||
                !IsLiveNode(marker) ||
                marker is not BattleEntity patrolEntity)
            {
                _siteExplorationPatrolMarkers.Remove(patrolMove.PatrolId);
                continue;
            }

            SyncSiteExplorationPatrolPlacement(site, patrolMove.PatrolId, patrolMove.To);
            _unitRoot?.MoveEntityTo(patrolEntity, new[] { patrolMove.From, patrolMove.To });
            animatedAnyMovement = true;
        }

        if (!animatedAnyMovement)
        {
            SyncSiteExplorationMarkerPositions(site);
        }
    }

    private void SyncSiteExplorationPatrolPlacement(WorldSiteState site, string patrolId, GridSurfacePosition surface)
    {
        WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId);
        SiteExplorationPatrolDefinition patrolDefinition = definition?.ExplorationPatrols.FirstOrDefault(item => item.Id == patrolId);
        if (site == null || patrolDefinition == null)
        {
            return;
        }

        WorldSiteUnitPlacement placement = null;
        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            placement = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == patrolDefinition.SourcePlacementId);
        }

        if (placement == null && string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            placement = site.UnitPlacements.FirstOrDefault(item => item.UnitTypeId == patrolDefinition.UnitTypeId);
        }

        if (placement == null)
        {
            return;
        }

        // Patrol AI owns this configured unit during exploration; keep the original placement state aligned with the patrol state.
        placement.CellX = surface.X;
        placement.CellY = surface.Y;
        placement.CellHeight = surface.Height;
    }

    private void ClearSiteExplorationMarkers()
    {
        if (IsLiveNode(_siteExplorationPartyMarker))
        {
            _siteExplorationPartyMarker.QueueFree();
        }
        _siteExplorationPartyMarker = null;
        // Patrol entries bind to existing site placement entities; never free them from the exploration layer.
        _siteExplorationPatrolMarkers.Clear();
        if (IsLiveNode(_siteExplorationAlertRangeRoot))
        {
            _siteExplorationAlertRangeRoot.QueueFree();
        }
        _siteExplorationAlertRangeRoot = null;
        if (IsLiveNode(_siteExplorationHud))
        {
            _siteExplorationHud.QueueFree();
        }
        _siteExplorationHud = null;
        _siteExplorationWaitButton = null;
        ClearSiteExplorationPathPreview();
    }

    private Node2D CreateSiteExplorationPartyEntity(WorldSiteState site)
    {
        WorldSiteUnitPlacement placement = ResolveSiteExplorationPartyPlacement(site);
        string unitTypeId = ResolveExplorationPartyUnitTypeId(site);
        BattleEntity entity = CreateExplorationEntity(
            "SiteExplorationParty",
            placement?.PlacementId ?? "site_exploration_party",
            unitTypeId,
            BattleFaction.Player,
            new GridSurfacePosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight));
        entity?.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(true);
        return entity;
    }

    private bool TryBindSiteExplorationPatrolEntity(WorldSiteDefinition definition, SiteExplorationPatrolState patrol, out Node2D marker)
    {
        marker = null;
        SiteExplorationPatrolDefinition patrolDefinition = definition?.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
        if (patrolDefinition == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId) &&
            _sitePlacementEntities.TryGetValue(patrolDefinition.SourcePlacementId, out marker))
        {
            marker.Name = $"SiteExplorationPatrol_{patrol.PatrolId}";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            return false;
        }

        KeyValuePair<string, Node2D>? fallback = _sitePlacementEntities
            .Where(item => ResolveSiteState(_siteHudSiteId)?.UnitPlacements.Any(placement =>
                placement.PlacementId == item.Key &&
                placement.UnitTypeId == patrolDefinition.UnitTypeId) == true)
            .Select(item => (KeyValuePair<string, Node2D>?)item)
            .FirstOrDefault();
        if (!fallback.HasValue)
        {
            return false;
        }

        marker = fallback.Value.Value;
        marker.Name = $"SiteExplorationPatrol_{patrol.PatrolId}";
        return true;
    }

    private BattleEntity CreateExplorationEntity(
        string nodeName,
        string forceId,
        string unitTypeId,
        BattleFaction faction,
        GridSurfacePosition surface)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = unitTypeId,
            Count = 1,
            FactionId = faction == BattleFaction.Player ? StrategicWorldIds.FactionPlayer : StrategicWorldIds.FactionUndead
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = forceId,
            CellX = surface.X,
            CellY = surface.Y,
            CellHeight = surface.Height
        });

        BattleEntity entity = _battleUnitFactory.Create(force, 0, faction, new GridPosition(surface.X, surface.Y));
        if (entity == null)
        {
            return null;
        }

        entity.Name = nodeName;
        entity.InputPickable = false;
        entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        SyncExplorationEntityGridOccupant(entity, surface);
        if (TryGetCellGlobalPosition(new GridPosition(surface.X, surface.Y), out Vector2 globalPosition))
        {
            entity.GlobalPosition = globalPosition;
        }

        return entity;
    }

    private string ResolveExplorationPartyUnitTypeId(WorldSiteState site)
    {
        return ResolveSiteExplorationPartyPlacement(site)?.UnitTypeId ?? StrategicWorldIds.UnitPlayerKnight;
    }

    private void SyncSiteExplorationMarkerPositions(WorldSiteState site)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        if (_siteExplorationPartyMarker != null && !IsLiveNode(_siteExplorationPartyMarker))
        {
            _siteExplorationPartyMarker = null;
        }

        if (_siteExplorationPartyMarker != null &&
            TryGetCellGlobalPosition(new GridPosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY), out Vector2 partyPosition))
        {
            _siteExplorationPartyMarker.GlobalPosition = partyPosition;
            if (_siteExplorationPartyMarker is BattleEntity partyEntity)
            {
                SyncExplorationEntityGridOccupant(
                    partyEntity,
                    new GridSurfacePosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight));
            }
        }

        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits)
        {
            if (patrol == null ||
                patrol.IsRemoved ||
                !_siteExplorationPatrolMarkers.TryGetValue(patrol.PatrolId, out Node2D marker) ||
                !IsLiveNode(marker))
            {
                _siteExplorationPatrolMarkers.Remove(patrol?.PatrolId ?? "");
                continue;
            }

            if (TryGetCellGlobalPosition(new GridPosition(patrol.CellX, patrol.CellY), out Vector2 patrolPosition))
            {
                marker.GlobalPosition = patrolPosition;
                if (marker is BattleEntity patrolEntity)
                {
                    SyncExplorationEntityGridOccupant(patrolEntity, new GridSurfacePosition(patrol.CellX, patrol.CellY, patrol.CellHeight));
                }
            }
        }
    }

    private void SyncExplorationEntityGridOccupant(BattleEntity entity, GridSurfacePosition surface)
    {
        if (!IsLiveNode(entity))
        {
            return;
        }

        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        gridOccupant.GridX = surface.X;
        gridOccupant.GridY = surface.Y;
        gridOccupant.GridHeight = surface.Height;
        gridOccupant.UseExplicitHeight = surface.Height > 0;
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }

    private static bool IsLiveNode(GodotObject node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        return node is not Node queuedNode || !queuedNode.IsQueuedForDeletion();
    }

    private void RemoveDisposedSitePlacementEntityRefs()
    {
        if (_sitePlacementEntities.Count == 0)
        {
            return;
        }

        foreach (string placementId in _sitePlacementEntities
                     .Where(item => !IsLiveNode(item.Value))
                     .Select(item => item.Key)
                     .ToArray())
        {
            _sitePlacementEntities.Remove(placementId);
        }
    }

    private void RefreshSiteExplorationAlertRangePresentation(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (IsLiveNode(_siteExplorationAlertRangeRoot))
        {
            _siteExplorationAlertRangeRoot.QueueFree();
        }
        _siteExplorationAlertRangeRoot = null;
        if (_sitePlacementEntityRoot == null || site?.Exploration == null || definition?.ExplorationPatrols == null)
        {
            return;
        }

        _siteExplorationAlertRangeRoot = new Node2D
        {
            Name = "SiteExplorationAlertRangeRoot",
            ZIndex = 42
        };
        _sitePlacementEntityRoot.AddChild(_siteExplorationAlertRangeRoot);

        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits.Where(item => item is { IsRemoved: false }))
        {
            SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
            if (patrolDefinition == null)
            {
                continue;
            }

            foreach (GridPosition cell in EnumerateManhattanCells(patrol.CellX, patrol.CellY, patrolDefinition.AlertRadiusCells))
            {
                if (!_activeGridMap.TryGetCell(cell, out _))
                {
                    continue;
                }

                // Draw only the outer alert ring so risk is readable without filling every internal cell.
                _siteExplorationAlertRangeRoot.AddChild(new Line2D
                {
                    Points = ClosePolygon(BuildCellPolygonGlobal(cell)),
                    Width = 2.0f,
                    DefaultColor = new Color(1.0f, 0.32f, 0.12f, 0.88f),
                    ZIndex = 42
                });
            }
        }
    }

    private static IEnumerable<GridPosition> EnumerateManhattanCells(int centerX, int centerY, int radius)
    {
        int safeRadius = System.Math.Max(0, radius);
        for (int x = centerX - safeRadius; x <= centerX + safeRadius; x++)
        {
            for (int y = centerY - safeRadius; y <= centerY + safeRadius; y++)
            {
                if (System.Math.Abs(x - centerX) + System.Math.Abs(y - centerY) == safeRadius)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length == 0)
        {
            return System.Array.Empty<Vector2>();
        }

        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private bool TryAppendSiteExplorationAlertChoices(WorldSiteState site)
    {
        if (site?.Exploration == null ||
            site.Exploration.PauseReason != SiteExplorationPauseAlertRadius ||
            string.IsNullOrWhiteSpace(site.Exploration.ActiveAlertPatrolId))
        {
            return false;
        }

        AddSiteExplorationActionButton("进入遭遇战", () => RequestSiteExplorationBattle(site));
        AddSiteExplorationActionButton("撤退并保持警戒", () => RetreatFromSiteExplorationAlert(site));
        return true;
    }

    private void AddSiteExplorationActionButton(string text, System.Action pressed)
    {
        Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        button.Text = text;
        button.Pressed += pressed;
        _siteActionList.AddChild(button);
    }

    private void EnsureSiteExplorationHud(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (!IsSiteExplorationActive(site, definition))
        {
            if (IsLiveNode(_siteExplorationHud))
            {
                _siteExplorationHud.QueueFree();
            }
            _siteExplorationHud = null;
            _siteExplorationHudPanel = null;
            _siteExplorationAlertActions = null;
            _siteExplorationAlertLabel = null;
            _siteExplorationWaitButton = null;
            _siteExplorationEngageButton = null;
            _siteExplorationRetreatButton = null;
            return;
        }

        if (_siteExplorationHud != null)
        {
            RefreshSiteExplorationHud(site, definition);
            return;
        }

        PackedScene scene = GD.Load<PackedScene>(SiteExplorationHudScenePath);
        _siteExplorationHud = scene?.Instantiate<Control>();
        if (_siteExplorationHud == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration HUD missing path={SiteExplorationHudScenePath}");
            return;
        }

        // Exploration HUD is screen-space UI. Attach it to the CanvasLayer via the full-screen site HUD root;
        // it must never participate in map/unit Node2D sorting.
        (_siteHudRoot ?? GetNodeOrNull<Node>("CanvasLayer") ?? (Node)this).AddChild(_siteExplorationHud);
        _siteExplorationHud.ZIndex = 650;
        _siteExplorationHudPanel = _siteExplorationHud.GetNodeOrNull<Control>("Panel");
        _siteExplorationAlertActions = _siteExplorationHud.GetNodeOrNull<Control>("Panel/Margin/Stack/AlertActions");
        _siteExplorationAlertLabel = _siteExplorationHud.GetNodeOrNull<Label>("Panel/Margin/Stack/AlertLabel");
        _siteExplorationWaitButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/WaitButton");
        _siteExplorationEngageButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/AlertActions/EngageButton");
        _siteExplorationRetreatButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/AlertActions/RetreatButton");
        ConfigureSiteExplorationButton(_siteExplorationWaitButton, OnSiteExplorationWaitPressed, "wait");
        ConfigureSiteExplorationButton(_siteExplorationEngageButton, OnSiteExplorationEngagePressed, "engage");
        ConfigureSiteExplorationButton(_siteExplorationRetreatButton, OnSiteExplorationRetreatPressed, "retreat");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Site exploration HUD bound site={site.SiteId} alertLabel={_siteExplorationAlertLabel != null} wait={_siteExplorationWaitButton != null} engage={_siteExplorationEngageButton != null} retreat={_siteExplorationRetreatButton != null}");

        RefreshSiteExplorationHud(site, definition);
    }

    private void ConfigureSiteExplorationButton(Button button, System.Action pressed, string role)
    {
        if (button == null || pressed == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration HUD button missing role={role}");
            return;
        }

        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.Pressed += () => DispatchSiteExplorationButton(role, pressed, "pressed");
        // Some exploration HUD containers are manually excluded from map input. This GUI fallback gives us
        // deterministic diagnostics if a scene-level mouse filter prevents BaseButton.Pressed from firing.
        button.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            {
                DispatchSiteExplorationButton(role, pressed, "gui_release");
            }
        };
    }

    private void DispatchSiteExplorationButton(string role, System.Action pressed, string source)
    {
        ulong now = Time.GetTicksMsec();
        if (_lastSiteExplorationButtonRole == role &&
            now - _lastSiteExplorationButtonMsec < 120UL)
        {
            return;
        }

        _lastSiteExplorationButtonRole = role ?? "";
        _lastSiteExplorationButtonMsec = now;
        GameLog.Info(nameof(WorldSiteRoot), $"Site exploration HUD button dispatched role={role} source={source}");
        pressed();
    }

    private void RefreshSiteExplorationHud(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (_siteExplorationHud == null)
        {
            return;
        }

        bool active = IsSiteExplorationActive(site, definition);
        bool alertPaused = IsSiteExplorationAlertPaused(site);
        _siteExplorationHud.Visible = active;
        if (_siteExplorationAlertActions != null)
        {
            _siteExplorationAlertActions.Visible = active && alertPaused;
        }
        if (_siteExplorationAlertLabel != null)
        {
            _siteExplorationAlertLabel.Visible = active && alertPaused;
            _siteExplorationAlertLabel.Text = alertPaused
                ? $"已进入警惕范围：{ResolveExplorationPatrolName(definition, site.Exploration.ActiveAlertPatrolId)}"
                : "";
        }
        if (_siteExplorationWaitButton != null)
        {
            _siteExplorationWaitButton.Visible = active && !alertPaused;
            _siteExplorationWaitButton.Disabled =
                !active ||
                _unitRoot?.HasActiveMovementTweens == true ||
                alertPaused;
        }
        if (_siteExplorationEngageButton != null)
        {
            _siteExplorationEngageButton.Visible = active && alertPaused;
            _siteExplorationEngageButton.Disabled = !active || !alertPaused;
        }
        if (_siteExplorationRetreatButton != null)
        {
            _siteExplorationRetreatButton.Visible = active && alertPaused;
            _siteExplorationRetreatButton.Disabled = !active || !alertPaused;
        }
    }

    private void OnSiteExplorationWaitPressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition) ||
            IsSiteExplorationAlertPaused(site))
        {
            return;
        }

        site.Exploration.IsSimulationPaused = false;
        site.Exploration.PauseReason = "";
        site.Exploration.ActiveAlertPatrolId = "";
        site.Exploration.PendingPathCellKeys.Clear();
        ClearSiteExplorationPathPreview();
        AdvanceSiteExplorationAction(site, definition, waitAction: true);
    }

    private void OnSiteExplorationEngagePressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (!IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = "当前没有可进入的探索遭遇。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Exploration engage ignored site={_siteHudSiteId} hasSite={site != null} pause={site?.Exploration?.PauseReason ?? ""} patrol={site?.Exploration?.ActiveAlertPatrolId ?? ""}");
            return;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Exploration engage requested site={site.SiteId} patrol={site.Exploration.ActiveAlertPatrolId}");
        RequestSiteExplorationBattle(site);
    }

    private void OnSiteExplorationRetreatPressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (!IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = "当前没有需要撤退的探索遭遇。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Exploration retreat ignored site={_siteHudSiteId} hasSite={site != null} pause={site?.Exploration?.PauseReason ?? ""} patrol={site?.Exploration?.ActiveAlertPatrolId ?? ""}");
            return;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Exploration retreat requested site={site.SiteId} patrol={site.Exploration.ActiveAlertPatrolId}");
        RetreatFromSiteExplorationAlert(site);
    }

    private void RequestSiteExplorationBattle(WorldSiteState site)
    {
        WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId);
        if (!IsSiteExplorationActive(site, definition))
        {
            StrategicWorldRuntime.LastNotice = "探索状态已失效，无法进入遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            return;
        }

        string patrolId = site.Exploration.ActiveAlertPatrolId;
        SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrolId);
        if (patrolDefinition == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：缺少触发巡逻配置。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=patrol_definition_missing");
            return;
        }

        SiteExplorationPatrolState patrolState = site.Exploration.PatrolUnits.FirstOrDefault(item => item.PatrolId == patrolId);
        WorldSiteUnitPlacement patrolPlacement = site.UnitPlacements.FirstOrDefault(item =>
            item.PlacementId == patrolDefinition.SourcePlacementId &&
            item.UnitTypeId == patrolDefinition.UnitTypeId);
        if (patrolState == null || patrolPlacement == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：触发巡逻单位不存在。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=patrol_source_missing placement={patrolDefinition.SourcePlacementId} unit={patrolDefinition.UnitTypeId}");
            return;
        }

        // The patrol's source placement is the battle identity authority; sync its current exploration cell
        // before handoff so battle deployment does not use a stale garrison position.
        patrolPlacement.CellX = patrolState.CellX;
        patrolPlacement.CellY = patrolState.CellY;
        patrolPlacement.CellHeight = patrolState.CellHeight;

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true ||
            army.GarrisonUnits.Sum(unit => System.Math.Max(0, unit.Count)) <= 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：缺少潜入部队。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=exploration_army_missing placement={partyPlacement?.PlacementId ?? ""}");
            return;
        }

        GridSurfacePosition entryCell = new(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight);
        SiteExplorationPatrolDefinition[] encounterPatrols = definition.ExplorationPatrols
            .Where(patrol => patrol != null && WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol))
            .ToArray();
        BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
            site.SiteId,
            $"patrol:{patrolId}",
            patrolId,
            army,
            encounterPatrols,
            entryCell,
            site.Exploration.AlertLevel,
            string.IsNullOrWhiteSpace(_siteHudReturnScenePath) ? "res://scenes/world/StrategicWorldRoot.tscn" : _siteHudReturnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);
        if (request.PlayerForces.Count == 0 || request.EnemyForces.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：参战单位不完整。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=forces_missing playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count}");
            return;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"ExplorationBattleRequested site={site.SiteId} army={army.ArmyId} armyUnits={FormatArmyUnitsForLog(army)} patrol={patrolId} patrolPlacement={patrolDefinition.SourcePlacementId} playerForces={FormatForcesForLog(request.PlayerForces)} enemyForces={FormatForcesForLog(request.EnemyForces)} sitePlacements={FormatSitePlacementsForLog(site)}");

        SiteBattleLaunchRollback rollback = CaptureSiteBattleLaunchRollback(site.SiteId);
        // Exploration confirmation only changes the site runtime mode; TurnSystem starts after the battle request is accepted.
        _siteModeTransitions.EnterBattleFromExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "exploration_battle_requested",
            request.RequestId);
        BattleSessionHandoff.BeginBattle(request);
        ApplyBattleStartRequest();
        if (!ActivateBattleRuntime())
        {
            BattleSessionHandoff.CancelBattle();
            RollbackSiteBattleLaunch(rollback, request, _battleStartBlockedReason);
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason={_battleStartBlockedReason}");
        }
    }

    private void RetreatFromSiteExplorationAlert(WorldSiteState site)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        ClearSiteExplorationPathPreview();
        _siteModeTransitions.RetreatFromExplorationAlert(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            site.Exploration.ActiveAlertPatrolId);
        StrategicWorldRuntime.LastNotice = "探索队伍撤退并保持警戒。";
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        RefreshSiteExplorationHud(site, ResolveSiteDefinition(site.SiteId));
    }

    private static bool IsSiteExplorationAlertPaused(WorldSiteState site)
    {
        return site?.Exploration != null &&
               site.Exploration.PauseReason == SiteExplorationPauseAlertRadius &&
               !string.IsNullOrWhiteSpace(site.Exploration.ActiveAlertPatrolId);
    }

    private bool IsSiteExplorationActive(WorldSiteState site, WorldSiteDefinition definition)
    {
        return site?.Exploration != null &&
               definition?.ExplorationPatrols != null &&
               definition.ExplorationPatrols.Count > 0 &&
               _activeGridMap != null &&
               site.OwnerFactionId != StrategicWorldRuntime.State?.PlayerFactionId &&
               ResolveSiteExplorationPartyPlacement(site) != null &&
               definition.ExplorationPatrols.Any(patrol => WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol));
    }

    private void EnsureSiteExplorationStateReady(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (!IsSiteExplorationActive(site, definition))
        {
            return;
        }

        WorldSiteExplorationService.ReconcilePatrolStates(site, definition);
        if (_activeGridMap.TryGetTopSurfacePosition(
                new GridPosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY),
                out GridSurfacePosition current) &&
            current.Height == site.Exploration.CurrentCellHeight)
        {
            return;
        }

        if (TryResolveExplorationEntrySurface(site, definition, out GridSurfacePosition entry))
        {
            site.Exploration.CurrentCellX = entry.X;
            site.Exploration.CurrentCellY = entry.Y;
            site.Exploration.CurrentCellHeight = entry.Height;
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
            return;
        }

        SiteExplorationRouteCellDefinition start = definition.ExplorationPatrols
            .SelectMany(patrol => patrol.RouteCells)
            .FirstOrDefault(cell => _activeGridMap.TryGetTopSurfacePosition(new GridPosition(cell.CellX, cell.CellY), out _));
        if (start == null)
        {
            return;
        }

        site.Exploration.CurrentCellX = start.CellX;
        site.Exploration.CurrentCellY = start.CellY;
        site.Exploration.CurrentCellHeight = start.CellHeight;
        site.Exploration.IsSimulationPaused = true;
        site.Exploration.PauseReason = SiteExplorationPauseReady;
    }

    private bool TryResolveExplorationEntrySurface(
        WorldSiteState site,
        WorldSiteDefinition definition,
        out GridSurfacePosition entry)
    {
        entry = default;
        if (site == null || definition == null)
        {
            return false;
        }

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement != null &&
            _activeGridMap.TryGetTopSurfacePosition(new GridPosition(partyPlacement.CellX, partyPlacement.CellY), out GridSurfacePosition partySurface) &&
            partySurface.Height == partyPlacement.CellHeight)
        {
            entry = partySurface;
            return true;
        }

        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        string unitTypeId = ResolveExplorationPartyUnitTypeId(site);
        bool canEnterWater = false;
        if (_battleUnitFactory.TryGetUnitDefinition(unitTypeId, out BattleUnitDefinition unitDefinition))
        {
            canEnterWater = unitDefinition.CanEnterWater;
        }

        WorldSiteAttackDirection direction = army.TargetApproachDirection == WorldSiteAttackDirection.Any
            ? WorldSiteAttackDirection.West
            : army.TargetApproachDirection;
        bool foundCandidate = TryResolveFirstDeploymentCandidate(direction, canEnterWater, out WorldSiteDeploymentCell candidate);
        if (!foundCandidate)
        {
            foundCandidate = TryResolveFirstDeploymentCandidate(WorldSiteAttackDirection.Any, canEnterWater, out candidate);
        }

        if (!foundCandidate)
        {
            return false;
        }

        // Infiltration starts from the same site edge as battle deployment; exploration does not choose a hidden fallback start.
        entry = new GridSurfacePosition(candidate.Cell.X, candidate.Cell.Y, candidate.Height);
        partyPlacement.CellX = entry.X;
        partyPlacement.CellY = entry.Y;
        partyPlacement.CellHeight = entry.Height;
        return true;
    }

    private bool TryResolveFirstDeploymentCandidate(
        WorldSiteAttackDirection direction,
        bool canEnterWater,
        out WorldSiteDeploymentCell candidate)
    {
        candidate = default;
        foreach (WorldSiteDeploymentCell item in _deploymentCache?.GetCandidates(direction) ?? System.Array.Empty<WorldSiteDeploymentCell>())
        {
            if (!CanUseDeploymentCell(item, canEnterWater))
            {
                continue;
            }

            candidate = item;
            return true;
        }

        return false;
    }

    private static string ResolveExplorationPatrolName(WorldSiteDefinition definition, string patrolId)
    {
        return definition?.ExplorationPatrols.FirstOrDefault(patrol => patrol.Id == patrolId)?.DisplayName ??
               patrolId ??
               "";
    }

    private void OnFacilitySlotEntityPressed(string slotId)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        FacilitySlotDefinition slot = definition?.FacilitySlots.FirstOrDefault(item => item.SlotId == slotId);
        if (site == null || slot == null)
        {
            StrategicWorldRuntime.LastNotice = "建筑点状态已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            return;
        }

        _selectedPlacementId = "";
        _selectedFacilitySlotId = slot.SlotId;

        if (_siteFacilitySlotEntities.TryGetValue(slot.SlotId, out WorldFacilitySlotEntity slotEntity) &&
            slotEntity.HasConfigurationError)
        {
            string notice = $"{slot.DisplayName}配置错误：{slotEntity.ConfigurationError}";
            RefreshSiteManagementUi(notice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot selection blocked by configuration site={site.SiteId} slot={slot.SlotId} reason={slotEntity.ConfigurationError}");
            GetViewport().SetInputAsHandled();
            return;
        }

        FacilityInstance existingFacility = ResolveFacilityInSlot(site, slot.SlotId);
        if (existingFacility != null)
        {
            StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
            string facilityName = queries.GetFacility(existingFacility.FacilityId)?.DisplayName ?? existingFacility.FacilityId;
            string notice = $"{slot.DisplayName}已有{facilityName}，状态：{GetFacilityStateLabel(existingFacility.State)}。";
            RefreshSiteManagementUi(notice);
            GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected site={site.SiteId} slot={slot.SlotId} facility={existingFacility.FacilityId} state={existingFacility.State}");
            GetViewport().SetInputAsHandled();
            return;
        }

        IReadOnlyList<WorldActionViewModel> buildActions = ResolveBuildActionsForSlot(site, slot);
        if (buildActions.Count == 0)
        {
            string notice = $"{slot.DisplayName}暂时没有可建建筑。";
            RefreshSiteManagementUi(notice);
            GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected without action site={site.SiteId} slot={slot.SlotId}");
            GetViewport().SetInputAsHandled();
            return;
        }

        bool hasEnabledBuildAction = buildActions.Any(action => action.IsEnabled);
        string selectedNotice = hasEnabledBuildAction
            ? $"{slot.DisplayName}已选中。请在右侧选择要建造的建筑。"
            : $"{slot.DisplayName}已选中，但当前资源或条件不足。可在右侧查看不可建原因。";
        RefreshSiteManagementUi(selectedNotice);
        GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected for build site={site.SiteId} slot={slot.SlotId} actions={buildActions.Count} enabled={hasEnabledBuildAction}");
        GetViewport().SetInputAsHandled();
    }

    private void SelectPlacement(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        _selectedFacilitySlotId = "";
        RefreshSiteManagementUi();
        GameLog.Info(nameof(WorldSiteRoot), $"Site placement selected site={_siteHudSiteId} placement={_selectedPlacementId}");
    }

    private void OnPlacementEntityPressed(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        _selectedFacilitySlotId = "";
        UpdateSitePeacetimePanelVisibility("placement_selected");
        _draggedPlacementId = _selectedPlacementId;
        if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out Node2D entity))
        {
            _draggedPlacementOriginGlobalPosition = entity.GlobalPosition;
            entity.GlobalPosition = GetGlobalMousePosition();
            SetSitePlacementSelected(entity, true);
            UpdateSiteDeploymentDragPreview(entity);
        }

        _siteSelectionLabel.Text = $"正在调整：{BuildPlacementDisplayName(_selectedPlacementId)}";
        GetViewport().SetInputAsHandled();
    }

    private bool TryHandleFacilitySlotInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event))
        {
            return false;
        }

        if (!TryResolveFacilitySlotUnderPointer(out string slotId))
        {
            return false;
        }

        OnFacilitySlotEntityPressed(slotId);
        return true;
    }

    private bool TryHandleSiteContextClearInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            string.IsNullOrWhiteSpace(_selectedFacilitySlotId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event) ||
            TryResolvePlacementUnderPointer(out _))
        {
            return false;
        }

        string previousSlotId = _selectedFacilitySlotId;
        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        RefreshSiteManagementUi();
        GameLog.Info(nameof(WorldSiteRoot), $"Site facility slot selection cleared site={_siteHudSiteId} previousSlot={previousSlotId}");
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void HandleSiteDeploymentDragInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_draggedPlacementId))
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } &&
                !IsPointerOverSiteHud(@event) &&
                TryResolvePlacementUnderPointer(out string pressedPlacementId))
            {
                OnPlacementEntityPressed(pressedPlacementId);
            }

            return;
        }

        if (@event is InputEventMouseMotion)
        {
            if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out Node2D entity))
            {
                entity.GlobalPosition = GetGlobalMousePosition();
                UpdateSiteDeploymentDragPreview(entity);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            return;
        }

        string placementId = _draggedPlacementId;
        _sitePlacementEntities.TryGetValue(placementId, out Node2D draggedEntity);
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        bool canDrop = TryEvaluateSiteDeploymentTarget(
            placementId,
            site,
            definition,
            out GridPosition gridPosition,
            out bool hasGridPosition,
            out string failureReason);
        _draggedPlacementId = "";
        ClearSiteDeploymentDragPreview(draggedEntity);

        if (!canDrop)
        {
            ReturnDraggedPlacementToOrigin(draggedEntity);
            RefreshSiteManagementUi(FormatPlacementFailure(failureReason));
            GameLog.Info(nameof(WorldSiteRoot), $"Site placement drag cancelled site={_siteHudSiteId} placement={placementId} hasGrid={hasGridPosition} reason={failureReason}");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_deploymentService.TryMovePlacement(
                site,
                definition,
                placementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                out failureReason))
        {
            RefreshSiteManagementUi("驻军位置已更新。");
        }
        else
        {
            ReturnDraggedPlacementToOrigin(draggedEntity);
            RefreshSiteManagementUi(FormatPlacementFailure(failureReason));
            GameLog.Info(nameof(WorldSiteRoot), $"Site placement drag rejected site={_siteHudSiteId} placement={placementId} cell={gridPosition} reason={failureReason}");
        }

        GetViewport().SetInputAsHandled();
    }

    private void UpdateSiteDeploymentDragPreview(Node2D entity)
    {
        if (entity == null)
        {
            return;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        bool canDrop = TryEvaluateSiteDeploymentTarget(
            _draggedPlacementId,
            site,
            definition,
            out GridPosition gridPosition,
            out bool hasGridPosition,
            out _);

        if (hasGridPosition && !canDrop)
        {
            _highlightOverlay?.SetCells(BattleGridHighlightKind.Invalid, new[] { gridPosition });
            return;
        }

        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
    }

    private bool TryEvaluateSiteDeploymentTarget(
        string placementId,
        WorldSiteState site,
        WorldSiteDefinition definition,
        out GridPosition gridPosition,
        out bool hasGridPosition,
        out string failureReason)
    {
        gridPosition = default;
        hasGridPosition = false;
        failureReason = "";

        if (!TryGetMouseGridPosition(out gridPosition))
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        hasGridPosition = true;
        if (!CanPlaceSiteDeploymentOnGridCell(placementId, gridPosition, out failureReason))
        {
            return false;
        }

        return _deploymentService.CanMovePlacement(
            site,
            definition,
            placementId,
            new Vector2I(gridPosition.X, gridPosition.Y),
            out failureReason);
    }

    private bool CanPlaceSiteDeploymentOnGridCell(string placementId, GridPosition position, out string failureReason)
    {
        failureReason = "";
        if (_activeGridMap == null)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        if (!_activeGridMap.TryGetTopSurface(position, out GridCellSurface surface) ||
            !surface.IsWalkable ||
            surface.MoveCost <= 0)
        {
            failureReason = "placement_cell_blocked";
            return false;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        if (!ResolvePlacementCanEnterWater(placement) && BattleRuleQueries.IsWater(surface))
        {
            failureReason = "placement_cell_water";
            return false;
        }

        return true;
    }

    private void ClearSiteDeploymentDragPreview(Node2D entity)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        SetSitePlacementSelected(entity, false);
    }

    private void ReturnDraggedPlacementToOrigin(Node2D entity)
    {
        if (entity == null)
        {
            return;
        }

        entity.GlobalPosition = _draggedPlacementOriginGlobalPosition;
        SetSitePlacementSelected(entity, false);
    }

    private void ExecuteSiteAction(WorldActionViewModel action, string targetSlotId = "")
    {
        if (action == null)
        {
            return;
        }

        WorldActionRequest request = new()
        {
            ActionId = action.ActionId,
            ActorFactionId = StrategicWorldRuntime.State.PlayerFactionId,
            SourceSiteId = _siteHudSiteId,
            TargetSiteId = action.TargetSiteId,
            TargetSlotId = targetSlotId ?? "",
            ThreatId = action.ThreatId
        };

        string returnScenePath = string.IsNullOrWhiteSpace(_siteHudReturnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : _siteHudReturnScenePath;
        SiteBattleLaunchRollback rollback = CaptureSiteBattleLaunchRollback(_siteHudSiteId);
        WorldActionResult result = _worldActionResolver.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            returnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);

        StrategicWorldRuntime.LastNotice = result.Message;
        ApplyModeTransitionRollbackEvent(rollback, result.Events);
        if (!result.Success)
        {
            RefreshSiteManagementUi(result.Message);
            return;
        }

        if (result.BattleStartRequest != null)
        {
            BattleSessionHandoff.BeginBattle(result.BattleStartRequest);
            ApplyBattleStartRequest();
            if (!ActivateBattleRuntime())
            {
                BattleSessionHandoff.CancelBattle();
                RollbackSiteBattleLaunch(rollback, result.BattleStartRequest, _battleStartBlockedReason);
                StrategicWorldRuntime.LastNotice = "无法进入战棋战斗。";
                RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
                GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter site battle request={result.BattleStartRequest.RequestId} target={result.BattleStartRequest.TargetSiteId} reason={_battleStartBlockedReason}");
            }

            return;
        }

        RefreshSiteManagementUi(result.Message);
    }

    private SiteBattleLaunchRollback CaptureSiteBattleLaunchRollback(string siteId)
    {
        SiteBattleLaunchRollback rollback = new()
        {
            SiteId = siteId ?? ""
        };

        WorldSiteState site = ResolveSiteState(rollback.SiteId);
        if (site != null)
        {
            rollback.HasPreviousSiteMode = true;
            rollback.PreviousSiteMode = site.SiteMode;
            if (site.Exploration != null)
            {
                rollback.HasPreviousExplorationState = true;
                rollback.PreviousExplorationPaused = site.Exploration.IsSimulationPaused;
                rollback.PreviousExplorationPauseReason = site.Exploration.PauseReason ?? "";
                rollback.PreviousActiveAlertPatrolId = site.Exploration.ActiveAlertPatrolId ?? "";
                rollback.PreviousPendingPathCellKeys.Clear();
                rollback.PreviousPendingPathCellKeys.AddRange(site.Exploration.PendingPathCellKeys);
            }
        }

        return rollback;
    }

    private static void ApplyModeTransitionRollbackEvent(
        SiteBattleLaunchRollback rollback,
        IReadOnlyCollection<GameEvent> transitionEvents)
    {
        if (rollback == null || transitionEvents == null)
        {
            return;
        }

        GameEvent modeEvent = transitionEvents.LastOrDefault(gameEvent =>
            gameEvent.Kind == "SiteModeChanged" &&
            gameEvent.TargetIds.Contains(rollback.SiteId) &&
            gameEvent.Payload.TryGetValue("to", out string toMode) &&
            toMode == WorldSiteMode.Wartime.ToString() &&
            gameEvent.Payload.TryGetValue("from", out _));
        if (modeEvent == null ||
            !modeEvent.Payload.TryGetValue("from", out string fromMode) ||
            !System.Enum.TryParse(fromMode, out WorldSiteMode previousMode))
        {
            return;
        }

        rollback.HasPreviousSiteMode = true;
        rollback.PreviousSiteMode = previousMode;
    }

    private void RollbackSiteBattleLaunch(
        SiteBattleLaunchRollback rollback,
        BattleStartRequest request,
        string reason)
    {
        string siteId = !string.IsNullOrWhiteSpace(request?.TargetSiteId)
            ? request.TargetSiteId
            : rollback?.SiteId ?? "";
        WorldSiteState site = ResolveSiteState(siteId);
        if (site == null)
        {
            return;
        }

        WorldSiteMode currentMode = site.SiteMode;
        if (rollback?.HasPreviousSiteMode == true)
        {
            _siteModeTransitions.RestoreMode(
                site,
                rollback.PreviousSiteMode,
                StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
                "battle_launch_rollback",
                request?.RequestId ?? "");
        }

        if (rollback?.HasPreviousExplorationState == true && site.Exploration != null)
        {
            site.Exploration.IsSimulationPaused = rollback.PreviousExplorationPaused;
            site.Exploration.PauseReason = rollback.PreviousExplorationPauseReason;
            site.Exploration.ActiveAlertPatrolId = rollback.PreviousActiveAlertPatrolId;
            site.Exploration.PendingPathCellKeys.Clear();
            site.Exploration.PendingPathCellKeys.AddRange(rollback.PreviousPendingPathCellKeys);
        }

        ClearBattleEntities();
        ClearBattleCorpsRuntime();
        SetBattleRuntimeEnabled(false);
        GameLog.Warn(
            nameof(WorldSiteRoot),
            $"Site battle launch rolled back site={site.SiteId} fromMode={currentMode} toMode={site.SiteMode} reason={reason}");
    }

    private string ResolveSelectedThreatId(WorldSiteState site)
    {
        return site?.PendingThreatIds
            .Select(id => StrategicWorldRuntime.State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .FirstOrDefault(threat => threat?.Stage == ThreatStage.Attacking)
            ?.Id ?? "";
    }

    private void RefreshSelectedSlotLabel(WorldSiteState site)
    {
        if (!string.IsNullOrWhiteSpace(_selectedPlacementId))
        {
            WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == _selectedPlacementId);
            _siteSelectionLabel.Text = placement == null
                ? ""
                : $"已选择：{BuildPlacementDisplayName(placement)}\n位置：{placement.CellX}, {placement.CellY}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedFacilitySlotId))
        {
            WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId ?? _siteHudSiteId);
            FacilitySlotDefinition slot = definition?.FacilitySlots.FirstOrDefault(item => item.SlotId == _selectedFacilitySlotId);
            if (slot == null)
            {
                _siteSelectionLabel.Text = "";
                return;
            }

            if (_siteFacilitySlotEntities.TryGetValue(slot.SlotId, out WorldFacilitySlotEntity slotEntity) &&
                slotEntity.HasConfigurationError)
            {
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n配置错误：{slotEntity.ConfigurationError}";
                return;
            }

            StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
            FacilityInstance facility = ResolveFacilityInSlot(site, slot.SlotId);
            if (facility != null)
            {
                string facilityName = queries.GetFacility(facility.FacilityId)?.DisplayName ?? facility.FacilityId;
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n{facilityName} · {GetFacilityStateLabel(facility.State)}";
                return;
            }

            IReadOnlyList<WorldActionViewModel> buildActions = ResolveBuildActionsForSlot(site, slot);
            if (buildActions.Count == 0)
            {
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n暂无可建建筑。";
                return;
            }

            int enabledCount = buildActions.Count(action => action.IsEnabled);
            string state = enabledCount > 0
                ? $"可建 {enabledCount}/{buildActions.Count} 项。请在“可建建筑”中选择。"
                : $"当前 {buildActions.Count} 项建筑都不可建，请查看按钮原因。";
            _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n{state}";
            return;
        }

        _siteSelectionLabel.Text = "";
    }

    private BattleEntity CreateSitePlacementUnitEntity(WorldSiteUnitPlacement placement, WorldSiteState site)
    {
        if (placement == null)
        {
            return null;
        }

        var force = new BattleForceRequest
        {
            ForceId = placement.PlacementId,
            SourceKind = placement.SourceKind,
            SourceId = placement.SourceId,
            UnitDefinitionId = placement.UnitTypeId,
            Count = 1,
            FactionId = placement.FactionId
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = placement.PlacementId,
            CellX = placement.CellX,
            CellY = placement.CellY,
            CellHeight = placement.CellHeight
        });

        var fallbackPosition = new GridPosition(placement.CellX, placement.CellY);
        BattleEntity entity = _battleUnitFactory.Create(
            force,
            0,
            ResolveBattleFaction(placement.FactionId),
            fallbackPosition);
        if (entity == null)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Site placement unit skipped because animated unit could not be created site={site?.SiteId ?? ""} placement={placement.PlacementId} unit={placement.UnitTypeId}");
            return null;
        }

        entity.Name = $"{placement.PlacementId.Replace(':', '_').Replace('-', '_')}SiteUnit";
        return entity;
    }

    private void ConfigureSitePlacementUnitEntity(BattleEntity entity, WorldSiteUnitPlacement placement)
    {
        if (entity == null || placement == null)
        {
            return;
        }

        entity.InputPickable = false;
        entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        entity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(placement.PlacementId == _selectedPlacementId);

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant != null)
        {
            SyncSitePlacementGridOccupant(entity, placement);
        }
    }

    private void SyncSitePlacementGridOccupant(BattleEntity entity, WorldSiteUnitPlacement placement)
    {
        if (entity == null || placement == null)
        {
            return;
        }

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        gridOccupant.GridX = placement.CellX;
        gridOccupant.GridY = placement.CellY;
        gridOccupant.GridHeight = placement.CellHeight;
        gridOccupant.UseExplicitHeight = placement.CellHeight > 0;
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }

    private static void SetSitePlacementSelected(Node2D entity, bool selected)
    {
        if (entity is BattleEntity battleEntity)
        {
            battleEntity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(selected);
        }
    }

    private bool TryResolveFacilitySlotUnderPointer(out string slotId)
    {
        slotId = "";
        if (_siteFacilitySlotEntities.Count == 0 ||
            _activeSiteMap?.GetNodeOrNull<CanvasItem>(FacilitySlotsRootName)?.Visible == false)
        {
            return false;
        }

        if (TryGetMouseGridPosition(out GridPosition gridPosition))
        {
            KeyValuePair<string, WorldFacilitySlotRuntimeLayout>? layoutHit = _siteFacilitySlotLayouts
                .Where(item => item.Value.FootprintCells.Contains(gridPosition) &&
                               _siteFacilitySlotEntities.ContainsKey(item.Key))
                .OrderByDescending(item => item.Value.ZIndex)
                .Select(item => (KeyValuePair<string, WorldFacilitySlotRuntimeLayout>?)item)
                .FirstOrDefault();
            if (layoutHit.HasValue)
            {
                slotId = layoutHit.Value.Key;
                return true;
            }
        }

        return false;
    }

    private bool TryResolvePlacementUnderPointer(out string placementId)
    {
        placementId = "";
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (!CanOpenSiteDetail(site))
        {
            return false;
        }

        if (TryGetMouseGridPosition(out GridPosition gridPosition))
        {
            WorldSiteUnitPlacement placement = site.UnitPlacements
                .Where(item => item.CellX == gridPosition.X && item.CellY == gridPosition.Y)
                .OrderByDescending(item => item.CellHeight)
                .FirstOrDefault();
            if (placement != null && _sitePlacementEntities.ContainsKey(placement.PlacementId))
            {
                placementId = placement.PlacementId;
                return true;
            }
        }

        Vector2 pointerGlobal = GetGlobalMousePosition();
        float maxDistanceSquared = SitePlacementPickRadiusPixels * SitePlacementPickRadiusPixels;
        KeyValuePair<string, Node2D>? nearest = _sitePlacementEntities
            .Where(item => item.Value != null && item.Value.GlobalPosition.DistanceSquaredTo(pointerGlobal) <= maxDistanceSquared)
            .OrderBy(item => item.Value.GlobalPosition.DistanceSquaredTo(pointerGlobal))
            .Select(item => (KeyValuePair<string, Node2D>?)item)
            .FirstOrDefault();
        if (nearest.HasValue)
        {
            placementId = nearest.Value.Key;
            return true;
        }

        return false;
    }

    private bool IsPointerOverSiteHud(InputEvent @event)
    {
        if (_siteHudRoot?.Visible != true)
        {
            return false;
        }

        Vector2 screenPosition = @event switch
        {
            InputEventMouseButton mouseButton => mouseButton.Position,
            InputEventMouseMotion mouseMotion => mouseMotion.Position,
            _ => new Vector2(float.NaN, float.NaN)
        };
        return !float.IsNaN(screenPosition.X) &&
               (IsScreenPointInsideControl(_siteHudTopBar, screenPosition) ||
                IsScreenPointInsideControl(_sitePeacetimePanel, screenPosition));
    }

    private bool IsPointerOverSiteExplorationHud(InputEvent @event)
    {
        Vector2 screenPosition = @event switch
        {
            InputEventMouseButton mouseButton => mouseButton.Position,
            InputEventMouseMotion mouseMotion => mouseMotion.Position,
            _ => new Vector2(float.NaN, float.NaN)
        };

        return !float.IsNaN(screenPosition.X) &&
               IsScreenPointInsideControl(_siteExplorationHudPanel, screenPosition);
    }

    private static bool IsScreenPointInsideControl(Control control, Vector2 screenPosition)
    {
        if (!IsLiveNode(control))
        {
            return false;
        }

        return control.Visible && control.GetGlobalRect().HasPoint(screenPosition);
    }

    private BattleFaction ResolveBattleFaction(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return BattleFaction.Neutral;
        }

        return factionId == StrategicWorldRuntime.State?.PlayerFactionId ||
               factionId == StrategicWorldIds.FactionPlayer
            ? BattleFaction.Player
            : BattleFaction.Enemy;
    }

    private Vector2 ResolvePlacementEntityGlobalPosition(WorldSiteUnitPlacement placement)
    {
        GridPosition gridPosition = new(placement.CellX, placement.CellY);
        if (TryGetCellGlobalPosition(gridPosition, out Vector2 globalPosition))
        {
            return globalPosition;
        }

        return GlobalPosition + new Vector2(96.0f, 128.0f + _sitePlacementEntities.Count * 32.0f);
    }

    private bool TrySnapFacilitySlotEntity(
        WorldFacilitySlotEntity entity,
        FacilitySlotDefinition slot,
        Dictionary<GridPosition, string> occupiedCells,
        out WorldFacilitySlotRuntimeLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = "";
        if (entity == null || slot == null)
        {
            failureReason = "slot_entity_missing";
            return false;
        }

        if (_coordinateLayer == null || _activeGridMap == null)
        {
            failureReason = "grid_missing";
            return false;
        }

        if (!TryResolveGridCellAtGlobalPosition(entity.GlobalPosition, out GridPosition anchorCell))
        {
            failureReason = "slot_anchor_cell_invalid";
            return false;
        }

        Vector2I footprintSize = ResolveFacilitySlotFootprintSize(entity);
        var footprintCells = new List<GridPosition>(footprintSize.X * footprintSize.Y);
        for (int y = 0; y < footprintSize.Y; y++)
        {
            for (int x = 0; x < footprintSize.X; x++)
            {
                GridPosition cell = new(anchorCell.X + x, anchorCell.Y + y);
                if (!_activeGridMap.TryGetCell(cell, out _))
                {
                    failureReason = $"footprint_cell_missing anchor={anchorCell} size={footprintSize.X}x{footprintSize.Y} cell={cell}";
                    return false;
                }

                footprintCells.Add(cell);
            }
        }

        foreach (GridPosition cell in footprintCells)
        {
            if (occupiedCells.TryGetValue(cell, out string occupiedSlotId))
            {
                failureReason = $"footprint_overlap other={occupiedSlotId} cell={cell}";
                return false;
            }
        }

        GridPosition sortCell = ResolveFacilitySlotSortCell(entity, footprintCells);
        GridSurfacePosition sortSurface = ResolveFacilitySlotSortSurface(sortCell);
        if (!TryGetCellGlobalPosition(anchorCell, out Vector2 rootGlobalPosition))
        {
            failureReason = $"anchor_cell_invalid cell={anchorCell}";
            return false;
        }

        entity.ApplySnappedLayout(rootGlobalPosition);
        entity.SetFootprintPolygons(footprintCells.Select(BuildCellPolygonGlobal).ToArray());
        int zIndex = ApplyFacilitySlotRenderSort(entity, sortSurface);

        layout = new WorldFacilitySlotRuntimeLayout
        {
            SlotId = slot.SlotId,
            SortCell = sortCell,
            SortSurface = sortSurface,
            FootprintWidth = footprintSize.X,
            FootprintHeight = footprintSize.Y,
            ZIndex = zIndex
        };
        layout.FootprintCells.AddRange(footprintCells);

        foreach (GridPosition cell in footprintCells)
        {
            occupiedCells[cell] = slot.SlotId;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Facility slot footprint snapped slot={slot.SlotId} anchor={anchorCell} size={footprintSize.X}x{footprintSize.Y} cells={footprintCells.Count} sort={sortSurface}");
        return true;
    }

    private static Vector2I ResolveFacilitySlotFootprintSize(WorldFacilitySlotEntity entity)
    {
        return new Vector2I(
            System.Math.Clamp(entity?.FootprintWidth ?? 1, 1, 12),
            System.Math.Clamp(entity?.FootprintHeight ?? 1, 1, 12));
    }

    private string BuildFacilitySlotFootprintLogSummary()
    {
        if (_siteFacilitySlotLayouts.Count == 0)
        {
            return "";
        }

        return string.Join(
            ",",
            _siteFacilitySlotLayouts.Values
                .OrderBy(layout => layout.SlotId)
                .Select(layout => $"{layout.SlotId}:{layout.FootprintWidth}x{layout.FootprintHeight}"));
    }

    private Vector2[] BuildCellPolygonGlobal(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        Vector2 center = _coordinateLayer.MapToLocal(origin);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => _coordinateLayer.ToGlobal(point))
            .ToArray();
    }

    private bool TryResolveGridCellAtGlobalPosition(Vector2 globalPosition, out GridPosition gridPosition)
    {
        gridPosition = default;
        if (_coordinateLayer == null || _activeGridMap == null)
        {
            return false;
        }

        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(globalPosition));
        gridPosition = new GridPosition(tilePosition.X, tilePosition.Y);
        return _activeGridMap.TryGetCell(gridPosition, out _);
    }

    private GridPosition ResolveFacilitySlotSortCell(WorldFacilitySlotEntity entity, IReadOnlyList<GridPosition> footprintCells)
    {
        if (entity?.UseLowestFootprintCellAsSortAnchor != false || !TryResolveGridCellAtGlobalPosition(entity.GlobalPosition, out GridPosition rootCell))
        {
            return footprintCells
                .OrderByDescending(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
        }

        return rootCell;
    }

    private GridSurfacePosition ResolveFacilitySlotSortSurface(GridPosition sortCell)
    {
        if (_activeGridMap?.TryGetTopSurfacePosition(sortCell, out GridSurfacePosition topSurface) == true)
        {
            return topSurface;
        }

        if (_activeGridMap?.TryGetCell(sortCell, out GridCell cell) == true)
        {
            return new GridSurfacePosition(sortCell, cell.Height);
        }

        return new GridSurfacePosition(sortCell, 0);
    }

    private int ApplyFacilitySlotRenderSort(WorldFacilitySlotEntity entity, GridSurfacePosition sortSurface)
    {
        int zIndex = ResolveFacilitySlotZIndex(sortSurface);
        if (entity == null)
        {
            return zIndex;
        }

        entity.ZAsRelative = false;
        entity.ZIndex = zIndex;
        return zIndex;
    }

    private int ResolveFacilitySlotZIndex(GridSurfacePosition sortSurface)
    {
        int zIndex = BattleRenderSortPolicy.GetUnitZIndex(sortSurface.Height);
        if (_activeSiteMap is not BattleMapView battleMapView)
        {
            return zIndex;
        }

        if (battleMapView.RenderSortCache?.TryGetYSortOriginUnitZIndex(sortSurface, out int ySortOriginZIndex) == true)
        {
            return ySortOriginZIndex;
        }

        return TryResolveObjectLayerZIndex(battleMapView, sortSurface, out int objectLayerZIndex)
            ? objectLayerZIndex
            : zIndex;
    }

    private static bool TryResolveObjectLayerZIndex(BattleMapView mapView, GridSurfacePosition sortSurface, out int zIndex)
    {
        zIndex = 0;
        if (mapView == null)
        {
            return false;
        }

        Vector2I tilePosition = new(sortSurface.X, sortSurface.Y);
        bool foundExactObjectTile = false;
        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView)
                     .Where(layer => layer.Role == LayerRole.Object && layer.Height == sortSurface.Height))
        {
            TileData tileData = layer.GetCellTileData(tilePosition);
            if (tileData == null)
            {
                continue;
            }

            int candidateZIndex = layer.ZIndex + tileData.ZIndex;
            zIndex = foundExactObjectTile ? Mathf.Max(zIndex, candidateZIndex) : candidateZIndex;
            foundExactObjectTile = true;
        }

        if (foundExactObjectTile)
        {
            return true;
        }

        bool foundObjectLayer = false;
        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView)
                     .Where(layer => layer.Role == LayerRole.Object && layer.Height == sortSurface.Height))
        {
            zIndex = foundObjectLayer ? Mathf.Max(zIndex, layer.ZIndex) : layer.ZIndex;
            foundObjectLayer = true;
        }

        return foundObjectLayer;
    }

    private FacilityInstance ResolveFacilityInSlot(WorldSiteState site, string slotId)
    {
        return string.IsNullOrWhiteSpace(slotId)
            ? null
            : site?.Facilities.FirstOrDefault(item => item.SlotId == slotId && item.State != FacilityState.Destroyed);
    }

    private IReadOnlyList<WorldActionViewModel> ResolveBuildActionsForSlot(WorldSiteState site, FacilitySlotDefinition slot)
    {
        if (site == null ||
            slot == null)
        {
            return System.Array.Empty<WorldActionViewModel>();
        }

        HashSet<string> actionIds = ResolveBuildActionIdsForSlot(slot).ToHashSet();
        if (actionIds.Count == 0)
        {
            return System.Array.Empty<WorldActionViewModel>();
        }

        IReadOnlyList<WorldActionViewModel> actions = _worldActionResolver.GetAvailableActions(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            _siteHudSiteId,
            ResolveSelectedThreatId(site),
            slot.SlotId);
        return actions
            .Where(action => actionIds.Contains(action.ActionId))
            .ToArray();
    }

    private IReadOnlyList<string> ResolveBuildActionIdsForSlot(FacilitySlotDefinition slot)
    {
        if (slot == null)
        {
            return System.Array.Empty<string>();
        }

        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        return queries.Actions.Values
            .Where(action =>
            {
                string facilityId = ResolveAddedFacilityId(action);
                return !string.IsNullOrWhiteSpace(facilityId) &&
                       slot.AllowedFacilityIds.Contains(facilityId);
            })
            .Select(action => action.Id)
            .ToArray();
    }

    private static string ResolveAddedFacilityId(WorldActionDefinition action)
    {
        return action?.Effects
            .FirstOrDefault(effect => effect.Kind == WorldEffectKind.AddFacility &&
                                      !string.IsNullOrWhiteSpace(effect.FacilityId))
            ?.FacilityId ?? "";
    }

    private string BuildPlacementDisplayName(string placementId)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        return placement == null ? placementId : BuildPlacementDisplayName(placement);
    }

    private string BuildPlacementDisplayName(WorldSiteUnitPlacement placement)
    {
        return _battleUnitFactory.ResolveUnitInstanceDisplayName(placement.UnitTypeId, placement.UnitIndex - 1);
    }

    private static string FormatPlacementFailure(string failureReason)
    {
        return failureReason switch
        {
            "placement_cell_occupied" => "无法放置：目标地块已有驻军。",
            "placement_cell_blocked" => "无法放置：目标地块不可行走。",
            "placement_cell_water" => "无法放置：该单位不能进入水域。",
            "placement_cell_invalid" => "无法放置：目标地块无效。",
            "missing_placement" => "无法放置：驻军记录不存在。",
            _ => "无法放置：目标地块无效。"
        };
    }

    private static string BuildAllowedFacilityNames(FacilitySlotDefinition slot, StrategicWorldDefinitionQueries queries)
    {
        return slot.AllowedFacilityIds.Count == 0
            ? "未配置"
            : string.Join("、", slot.AllowedFacilityIds.Select(id => queries.GetFacility(id)?.DisplayName ?? id));
    }

    private static string BuildActionButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
    }

    private static string BuildFacilityBuildButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
    }

    private static string BuildActionTooltip(WorldActionViewModel action)
    {
        if (action == null)
        {
            return "";
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            lines.Add(action.Description);
        }

        lines.AddRange(action.EffectLines);
        lines.AddRange(action.WarningLines);
        if (!action.IsEnabled && !string.IsNullOrWhiteSpace(action.DisabledReason))
        {
            lines.Add(action.DisabledReason);
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private bool IsFacilityBuildAction(string actionId)
    {
        StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
        return !string.IsNullOrWhiteSpace(ResolveAddedFacilityId(queries.GetAction(actionId)));
    }

    private static void AddMutedLine(Container parent, string text)
    {
        Label label = GameUiSceneFactory.CreateWorldMutedLine(nameof(WorldSiteRoot));
        if (label == null)
        {
            return;
        }

        label.Text = text;
        parent.AddChild(label);
    }

    private static void ClearChildren(Node node)
    {
        if (node == null)
        {
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string GetBattleOutcomeLabel(BattleOutcome outcome)
    {
        return outcome switch
        {
            BattleOutcome.Victory => "战斗胜利",
            BattleOutcome.Defeat => "战斗失败",
            BattleOutcome.Withdraw => "已撤退",
            BattleOutcome.Disaster => "惨败",
            BattleOutcome.None => "非战时",
            _ => "战斗结束"
        };
    }

    private static string GetControlStateLabel(SiteControlState state)
    {
        return state switch
        {
            SiteControlState.Unknown => "未知",
            SiteControlState.Neutral => "中立",
            SiteControlState.Hostile => "敌控",
            SiteControlState.Contested => "争夺中",
            SiteControlState.PlayerHeld => "玩家控制",
            SiteControlState.Damaged => "受损",
            SiteControlState.Lost => "丢失",
            _ => "未知"
        };
    }

    private static string GetSiteModeLabel(WorldSiteMode mode)
    {
        return mode switch
        {
            WorldSiteMode.Peacetime => "非战时",
            WorldSiteMode.Alert => "警戒",
            WorldSiteMode.Wartime => "战时",
            WorldSiteMode.Aftermath => "战后",
            _ => "未知"
        };
    }

    private static string GetFacilityStateLabel(FacilityState state)
    {
        return state switch
        {
            FacilityState.Planned => "规划",
            FacilityState.Building => "建造中",
            FacilityState.Active => "运行",
            FacilityState.Damaged => "受损",
            FacilityState.Disabled => "停用",
            FacilityState.Destroyed => "摧毁",
            _ => "未知"
        };
    }

    private static string GetThreatStageLabel(ThreatStage stage)
    {
        return stage switch
        {
            ThreatStage.Hidden => "隐藏",
            ThreatStage.Rumor => "传闻",
            ThreatStage.Marching => "行军中",
            ThreatStage.Attacking => "进攻中",
            ThreatStage.Resolved => "已解决",
            _ => "未知"
        };
    }

    private string GetUnitLabel(string unitTypeId)
    {
        return _battleUnitFactory.ResolveUnitDisplayName(unitTypeId);
    }
}
