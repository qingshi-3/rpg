using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
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
    private const string SiteUnitPlacementEntityScenePath = "res://scenes/world/sites/WorldSiteUnitPlacementEntity.tscn";

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
    private readonly Dictionary<string, WorldSiteUnitPlacementEntity> _sitePlacementEntities = new();
    private BattleInputRouter _inputRouter;
    private BattleCommandController _commandController;
    private BattleTurnController _turnController;
    private BattleIntentController _intentController;
    private int _battleStateVersion;
    private bool _battleEndHandled;
    private bool _battleRuntimeEnabled = true;
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
        _hudRoot = GetNodeOrNull<BattleHudRoot>(HudRootPath);
        _inputRouter = GetNodeOrNull<BattleInputRouter>(InputRouterPath);
        _commandController = GetNodeOrNull<BattleCommandController>(CommandControllerPath);
        _turnController = GetNodeOrNull<BattleTurnController>(TurnControllerPath);
        _intentController = GetNodeOrNull<BattleIntentController>(IntentControllerPath);
        BuildSiteHud();

        bool hasActiveBattleLaunch = BattleSessionHandoff.HasActiveLaunch;
        if (_unitRoot != null)
        {
            _unitRoot.Initialize(TryGetCellGlobalPosition);
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
                OnBattleEnded);
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
                entity => _hudRoot?.ShowEntity(entity),
                text => _hudRoot?.ShowActionHint(text),
                () => _hudRoot?.ClearActiveCommand());

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

        if (hasActiveBattleLaunch)
        {
            SetBattleRuntimeEnabled(true);
            _turnController?.StartBattle();
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

        EmitSignal(SignalName.SiteMapLoaded, _activeSiteMap);

        PlaceBattleEntitiesOnGrid();
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

    private void ApplyBattleStartRequest()
    {
        if (_unitRoot == null || !BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            return;
        }

        if (request.BattleKind is BattleKind.AssaultSite or BattleKind.DefenseRaid or BattleKind.FieldIntercept)
        {
            ClearBattleEntities();
            AddRequestedForces(request.PlayerForces, BattleFaction.Player);
            AddRequestedForces(request.EnemyForces, BattleFaction.Enemy);
        }

        if (request.BattleKind == BattleKind.DefenseRaid)
        {
            ApplyBattleModifiers(request);
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle request consumed kind={request.BattleKind} target={request.TargetSiteId} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} modifiers={request.BattleModifiers.Count}");
    }

    private void AddRequestedForces(IEnumerable<BattleForceRequest> forces, BattleFaction fallbackFaction)
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
                GridPosition fallbackPosition = ResolveDefaultForcePosition(fallbackFaction, index);
                BattleEntity entity = _battleUnitFactory.Create(force, i, fallbackFaction, fallbackPosition);
                if (entity == null)
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"Skip battle unit force={force.ForceId} unit={force.UnitDefinitionId} index={i}");
                    continue;
                }

                _unitRoot.AddChild(entity);
                index++;
            }
        }
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

    private static GridPosition ResolveDefaultForcePosition(BattleFaction faction, int index)
    {
        return faction == BattleFaction.Player
            ? new GridPosition(17 + index, 16)
            : new GridPosition(20 + index, 20);
    }

    private void ApplyBattleModifiers(BattleStartRequest request)
    {
        int towerSupportCount = request.BattleModifiers.Count(modifier => modifier.Type == "tower_support" && modifier.Uses > 0);
        if (towerSupportCount <= 0)
        {
            return;
        }

        int damage = towerSupportCount * 2;
        BattleEntity target = _unitRoot.GetEntitiesSnapshot()
            .FirstOrDefault(entity =>
                entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
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

        _hudRoot?.ShowActionHint($"防御塔开场支援，对 {target.DisplayName} 造成 {applied} 伤害");
        GameLog.Info(nameof(WorldSiteRoot), $"Tower support applied target={target.EntityId} damage={applied} supports={towerSupportCount}");
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

        if (BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest consumedRequest, out BattleResult consumedResult))
        {
            request = consumedRequest;
            battleResult = consumedResult;
            applyResult = ApplyBattleResultToWorld(request, battleResult);
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

    private void SetBattleRuntimeEnabled(bool enabled)
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

        if (_unitRoot != null)
        {
            _unitRoot.Visible = enabled;
        }

        if (_sitePlacementEntityRoot != null)
        {
            _sitePlacementEntityRoot.Visible = !enabled;
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

        if (site == null || definition == null)
        {
            return;
        }

        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
            WorldSiteUnitPlacementEntity entity = CreateSitePlacementEntity();
            if (entity == null)
            {
                continue;
            }

            entity.BindPlacement(placement.PlacementId, BuildPlacementDisplayName(placement));
            entity.CanDrag = CanOpenSiteDetail(site);
            string placementId = placement.PlacementId;
            entity.Pressed += OnPlacementEntityPressed;
            _sitePlacementEntityRoot.AddChild(entity);
            _sitePlacementEntities[placementId] = entity;
        }

        UpdateSiteMapEntities();
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
            if (_sitePlacementEntities.TryGetValue(placement.PlacementId, out WorldSiteUnitPlacementEntity entity) &&
                placement.PlacementId != _draggedPlacementId)
            {
                entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
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
        if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out WorldSiteUnitPlacementEntity entity))
        {
            _draggedPlacementOriginGlobalPosition = entity.GlobalPosition;
            entity.GlobalPosition = GetGlobalMousePosition();
            UpdateSiteDeploymentDragPreview(entity);
        }

        _siteSelectionLabel.Text = $"正在调整：{BuildPlacementDisplayName(_selectedPlacementId)}";
        GetViewport().SetInputAsHandled();
    }

    private void HandleSiteDeploymentDragInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled || string.IsNullOrWhiteSpace(_draggedPlacementId))
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out WorldSiteUnitPlacementEntity entity))
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
        _sitePlacementEntities.TryGetValue(placementId, out WorldSiteUnitPlacementEntity draggedEntity);
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

    private void UpdateSiteDeploymentDragPreview(WorldSiteUnitPlacementEntity entity)
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

        entity.SetPlacementPreviewState(true, canDrop);

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
        if (!CanPlaceSiteDeploymentOnGridCell(gridPosition, out failureReason))
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

    private bool CanPlaceSiteDeploymentOnGridCell(GridPosition position, out string failureReason)
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

        return true;
    }

    private void ClearSiteDeploymentDragPreview(WorldSiteUnitPlacementEntity entity)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        entity?.SetPlacementPreviewState(false, true);
    }

    private void ReturnDraggedPlacementToOrigin(WorldSiteUnitPlacementEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        entity.GlobalPosition = _draggedPlacementOriginGlobalPosition;
        entity.SetPlacementPreviewState(false, true);
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

    private WorldSiteUnitPlacementEntity CreateSitePlacementEntity()
    {
        PackedScene scene = GD.Load<PackedScene>(SiteUnitPlacementEntityScenePath);
        if (scene == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot load site placement entity scene path={SiteUnitPlacementEntityScenePath}");
            return null;
        }

        WorldSiteUnitPlacementEntity entity = scene.Instantiate<WorldSiteUnitPlacementEntity>();
        if (entity == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Site placement entity scene type mismatch path={SiteUnitPlacementEntityScenePath}");
        }

        return entity;
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
