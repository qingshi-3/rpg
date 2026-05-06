using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.InputSystem;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Preview;

public partial class BattlePreviewController : Node
{
    private BattleGridHighlightOverlay _highlightOverlay;
    private System.Func<BattleGridMap> _getGridMap;
    private TryResolveBattleGridPosition _tryResolvePointerGridPosition;
    private System.Func<IReadOnlyList<BattleEntity>> _getEntitiesSnapshot;
    private System.Func<GridPosition, BattleEntity> _findEntityAt;
    private System.Func<BattleEntity, ISet<GridSurfacePosition>> _buildBlockedMovementSurfaces;
    private System.Action<BattleEntity> _markEntityDefeated;
    private System.Func<BattleAiContext> _createAiContext;
    private System.Func<BattleEntity, BattleIntent> _getEnemyIntent;
    private System.Action<string> _showActionHint;
    private System.Func<bool> _hasActiveMovementTweens;

    private MovementRangeResult _activeMovementRangeResult;
    private GridPosition? _activeMovementPathTarget;
    private BattleEntity _hoverIntentEntity;
    private int _hoverIntentPreviewVersion = -1;
    private readonly BattleIntentResolver _intentResolver = new();

    public bool HasActiveMovementRange => _activeMovementRangeResult != null;

    public void Initialize(
        BattleGridHighlightOverlay highlightOverlay,
        System.Func<BattleGridMap> getGridMap,
        TryResolveBattleGridPosition tryResolvePointerGridPosition,
        System.Func<IReadOnlyList<BattleEntity>> getEntitiesSnapshot,
        System.Func<GridPosition, BattleEntity> findEntityAt,
        System.Func<BattleEntity, ISet<GridSurfacePosition>> buildBlockedMovementSurfaces,
        System.Action<BattleEntity> markEntityDefeated,
        System.Func<BattleAiContext> createAiContext,
        System.Func<BattleEntity, BattleIntent> getEnemyIntent,
        System.Action<string> showActionHint,
        System.Func<bool> hasActiveMovementTweens)
    {
        _highlightOverlay = highlightOverlay;
        _getGridMap = getGridMap;
        _tryResolvePointerGridPosition = tryResolvePointerGridPosition;
        _getEntitiesSnapshot = getEntitiesSnapshot;
        _findEntityAt = findEntityAt;
        _buildBlockedMovementSurfaces = buildBlockedMovementSurfaces;
        _markEntityDefeated = markEntityDefeated;
        _createAiContext = createAiContext;
        _getEnemyIntent = getEnemyIntent;
        _showActionHint = showActionHint;
        _hasActiveMovementTweens = hasActiveMovementTweens;
    }

    public void ClearActionPreviewHighlights()
    {
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;
        _hoverIntentEntity = null;
        _hoverIntentPreviewVersion = -1;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Threat);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Attack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
    }

    public void ClearSelectedHighlight()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Selected);
    }

    public void UpdateSelectedHighlight(BattleEntity entity)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            ClearSelectedHighlight();
            GameLog.Info(nameof(BattlePreviewController), $"Selected highlight cleared because entity has no grid occupant id={entity?.EntityId} name={entity?.DisplayName}");
            return;
        }

        _highlightOverlay?.SetCells(BattleGridHighlightKind.Selected, new[] { gridOccupant.Position });
        GameLog.Info(nameof(BattlePreviewController), $"Selected highlight set id={entity.EntityId} cell={gridOccupant.Position}");
    }

    public void ShowMovementRange(BattleEntity entity)
    {
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;
        ClearHoverIntentPreview();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Attack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);

        BattleGridMap gridMap = _getGridMap?.Invoke();
        if (gridMap == null)
        {
            GameLog.Warn(nameof(BattlePreviewController), "Cannot show movement range because active grid map is missing.");
            return;
        }

        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        MovementComponent movement = entity?.GetComponent<MovementComponent>();
        if (entity == null || gridOccupant == null || movement == null || movement.MoveRange <= 0)
        {
            GameLog.Info(
                nameof(BattlePreviewController),
                $"Movement range skipped id={entity?.EntityId} hasGridOccupant={gridOccupant != null} hasMovement={movement != null} moveRange={movement?.MoveRange ?? 0}");
            return;
        }

        if (!movement.CanUseMove())
        {
            GameLog.Info(
                nameof(BattlePreviewController),
                $"Movement range skipped by move uses id={entity.EntityId} remaining={movement.MoveUsesRemaining}");
            return;
        }

        MovementRangeResult result = MovementRangeFinder.FindReachableCells(
            gridMap,
            gridOccupant.SurfacePosition,
            movement.MoveRange,
            _buildBlockedMovementSurfaces?.Invoke(entity) ?? new HashSet<GridSurfacePosition>(),
            surface => BattleRuleQueries.CanEnterSurface(entity, surface));

        if (!result.StartCellExists || !result.StartWalkable && result.DestinationCells.Count == 0)
        {
            ShowInvalidCell(gridOccupant.Position);
            GameLog.Warn(
                nameof(BattlePreviewController),
                $"Movement range invalid start id={entity.EntityId} cell={gridOccupant.Position} exists={result.StartCellExists} walkable={result.StartWalkable}");
            return;
        }

        _activeMovementRangeResult = result;
        if (!result.StartWalkable)
        {
            ShowInvalidCell(gridOccupant.Position);
            GameLog.Warn(
                nameof(BattlePreviewController),
                $"Movement range recovered from invalid start id={entity.EntityId} surface={gridOccupant.SurfacePosition} destinations={result.DestinationCells.Count}");
        }

        _highlightOverlay?.SetCells(BattleGridHighlightKind.Move, result.DestinationCells);
        GameLog.Info(
            nameof(BattlePreviewController),
            $"Movement range shown id={entity.EntityId} start={gridOccupant.SurfacePosition} moveRange={movement.MoveRange} visited={result.ReachableSurfaceCosts.Count} destinations={result.DestinationSurfaces.Count}");
    }

    public void UpdateMovementPathPreview(bool isMoveTargeting)
    {
        if (!isMoveTargeting ||
            _activeMovementRangeResult == null ||
            _highlightOverlay == null)
        {
            ClearMovementPathPreview();
            return;
        }

        if (_tryResolvePointerGridPosition == null || !_tryResolvePointerGridPosition(out GridPosition hoverPosition))
        {
            ClearMovementPathPreview();
            return;
        }

        if (_activeMovementPathTarget == hoverPosition)
        {
            return;
        }

        _activeMovementPathTarget = hoverPosition;
        if (!_activeMovementRangeResult.TryBuildPathTo(hoverPosition, out IReadOnlyList<GridSurfacePosition> path))
        {
            _highlightOverlay.ClearCells(BattleGridHighlightKind.Path);
            return;
        }

        GridPosition[] pathCells = path
            .Select(surface => surface.Position)
            .ToArray();

        if (pathCells.Length < 2)
        {
            _highlightOverlay.ClearCells(BattleGridHighlightKind.Path);
            return;
        }

        _highlightOverlay.SetPath(pathCells);
    }

    public void ClearMovementPathPreview()
    {
        if (_activeMovementPathTarget == null)
        {
            return;
        }

        _activeMovementPathTarget = null;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
    }

    public void UpdateHoverIntentPreview(bool canShowHoverIntentPreview, int battleStateVersion)
    {
        if (!canShowHoverIntentPreview || _highlightOverlay == null || _getGridMap?.Invoke() == null || _hasActiveMovementTweens?.Invoke() == true)
        {
            ClearHoverIntentPreview();
            return;
        }

        BattleEntity entity = _tryResolvePointerGridPosition != null && _tryResolvePointerGridPosition(out GridPosition hoverPosition)
            ? _findEntityAt?.Invoke(hoverPosition)
            : null;
        if (!IsEnemyIntentPreviewTarget(entity))
        {
            ClearHoverIntentPreview();
            return;
        }

        bool isNewHoverTarget = _hoverIntentEntity != entity;
        bool isStateChanged = _hoverIntentPreviewVersion != battleStateVersion;
        if (!isNewHoverTarget && !isStateChanged)
        {
            return;
        }

        _hoverIntentEntity = entity;
        _hoverIntentPreviewVersion = battleStateVersion;
        ShowIntentPreview(entity, isNewHoverTarget || isStateChanged);
    }

    public void InvalidateHoverIntentPreview()
    {
        _hoverIntentPreviewVersion = -1;
    }

    public void ClearHoverIntentPreview()
    {
        if (_hoverIntentEntity == null)
        {
            return;
        }

        _hoverIntentEntity = null;
        _hoverIntentPreviewVersion = -1;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
    }

    public void ApplyIntentHighlights(BattleIntentPreview preview)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);

        if (preview == null)
        {
            return;
        }

        if (preview.PathCells.Count > 1)
        {
            _highlightOverlay?.SetPath(preview.PathCells);
        }

        GridPosition[] targetCells = preview.AffectedCells
            .Concat(GetIntentTargetCell(preview) is { } targetCell ? new[] { targetCell } : System.Array.Empty<GridPosition>())
            .Distinct()
            .ToArray();
        ShowTargetCells(targetCells);
    }

    public void ShowAbilityTargetHighlight(BattleEntity attacker, AbilityDefinition ability)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        ClearHoverIntentPreview();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Attack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;

        BattleGridMap gridMap = _getGridMap?.Invoke();
        GridOccupantComponent gridOccupant = attacker?.GetComponent<GridOccupantComponent>();
        if (attacker == null || gridMap == null || ability == null || gridOccupant == null)
        {
            GameLog.Warn(
                nameof(BattlePreviewController),
                $"Cannot show ability range id={attacker?.EntityId} hasGrid={gridMap != null} hasAbility={ability != null} hasGridOccupant={gridOccupant != null}");
            return;
        }

        HashSet<GridPosition> rangeCells = BuildCellsInManhattanRange(gridOccupant.Position, ability.Range)
            .Where(position => gridMap.TryGetCell(position, out _))
            .ToHashSet();

        IReadOnlyList<BattleEntity> entities = _getEntitiesSnapshot?.Invoke() ?? System.Array.Empty<BattleEntity>();
        GridPosition[] targetCells = entities
            .Where(target => BattleAbilityQueries.IsValidTarget(
                gridMap,
                entities,
                attacker,
                target,
                target.GetComponent<GridOccupantComponent>()?.Position ?? default,
                ability,
                _markEntityDefeated,
                out _))
            .Select(target => target.GetComponent<GridOccupantComponent>().Position)
            .Distinct()
            .ToArray();

        _highlightOverlay?.SetCells(BattleGridHighlightKind.Attack, rangeCells);
        ShowTargetCells(targetCells);
        _showActionHint?.Invoke(targetCells.Length > 0 ? $"请选择{ability.DisplayName}目标" : "范围内没有有效目标");

        GameLog.Info(
            nameof(BattlePreviewController),
            $"Ability range shown attacker={attacker.EntityId} ability={ability.Id} start={gridOccupant.Position} range={ability.Range} cells={rangeCells.Count} targets={targetCells.Length}");
    }

    public void ShowInvalidCell(GridPosition position)
    {
        _highlightOverlay?.SetCells(BattleGridHighlightKind.Invalid, new[] { position });
    }

    public void ShowTargetCells(IEnumerable<GridPosition> positions)
    {
        GridPosition[] targetCells = positions?.Distinct().ToArray() ?? System.Array.Empty<GridPosition>();
        if (targetCells.Length > 0)
        {
            _highlightOverlay?.SetCells(BattleGridHighlightKind.Target, targetCells);
        }
        else
        {
            _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        }
    }

    public static string DescribeIntentForCurrentState(BattleIntentPreview preview)
    {
        if (preview == null || preview.Request == null || preview.Actor == null)
        {
            return "敌方意图未知";
        }

        if (!preview.HasAction)
        {
            return string.IsNullOrWhiteSpace(preview.Request.Reason)
                ? $"{preview.Actor.DisplayName} 暂无行动"
                : preview.Request.Reason;
        }

        return string.IsNullOrWhiteSpace(preview.DetailText)
            ? preview.Intent?.Summary ?? "敌方意图未知"
            : preview.DetailText;
    }

    private void ShowIntentPreview(BattleEntity entity, bool shouldLog)
    {
        BattleIntent intent = _getEnemyIntent?.Invoke(entity);
        if (intent == null)
        {
            ClearHoverIntentPreview();
            return;
        }

        BattleIntentPreview preview = _intentResolver.Preview(_createAiContext?.Invoke(), intent);
        ApplyIntentHighlights(preview);
        _showActionHint?.Invoke(DescribeIntentForCurrentState(preview));
        if (shouldLog)
        {
            GameLog.Info(
                nameof(BattlePreviewController),
                $"Intent preview shown enemy={entity.EntityId} intent={intent.TemplateId} resolvedKind={preview.Kind} target={preview.Target?.EntityId} destination={preview.Request?.Destination} affected={preview.AffectedCells.Count} path={preview.PathCells.Count}");
        }
    }

    private static bool IsEnemyIntentPreviewTarget(BattleEntity entity)
    {
        return entity != null &&
               !BattleRuleQueries.IsDefeated(entity) &&
               entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
               entity.GetComponent<GridOccupantComponent>() != null;
    }

    private static GridPosition? GetIntentTargetCell(BattleIntentPreview preview)
    {
        if (preview == null || preview.Request == null)
        {
            return null;
        }

        if (preview.Kind == BattleActionKind.Move)
        {
            return preview.Request.Destination;
        }

        GridOccupantComponent targetGrid = preview.Target?.GetComponent<GridOccupantComponent>();
        return targetGrid?.Position;
    }

    private static IEnumerable<GridPosition> BuildCellsInManhattanRange(GridPosition origin, int range)
    {
        int safeRange = System.Math.Max(0, range);

        for (int x = origin.X - safeRange; x <= origin.X + safeRange; x++)
        {
            for (int y = origin.Y - safeRange; y <= origin.Y + safeRange; y++)
            {
                var position = new GridPosition(x, y);
                if (BattleRuleQueries.GetManhattanDistance(origin, position) <= safeRange)
                {
                    yield return position;
                }
            }
        }
    }
}
