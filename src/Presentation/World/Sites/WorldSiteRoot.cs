using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Auto;
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

public partial class WorldSiteRoot : Node2D
{
    private const float SitePlacementPickRadiusPixels = 42.0f;
    private const string SiteExplorationHudScenePath = "res://scenes/world/sites/SiteExplorationHud.tscn";
    private const string FacilitySlotsRootName = "FacilitySlots";
    private const string SiteExplorationPauseReady = "exploration_ready";
    private const string SiteExplorationPauseMovePreview = "exploration_move_preview";
    private const string SiteExplorationPauseAlertRadius = "exploration_alert_radius";

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

    [Signal]
    public delegate void SiteMapLoadedEventHandler(Node activeSiteMap);

    [Export]
    public NodePath MapRootPath { get; set; } = new("MapRoot");

    [Export]
    public NodePath UnitRootPath { get; set; } = new("UnitRoot");

    [Export]
    public NodePath HighlightOverlayPath { get; set; } = new("OverlayRoot/GridHighlightOverlay");

    [Export]
    public NodePath SelectionVignetteOverlayPath { get; set; } = new("CanvasLayer/SelectionVignetteOverlay");

    [Export]
    public NodePath BattleCameraPath { get; set; } = new("Camera2D");

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
    private readonly BattleUnitFactory _battleUnitFactory = new();
    private readonly WorldBattleResultApplier _worldBattleResultApplier = new();
    private readonly WorldActionResolver _worldActionResolver;
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldSiteRuntimeDeploymentCacheBuilder _deploymentCacheBuilder = new();
    private readonly WorldSiteDeploymentTargetEvaluator _deploymentTargetEvaluator = new();
    private readonly WorldSiteDeploymentTerrainReconciler _deploymentTerrainReconciler = new();
    private readonly WorldSiteBattleDeploymentPreparer _battleDeploymentPreparer = new();
    private readonly WorldSiteBattleLauncher _battleLauncher = new();
    private readonly WorldSiteAutoBattleAdapter _autoBattleAdapter = new();
    private readonly AutoBattleReportSummaryFormatter _autoBattleReportSummaryFormatter = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private AutoBattleReport _lastAutoBattleReport;

    public Node ActiveSiteMap => _activeSiteMap;
    public BattleGridMap ActiveGridMap => _activeGridMap;
    public BattleEntity SelectedEntity => null;
    public bool AllowsDebugHoverInfo => RuntimeMode != WorldSiteRuntimeMode.Battle;
    public bool IsEnemyPhaseRunning => false;
    public WorldSiteRuntimeMode RuntimeMode => ResolveRuntimeMode();

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
        _selectionVignetteOverlay = GetNodeOrNull<BattleSelectionVignetteOverlay>(SelectionVignetteOverlayPath);
        _battleCamera = GetNodeOrNull<BattleCameraController>(BattleCameraPath);
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

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Ready mapRoot={_mapRoot?.GetPath()} unitRoot={_unitRoot?.GetPath()} highlight={_highlightOverlay != null}");

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
        _deploymentCache = _deploymentCacheBuilder.Build(siteId, _activeGridMap);
        if (_activeGridMap == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot build site deployment cache site={siteId} reason=grid_missing");
            return;
        }

        string counts = string.Join(
            " ",
            WorldSiteRuntimeDeploymentCacheBuilder.SupportedDirections
                .Select(direction => $"{direction}={_deploymentCache.GetCandidates(direction).Count}"));
        GameLog.Info(nameof(WorldSiteRoot), $"SiteDeploymentCacheBuilt site={siteId} surfaces={_deploymentCache.CandidateSurfaceCount} {counts}");
    }

    private void ApplyBattleStartRequest()
    {
        _battleStartBlockedReason = "";
        if (_unitRoot == null || !BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
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
            var reservedDeploymentSurfaces = new HashSet<GridSurfacePosition>();
            AddRequestedForces(request.PlayerForces, BattleFaction.Player, request, reservedDeploymentSurfaces);
            AddRequestedForces(request.EnemyForces, BattleFaction.Enemy, request, reservedDeploymentSurfaces);
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

        return ActivateAutoBattleRuntime();
    }

    private bool ActivateAutoBattleRuntime()
    {
        if (!_autoBattleAdapter.TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult resolution))
        {
            _battleStartBlockedReason = string.IsNullOrWhiteSpace(resolution?.FailureReason)
                ? "auto_battle_activation_failed"
                : resolution.FailureReason;
            GameLog.Warn(nameof(WorldSiteRoot), $"Auto battle activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        _lastAutoBattleReport = resolution.Report;
        WorldActionResult applyResult = ApplyBattleResultToWorld(resolution.Request, resolution.BattleResult);
        string autoBattleNotice = BuildAutoBattleReturnNotice(applyResult, resolution.Report);
        if (!string.IsNullOrWhiteSpace(autoBattleNotice))
        {
            applyResult ??= new WorldActionResult
            {
                Success = true,
                ActionId = "battle_result"
            };
            applyResult.Message = autoBattleNotice;
            StrategicWorldRuntime.LastNotice = autoBattleNotice;
        }

        ReconcileWorldSitePlacementsAfterBattle(
            resolution.Request,
            System.Array.Empty<WorldSiteLivePlacementSnapshot>(),
            resolution.BattleResult.Outcome);
        ClearBattleEntities();
        SetBattleRuntimeEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Auto battle resolved request={resolution.Request?.RequestId ?? ""} outcome={resolution.BattleResult?.Outcome} reportEvents={resolution.Report?.EventFeed.Count ?? 0} failure={resolution.Report?.TopFailureReason ?? ""}");
        SwitchToNonBattleUi(
            resolution.BattleResult.Outcome,
            resolution.Request,
            applyResult,
            resolution.Request?.ReturnScenePath ?? "");
        return true;
    }

    private string BuildAutoBattleReturnNotice(WorldActionResult applyResult, AutoBattleReport report)
    {
        string worldMessage = applyResult?.Message?.Trim() ?? "";
        string reportSummary = _autoBattleReportSummaryFormatter.Format(report).Trim();
        if (string.IsNullOrWhiteSpace(reportSummary))
        {
            return worldMessage;
        }

        if (string.IsNullOrWhiteSpace(worldMessage))
        {
            return reportSummary;
        }

        return $"{worldMessage}\n{reportSummary}";
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

        return _battleDeploymentPreparer.Prepare(
            request,
            site,
            siteDefinition,
            _deploymentCache,
            _activeGridMap,
            ResolveForceCanEnterWater,
            ResolvePlacementCanEnterWater,
            out _);
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

        WorldSiteDeploymentTerrainReconcileResult result = _deploymentTerrainReconciler.Reconcile(
            _activeGridMap,
            _deploymentCache,
            site,
            definition,
            ResolvePlacementCanEnterWater);
        return result.Success;
    }

    private bool ResolvePlacementCanEnterWater(WorldSiteUnitPlacement placement)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(placement?.UnitTypeId, out BattleUnitDefinition definition))
        {
            return false;
        }

        return definition.CanEnterWater;
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
            }
        }
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
            return;
        }

        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            entity.GetParent()?.RemoveChild(entity);
            entity.QueueFree();
        }

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
        bool placedAny = false;
        WorldSiteUnitPlacement refreshedPartyPlacement = null;
        int unitIndex = 0;
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            for (int count = 0; count < unit.Count; count++)
            {
                unitIndex++;
                string placementId = BuildVisitingArmyPlacementId(army.ArmyId, unit.UnitTypeId, unitIndex);
                bool canEnterWater = _battleUnitFactory.TryGetUnitDefinition(unit.UnitTypeId, out BattleUnitDefinition unitDefinition) &&
                                     unitDefinition.CanEnterWater;
                if (!TryResolveKnownPlayerEntranceDeploymentCandidate(
                        site,
                        definition,
                        army.TargetApproachDirection,
                        canEnterWater,
                        out WorldSiteDeploymentCell candidate,
                        out WorldSiteAttackDirection direction,
                        out string entranceId))
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"VisitingArmyPlacementSkipped site={site.SiteId} army={army.ArmyId} unit={unit.UnitTypeId} reason=known_player_entrance_deployment_candidate_missing targetDirection={army.TargetApproachDirection}");
                    continue;
                }

                WorldSiteUnitPlacement existing = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
                if (existing != null)
                {
                    ApplyVisitingArmyPlacementMetadata(existing, army, unit.UnitTypeId, unitIndex, direction, entranceId);
                    existing.CellX = candidate.Cell.X;
                    existing.CellY = candidate.Cell.Y;
                    existing.CellHeight = candidate.Height;
                    placedAny = true;
                    if (refreshedPartyPlacement == null || existing.UnitIndex < refreshedPartyPlacement.UnitIndex)
                    {
                        refreshedPartyPlacement = existing;
                    }
                    continue;
                }

                WorldSiteUnitPlacement placement = new()
                {
                    PlacementId = placementId,
                    UnitTypeId = unit.UnitTypeId,
                    UnitIndex = unitIndex,
                    FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId,
                    PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy,
                    SourceKind = "PlayerArmy",
                    SourceId = army.ArmyId,
                    ArmyId = army.ArmyId,
                    EntranceId = entranceId,
                    AttackDirection = direction,
                    CellX = candidate.Cell.X,
                    CellY = candidate.Cell.Y,
                    CellHeight = candidate.Height
                };
                site.UnitPlacements.Add(placement);
                createdAny = true;
                placedAny = true;
                if (refreshedPartyPlacement == null || placement.UnitIndex < refreshedPartyPlacement.UnitIndex)
                {
                    refreshedPartyPlacement = placement;
                }
            }
        }

        WorldSiteUnitPlacement partyPlacement = refreshedPartyPlacement ?? ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement != null &&
            (ReferenceEquals(partyPlacement, refreshedPartyPlacement) ||
             IsKnownPlayerEntrancePlacement(site, definition, partyPlacement)))
        {
            site.Exploration.CurrentCellX = partyPlacement.CellX;
            site.Exploration.CurrentCellY = partyPlacement.CellY;
            site.Exploration.CurrentCellHeight = partyPlacement.CellHeight;
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
        }
        else if (partyPlacement != null)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"VisitingArmyExplorationCellCopySkipped site={site.SiteId} army={army.ArmyId} placement={partyPlacement.PlacementId} reason=stale_or_unknown_entrance");
        }

        if (createdAny)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"VisitingArmyPlacementEnsured site={site.SiteId} army={army.ArmyId} placements={unitIndex} armyUnits={FormatArmyUnitsForLog(army)} sitePlacements={FormatSitePlacementsForLog(site)}");
        }

        return placedAny;
    }

    private static string BuildVisitingArmyPlacementId(string armyId, string unitTypeId, int index)
    {
        return $"site_army:PlayerArmy:{armyId}:{unitTypeId}:{index}";
    }

    private static void ApplyVisitingArmyPlacementMetadata(
        WorldSiteUnitPlacement placement,
        WorldArmyState army,
        string unitTypeId,
        int index,
        WorldSiteAttackDirection direction,
        string entranceId)
    {
        placement.UnitTypeId = unitTypeId;
        placement.UnitIndex = index;
        placement.FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId;
        placement.PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy;
        placement.SourceKind = "PlayerArmy";
        placement.SourceId = army.ArmyId;
        placement.ArmyId = army.ArmyId;
        placement.EntranceId = entranceId ?? "";
        placement.AttackDirection = direction;
    }

    private static bool IsPlayerArmySitePlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null &&
               placement.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(placement.ArmyId) &&
               placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker;
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

    private bool TryAppendSiteExplorationPointActions(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site?.Exploration == null ||
            definition?.ExplorationPoints == null ||
            definition.ExplorationPoints.Count == 0)
        {
            return false;
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);
        HashSet<string> knownPointIds = new(intel.KnownExplorationPointIds, System.StringComparer.Ordinal);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Exploration.RevealedPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Exploration.ResolvedPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Memory?.RevealedExplorationPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Memory?.ResolvedPointIds);

        int appendedCount = 0;
        foreach (SiteExplorationPointDefinition point in definition.ExplorationPoints)
        {
            if (point == null ||
                string.IsNullOrWhiteSpace(point.Id) ||
                point.Actions.Count == 0 ||
                IsSiteExplorationPointResolved(site, point.Id) ||
                !knownPointIds.Contains(point.Id) ||
                !IsSiteExplorationPointInRange(site.Exploration, point))
            {
                continue;
            }

            foreach (SiteExplorationActionDefinition action in point.Actions.Where(item => item != null))
            {
                Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
                if (button == null)
                {
                    continue;
                }

                button.Text = BuildSiteExplorationPointActionButtonText(point, action);
                button.TooltipText = BuildSiteExplorationPointActionTooltip(point, action);
                button.Pressed += () => ExecuteSiteExplorationPointAction(site.SiteId, point.Id, action.Id);

                _siteActionList.AddChild(button);
                appendedCount++;
            }
        }

        return appendedCount > 0;
    }

    private void ExecuteSiteExplorationPointAction(string siteId, string pointId, string actionId)
    {
        WorldSiteState site = ResolveSiteState(siteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(siteId);
        SiteExplorationPointDefinition point = definition?.ExplorationPoints.FirstOrDefault(item => item.Id == pointId);
        SiteExplorationActionDefinition action = point?.Actions.FirstOrDefault(item => item.Id == actionId);
        if (!IsSiteExplorationActive(site, definition) ||
            point == null ||
            action == null ||
            IsSiteExplorationPointResolved(site, point.Id) ||
            !IsSiteExplorationPointInRange(site.Exploration, point))
        {
            StrategicWorldRuntime.LastNotice = "探索行动已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration point action ignored site={siteId} point={pointId} action={actionId}");
            return;
        }

        if (action.StartsBattle)
        {
            RequestSiteExplorationPointBattle(site, definition, point, action);
            return;
        }

        WorldActionResult applyResult = WorldSiteExplorationService.ApplyActionResult(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site,
            point.Id,
            action);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Site exploration point action executed site={site.SiteId} point={point.Id} action={action.Id} success={applyResult.Success} alert={site.Exploration.AlertLevel}");
        RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
    }

    private static void AddKnownSiteExplorationPointIds(HashSet<string> knownPointIds, IEnumerable<string> pointIds)
    {
        if (knownPointIds == null || pointIds == null)
        {
            return;
        }

        foreach (string id in pointIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                knownPointIds.Add(id);
            }
        }
    }

    private static bool IsSiteExplorationPointResolved(WorldSiteState site, string pointId)
    {
        return site?.Exploration?.ResolvedPointIds.Contains(pointId) == true ||
               site?.Memory?.ResolvedPointIds.Contains(pointId) == true;
    }

    private static bool IsSiteExplorationPointInRange(
        WorldSiteExplorationState exploration,
        SiteExplorationPointDefinition point)
    {
        if (exploration == null || point == null || exploration.CurrentCellHeight != point.CellHeight)
        {
            return false;
        }

        int distance = System.Math.Abs(exploration.CurrentCellX - point.CellX) +
                       System.Math.Abs(exploration.CurrentCellY - point.CellY);
        return distance <= System.Math.Max(0, point.InteractionRange);
    }

    private static string BuildSiteExplorationPointActionButtonText(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        string actionName = ResolveSiteExplorationActionDisplayName(point, action);
        string pointName = ResolveSiteExplorationPointDisplayName(point);
        return $"{actionName}\n{pointName}";
    }

    private static string BuildSiteExplorationPointActionTooltip(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(point?.Description))
        {
            lines.Add(point.Description);
        }
        if (!string.IsNullOrWhiteSpace(action?.Description))
        {
            lines.Add(action.Description);
        }
        if (action?.StartsBattle == true)
        {
            lines.Add("将进入场域遭遇战。");
        }

        return string.Join("\n", lines);
    }

    private static string ResolveSiteExplorationActionDisplayName(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action?.DisplayName))
        {
            return action.DisplayName;
        }

        return string.IsNullOrWhiteSpace(action?.Id)
            ? ResolveSiteExplorationPointDisplayName(point)
            : action.Id;
    }

    private static string ResolveSiteExplorationPointDisplayName(SiteExplorationPointDefinition point)
    {
        return string.IsNullOrWhiteSpace(point?.DisplayName)
            ? point?.Id ?? ""
            : point.DisplayName;
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

    private void RequestSiteExplorationPointBattle(
        WorldSiteState site,
        WorldSiteDefinition definition,
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        if (!IsSiteExplorationActive(site, definition) || point == null || action == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：探索状态已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site?.SiteId ?? ""} point={point?.Id ?? ""} action={action?.Id ?? ""} reason=exploration_context_missing");
            return;
        }

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true ||
            army.GarrisonUnits.Sum(unit => System.Math.Max(0, unit.Count)) <= 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：缺少潜入部队。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason=exploration_army_missing placement={partyPlacement?.PlacementId ?? ""}");
            return;
        }

        SiteExplorationPatrolDefinition[] encounterPatrols = ResolveAliveExplorationEncounterPatrols(site, definition);
        GridSurfacePosition entryCell = new(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight);
        int alertLevel = System.Math.Clamp(site.Exploration.AlertLevel + action.AlertDelta, 0, 5);
        string encounterId = string.IsNullOrWhiteSpace(action.BattleEncounterId) ? action.Id : action.BattleEncounterId;
        BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
            site.SiteId,
            point.Id,
            "",
            army,
            encounterPatrols,
            entryCell,
            alertLevel,
            string.IsNullOrWhiteSpace(_siteHudReturnScenePath) ? "res://scenes/world/StrategicWorldRoot.tscn" : _siteHudReturnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath,
            encounterId);
        WorldSiteIntelService.ApplySiteIntelToRequest(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            site.SiteId);
        if (request.PlayerForces.Count == 0 || request.EnemyForces.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：参战单位不完整。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason=forces_missing playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} patrols={encounterPatrols.Length}");
            return;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"ExplorationPointBattleRequested site={site.SiteId} point={point.Id} action={action.Id} encounter={request.EncounterId} army={army.ArmyId} alert={alertLevel} playerForces={FormatForcesForLog(request.PlayerForces)} enemyForces={FormatForcesForLog(request.EnemyForces)}");

        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
        _siteModeTransitions.EnterBattleFromExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "exploration_point_battle_requested",
            request.RequestId);
        WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
            StrategicWorldRuntime.State,
            request,
            rollback,
            ApplyBattleStartRequest,
            ActivateBattleRuntime,
            () => _battleStartBlockedReason,
            ClearBattleEntities,
            null,
            enabled => SetBattleRuntimeEnabled(enabled));
        if (!launch.Success)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason={launch.FailureReason}");
        }
    }

    private static SiteExplorationPatrolDefinition[] ResolveAliveExplorationEncounterPatrols(
        WorldSiteState site,
        WorldSiteDefinition definition)
    {
        if (site?.Exploration == null || definition?.ExplorationPatrols == null)
        {
            return System.Array.Empty<SiteExplorationPatrolDefinition>();
        }

        SiteExplorationPatrolDefinition[] patrols = definition.ExplorationPatrols
            .Where(patrol =>
                patrol != null &&
                WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol) &&
                !site.Exploration.PatrolUnits.Any(state => state.PatrolId == patrol.Id && state.IsRemoved))
            .ToArray();
        foreach (SiteExplorationPatrolDefinition patrol in patrols)
        {
            SiteExplorationPatrolState patrolState = site.Exploration.PatrolUnits.FirstOrDefault(item => item.PatrolId == patrol.Id);
            WorldSiteUnitPlacement patrolPlacement = site.UnitPlacements.FirstOrDefault(item =>
                item.PlacementId == patrol.SourcePlacementId &&
                item.UnitTypeId == patrol.UnitTypeId);
            if (patrolState == null || patrolPlacement == null)
            {
                continue;
            }

            patrolPlacement.CellX = patrolState.CellX;
            patrolPlacement.CellY = patrolState.CellY;
            patrolPlacement.CellHeight = patrolState.CellHeight;
        }

        return patrols;
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
        WorldSiteIntelService.ApplySiteIntelToRequest(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            site.SiteId);
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

        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
        // Exploration confirmation only changes the site runtime mode; auto battle activation starts after the request is accepted.
        _siteModeTransitions.EnterBattleFromExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "exploration_battle_requested",
            request.RequestId);
        WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
            StrategicWorldRuntime.State,
            request,
            rollback,
            ApplyBattleStartRequest,
            ActivateBattleRuntime,
            () => _battleStartBlockedReason,
            ClearBattleEntities,
            null,
            enabled => SetBattleRuntimeEnabled(enabled));
        if (!launch.Success)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason={launch.FailureReason}");
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

        GameLog.Warn(
            nameof(WorldSiteRoot),
            $"SiteExplorationEntryUnresolved site={site.SiteId} reason=known_player_entrance_entry_missing");
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
            IsKnownPlayerEntrancePlacement(site, definition, partyPlacement) &&
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

        if (!TryResolveKnownPlayerEntranceDeploymentCandidate(
                site,
                definition,
                army.TargetApproachDirection,
                canEnterWater,
                out WorldSiteDeploymentCell candidate,
                out WorldSiteAttackDirection direction,
                out string entranceId))
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"SiteExplorationEntryMissingKnownEntrance site={site.SiteId} army={army.ArmyId} targetDirection={army.TargetApproachDirection}");
            return false;
        }

        // Infiltration starts from a player entrance known through current intel; hidden authored entrances stay unusable until revealed.
        entry = new GridSurfacePosition(candidate.Cell.X, candidate.Cell.Y, candidate.Height);
        partyPlacement.EntranceId = entranceId;
        partyPlacement.AttackDirection = direction;
        partyPlacement.CellX = entry.X;
        partyPlacement.CellY = entry.Y;
        partyPlacement.CellHeight = entry.Height;
        return true;
    }

    private bool TryResolveKnownPlayerEntranceDeploymentCandidate(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteAttackDirection preferredDirection,
        bool canEnterWater,
        out WorldSiteDeploymentCell candidate,
        out WorldSiteAttackDirection resolvedDirection,
        out string entranceId)
    {
        candidate = default;
        resolvedDirection = WorldSiteAttackDirection.Any;
        entranceId = "";
        foreach (BattleEntranceDefinition entrance in EnumerateKnownPlayerEntrances(site, definition, preferredDirection))
        {
            WorldSiteAttackDirection direction = entrance.Direction;
            if (TryResolveFirstDeploymentCandidate(direction, canEnterWater, out candidate))
            {
                resolvedDirection = direction;
                entranceId = entrance.EntranceId ?? "";
                return true;
            }
        }

        return false;
    }

    private IEnumerable<BattleEntranceDefinition> EnumerateKnownPlayerEntrances(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteAttackDirection preferredDirection)
    {
        if (site == null || definition?.EntranceDefinitions == null)
        {
            return System.Array.Empty<BattleEntranceDefinition>();
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);
        var knownEntranceIds = new HashSet<string>(intel.KnownEntranceIds, System.StringComparer.Ordinal);
        string playerFactionId = StrategicWorldRuntime.State?.PlayerFactionId ?? StrategicWorldIds.FactionPlayer;
        List<BattleEntranceDefinition> entrances = definition.EntranceDefinitions
            .Where(entrance => IsKnownPlayerEntrance(entrance, knownEntranceIds, playerFactionId))
            .ToList();
        if (preferredDirection == WorldSiteAttackDirection.Any)
        {
            return entrances;
        }

        return entrances
            .Where(entrance => entrance.Direction == preferredDirection)
            .Concat(entrances.Where(entrance => entrance.Direction != preferredDirection))
            .ToArray();
    }

    private static bool IsKnownPlayerEntrancePlacement(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement)
    {
        if (placement == null)
        {
            return false;
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site?.SiteId ?? definition?.Id ?? "",
            WorldIntelVisibility.Visible);
        var knownEntranceIds = new HashSet<string>(intel.KnownEntranceIds, System.StringComparer.Ordinal);
        string playerFactionId = StrategicWorldRuntime.State?.PlayerFactionId ?? StrategicWorldIds.FactionPlayer;
        return definition?.EntranceDefinitions?.Any(entrance =>
            IsKnownPlayerEntrance(entrance, knownEntranceIds, playerFactionId) &&
            ((!string.IsNullOrWhiteSpace(placement.EntranceId) && entrance.EntranceId == placement.EntranceId) ||
             (string.IsNullOrWhiteSpace(placement.EntranceId) && entrance.Direction == placement.AttackDirection))) == true;
    }

    private static bool IsKnownPlayerEntrance(
        BattleEntranceDefinition entrance,
        IReadOnlySet<string> knownEntranceIds,
        string playerFactionId)
    {
        if (entrance == null ||
            string.IsNullOrWhiteSpace(entrance.EntranceId) ||
            knownEntranceIds?.Contains(entrance.EntranceId) != true ||
            string.Equals(entrance.Source, "Garrison", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entrance.FactionId) ||
               entrance.FactionId == playerFactionId ||
               entrance.FactionId == StrategicWorldIds.FactionPlayer;
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

        if (_deploymentTargetEvaluator.TryMoveToGridCell(
                _activeGridMap,
                site,
                definition,
                placementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                ResolvePlacementCanEnterWater,
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
        return _deploymentTargetEvaluator.CanMoveToGridCell(
            _activeGridMap,
            site,
            definition,
            placementId,
            new Vector2I(gridPosition.X, gridPosition.Y),
            ResolvePlacementCanEnterWater,
            out failureReason);
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
        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(ResolveSiteState(_siteHudSiteId));
        WorldActionResult result = _worldActionResolver.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            returnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);

        StrategicWorldRuntime.LastNotice = result.Message;
        _battleLauncher.ApplyModeTransitionRollbackEvent(rollback, result.Events);
        if (!result.Success)
        {
            RefreshSiteManagementUi(result.Message);
            return;
        }

        if (result.BattleStartRequest != null)
        {
            WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
                StrategicWorldRuntime.State,
                result.BattleStartRequest,
                rollback,
                ApplyBattleStartRequest,
                ActivateBattleRuntime,
                () => _battleStartBlockedReason,
                ClearBattleEntities,
                null,
                enabled => SetBattleRuntimeEnabled(enabled));
            if (!launch.Success)
            {
                StrategicWorldRuntime.LastNotice = "无法进入自动战斗。";
                RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
                GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter site battle request={result.BattleStartRequest.RequestId} target={result.BattleStartRequest.TargetSiteId} reason={launch.FailureReason}");
            }

            return;
        }

        RefreshSiteManagementUi(result.Message);
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
