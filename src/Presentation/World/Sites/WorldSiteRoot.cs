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
    private BattleHudRoot _hudRoot;
    private Control _siteHudRoot;
    private Node2D _sitePlacementEntityRoot;
    private Label _siteHudTitle;
    private Label _siteHudBody;
    private Label _siteResourceLabel;
    private Label _siteNoticeLabel;
    private Label _siteSelectionLabel;
    private Button _returnMapButton;
    private VBoxContainer _siteFacilityList;
    private VBoxContainer _siteGarrisonList;
    private VBoxContainer _siteThreatList;
    private VBoxContainer _siteActionList;
    private readonly Dictionary<string, Node2D> _sitePlacementEntities = new();
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
    private string _draggedPlacementId = "";
    private Vector2 _draggedPlacementOriginGlobalPosition;
    private readonly BattleActionExecutor _actionExecutor = new();
    private readonly BattleUnitFactory _battleUnitFactory = new();
    private readonly WorldBattleResultApplier _worldBattleResultApplier = new();
    private readonly WorldActionResolver _worldActionResolver = new();
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public Node ActiveSiteMap => _activeSiteMap;
    public BattleGridMap ActiveGridMap => _activeGridMap;
    public BattleEntity SelectedEntity => _commandController?.SelectedEntity;
    public bool AllowsDebugHoverInfo => _commandController?.AllowsDebugHoverInfo == true;
    public bool IsEnemyPhaseRunning => _turnController?.IsEnemyPhaseRunning == true;

    public override void _Ready()
    {
        GameLog.StartSession(nameof(WorldSiteRoot));

        _mapRoot = GetNode<Node>(MapRootPath);
        _unitRoot = GetNodeOrNull<BattleUnitRoot>(UnitRootPath);
        _highlightOverlay = GetNodeOrNull<BattleGridHighlightOverlay>(HighlightOverlayPath);
        _previewController = GetNodeOrNull<BattlePreviewController>(PreviewControllerPath);
        _selectionVignetteOverlay = GetNodeOrNull<BattleSelectionVignetteOverlay>(SelectionVignetteOverlayPath);
        _hudRoot = GetNodeOrNull<BattleHudRoot>(HudRootPath);
        _inputRouter = GetNodeOrNull<BattleInputRouter>(InputRouterPath);
        _commandController = GetNodeOrNull<BattleCommandController>(CommandControllerPath);
        _turnController = GetNodeOrNull<BattleTurnController>(TurnControllerPath);
        _intentController = GetNodeOrNull<BattleIntentController>(IntentControllerPath);
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
                _intentController == null ? null : _intentController.GenerateEnemyIntents,
                _intentController == null ? null : _intentController.ExecuteEnemyAction,
                _intentController == null ? null : _intentController.ClearEnemyIntentBookkeeping,
                MarkBattleStateChanged,
                text => _hudRoot?.ShowActionHint(text),
                () => _hudRoot?.ClearActiveCommand(),
                OnBattleEnded,
                (round, activeFaction, activeEntity, queue) => _hudRoot?.ShowTurnQueue(round, activeFaction, activeEntity, queue));
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
        PlaceBattleEntitiesOnGrid();
        RegisterBattleEntities();

        if (hasActiveBattleLaunch && string.IsNullOrWhiteSpace(_battleStartBlockedReason))
        {
            SetBattleRuntimeEnabled(true);
            _turnController?.StartBattle();
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
            UpdateSiteMapEntities();
            return;
        }

        _previewController?.UpdateMovementPathPreview(_commandController?.IsMoveTargeting == true);
        _previewController?.UpdateHoverIntentPreview(_commandController?.CanShowHoverIntentPreview == true, _battleStateVersion);
    }

    public override void _Input(InputEvent @event)
    {
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
        BattleForceRequest[] nonResidentForces = EnumerateRequestForces(request)
            .Where(force => !IsResidentGarrisonForceForSite(force, site.SiteId))
            .ToArray();
        _deploymentService.ClearBattlePlacementsForForces(site, nonResidentForces);

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
        if (force == null || force.Count <= 0 || IsResidentGarrisonForceForSite(force, site?.SiteId))
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
        int index = 0;
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
                index++;
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

    private BattleActionExecutionContext CreateActionExecutionContext()
    {
        System.Action<BattleEntity, IReadOnlyList<GridSurfacePosition>> moveEntityTo =
            _unitRoot == null ? (_, _) => { } : _unitRoot.MoveEntityTo;

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
            PersistLivePlacementSnapshots(request, livePlacementSnapshots);
            applyResult = ApplyBattleResultToWorld(request, battleResult);
            ReconcileWorldSitePlacementsAfterBattle(request, livePlacementSnapshots);
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
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots)
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
        int matched = ApplyLiveSnapshotsToMatchingPlacements(site, snapshots, usedSnapshotPlacementIds);
        int converted = ApplyLiveSnapshotsToOwnerGarrisons(site, snapshots, usedSnapshotPlacementIds);
        int beforeCleanup = site.UnitPlacements.Count;
        _deploymentService.ClearBattlePlacementsForForces(site, EnumerateBattleForces(request));
        EnsureSitePlacementsRespectTerrain(site, definition);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"WorldSitePlacementsReconciledAfterBattle site={site.SiteId} request={request.RequestId} snapshots={snapshots?.Count ?? 0} matched={matched} converted={converted} removedBattlePlacements={beforeCleanup - site.UnitPlacements.Count} remaining={site.UnitPlacements.Count}");
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
        canvasLayer.AddChild(_siteHudRoot);
        EnsureSitePlacementEntityRoot();

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
            "SitePeacetimePanel/Margin/Scroll/Content/SiteHudBody",
            nameof(WorldSiteRoot));
        _siteSelectionLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteSelectionLabel",
            nameof(WorldSiteRoot));
        _siteFacilityList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteFacilityList",
            nameof(WorldSiteRoot));
        _siteGarrisonList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteGarrisonList",
            nameof(WorldSiteRoot));
        _siteThreatList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteThreatList",
            nameof(WorldSiteRoot));
        _siteActionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteActionList",
            nameof(WorldSiteRoot));
        _siteNoticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            _siteHudRoot,
            "SitePeacetimePanel/Margin/Scroll/Content/SiteNoticeLabel",
            nameof(WorldSiteRoot));

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
            Visible = false
        };
        AddChild(_sitePlacementEntityRoot);
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
        if (request == null &&
            StrategicWorldRuntime.TryConsumePendingSiteVisit(out string pendingSiteId, out string pendingReturnScenePath))
        {
            siteId = pendingSiteId;
            if (string.IsNullOrWhiteSpace(returnScenePath))
            {
                returnScenePath = pendingReturnScenePath;
            }
        }

        _siteHudSiteId = siteId;
        _siteHudReturnScenePath = string.IsNullOrWhiteSpace(returnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : returnScenePath;

        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = string.IsNullOrWhiteSpace(_siteHudReturnScenePath);
            _returnMapButton.TooltipText = _returnMapButton.Disabled ? "没有可返回的大地图场景。" : "";
        }

        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
        }

        RefreshSiteManagementUi(applyResult?.Message, outcome);
        GameLog.Info(nameof(WorldSiteRoot), $"Switched to site management UI site={siteId}");
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
            _sitePlacementEntityRoot.Visible = !enabled && !keepBattlePresentation;
        }

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
        ClearResolvedBattlePlacements(site);
        EnsureSitePlacementsRespectTerrain(site, definition);

        _siteHudTitle.Text = outcome == BattleOutcome.None
            ? $"{ResolveSiteName(_siteHudSiteId)} · 场域经营"
            : $"{ResolveSiteName(_siteHudSiteId)} · {GetBattleOutcomeLabel(outcome)}";
        _siteResourceLabel.Text = BuildResourceLine();
        _siteHudBody.Text = BuildSiteOverview(_siteHudSiteId);
        _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice) ? StrategicWorldRuntime.LastNotice : notice.Trim();

        RefreshFacilityList(site, definition);
        RefreshGarrisonList(site);
        RefreshThreatList(site);
        RefreshActionList(site);
        RefreshSiteMapEntities(site, definition);
    }

    private void ClearResolvedBattlePlacements(WorldSiteState site)
    {
        if (site == null || site.SiteMode == WorldSiteMode.Wartime)
        {
            return;
        }

        int removed = site.UnitPlacements.RemoveAll(placement =>
            !WorldSiteDeploymentService.IsGarrisonPlacement(placement));
        if (removed > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"Resolved battle placements cleared for site management site={site.SiteId} mode={site.SiteMode} removed={removed}");
        }
    }

    private string BuildResourceLine()
    {
        ResourceStore resources = StrategicWorldRuntime.State.PlayerResources;
        return
            $"人口 {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}    " +
            $"经济 {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}    " +
            $"石材 {resources.GetAmount(StrategicWorldIds.ResourceStone)}    " +
            $"世界步 {StrategicWorldRuntime.State.WorldTick}";
    }

    private string BuildSiteOverview(string siteId)
    {
        WorldSiteState site = ResolveSiteState(siteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(siteId);
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
            $"归属：{GetFactionLabel(site.OwnerFactionId)}    受损：{site.DamageLevel}\n" +
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
        foreach (FacilitySlotDefinition slot in definition.FacilitySlots)
        {
            FacilityInstance facility = site.Facilities.FirstOrDefault(item => item.SlotId == slot.SlotId && item.State != FacilityState.Destroyed);
            string facilityText = facility == null
                ? $"空置，可建：{BuildAllowedFacilityNames(slot, queries)}"
                : $"{queries.GetFacility(facility.FacilityId)?.DisplayName ?? facility.FacilityId} · {GetFacilityStateLabel(facility.State)}";
            AddMutedLine(_siteFacilityList, $"{slot.DisplayName}\n{facilityText}");
        }

        RefreshSelectedSlotLabel(site);
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
        string selectedThreatId = ResolveSelectedThreatId(site);
        IReadOnlyList<WorldActionViewModel> actions = _worldActionResolver.GetAvailableActions(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            _siteHudSiteId,
            selectedThreatId);

        foreach (WorldActionViewModel action in actions)
        {
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
        ClearBattleEntities();

        if (site == null || definition == null)
        {
            return;
        }

        if (_unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot rebuild site management units because UnitRoot is missing site={site.SiteId}");
            return;
        }

        int animatedCount = 0;
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
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

        if (_unitRoot != null)
        {
            _unitRoot.Visible = true;
            _unitRoot.PlayIdleForActiveEntities();
        }

        UpdateSiteMapEntities();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteManagementUnitsRebuilt site={site.SiteId} placements={site.UnitPlacements.Count} animated={animatedCount} canDrag={CanOpenSiteDetail(site)}");
    }

    private void UpdateSiteMapEntities()
    {
        if (_siteHudRoot?.Visible != true || string.IsNullOrWhiteSpace(_siteHudSiteId))
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
                placement.PlacementId != _draggedPlacementId)
            {
                entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
                if (entity is BattleEntity battleEntity)
                {
                    SyncSitePlacementGridOccupant(battleEntity, placement);
                }
            }
        }
    }

    private void SelectPlacement(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        RefreshSiteManagementUi();
        GameLog.Info(nameof(WorldSiteRoot), $"Site placement selected site={_siteHudSiteId} placement={_selectedPlacementId}");
    }

    private void OnPlacementEntityPressed(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
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

    private void ExecuteSiteAction(WorldActionViewModel action)
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
            TargetSlotId = "",
            ThreatId = action.ThreatId
        };

        string returnScenePath = string.IsNullOrWhiteSpace(_siteHudReturnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : _siteHudReturnScenePath;
        WorldActionResult result = _worldActionResolver.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            returnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);

        StrategicWorldRuntime.LastNotice = result.Message;
        if (!result.Success)
        {
            RefreshSiteManagementUi(result.Message);
            return;
        }

        if (result.BattleStartRequest != null)
        {
            BattleSessionHandoff.BeginBattle(result.BattleStartRequest);
            Error error = GetTree().ChangeSceneToFile(result.BattleStartRequest.SiteScenePath);
            if (error != Error.Ok)
            {
                BattleSessionHandoff.CancelBattle();
                StrategicWorldRuntime.LastNotice = "无法进入战棋战斗。";
                RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
                GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter site battle path={result.BattleStartRequest.SiteScenePath} error={error}");
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
        return !float.IsNaN(screenPosition.X) && _siteHudRoot.GetGlobalRect().HasPoint(screenPosition);
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

    private string BuildPlacementDisplayName(string placementId)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        return placement == null ? placementId : BuildPlacementDisplayName(placement);
    }

    private string BuildPlacementDisplayName(WorldSiteUnitPlacement placement)
    {
        return $"{GetUnitLabel(placement.UnitTypeId)} #{placement.UnitIndex}";
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

    private static string GetFactionLabel(string factionId)
    {
        return factionId switch
        {
            StrategicWorldIds.FactionPlayer => "玩家",
            StrategicWorldIds.FactionUndead => "亡灵",
            "" => "无",
            _ => factionId
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

    private static string GetUnitLabel(string unitTypeId)
    {
        return unitTypeId switch
        {
            StrategicWorldIds.UnitMilitia => "民兵",
            StrategicWorldIds.UnitPlayerKnight => "骑士",
            StrategicWorldIds.UnitSkeletonWarrior => "骸骨斥候",
            StrategicWorldIds.UnitSkeletonArcher => "腐骨射手",
            _ => unitTypeId
        };
    }
}
