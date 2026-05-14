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
using Rpg.Presentation.Battle.Threats;

namespace Rpg.Presentation.Battle.Preview;

public partial class BattlePreviewController : Node
{
    private BattleGridHighlightOverlay _highlightOverlay;
    private BattleSelectionVignetteOverlay _selectionVignetteOverlay;
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
    private BattleOverlayMode _activeOverlayMode = BattleOverlayMode.None;
    private readonly BattleIntentResolver _intentResolver = new();
    private BattleEntity _selectedVisualEntity;
    private BattleEntity _previewFocusEntity;
    private BattleEntity _activeTargetingActor;
    private AbilityDefinition _activeTargetingAbility;
    private GridPosition? _activeTargetHoverCell;
    private readonly HashSet<BattleEntity> _targetPreviewEntities = new();

    public bool HasActiveMovementRange => _activeMovementRangeResult != null;

    public void Initialize(
        BattleGridHighlightOverlay highlightOverlay,
        BattleSelectionVignetteOverlay selectionVignetteOverlay,
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
        _selectionVignetteOverlay = selectionVignetteOverlay;
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
        _activeOverlayMode = BattleOverlayMode.None;
        ClearExecutionOverlay();
        ClearThreatOverlay();
        ClearPreviewFocusPresentation();
    }

    public void ClearSelectedHighlight()
    {
        SetSelectedPresentation(_selectedVisualEntity, false);
        _selectedVisualEntity = null;
        _selectionVignetteOverlay?.ClearTarget();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Selected);
    }

    public void UpdateSelectedHighlight(BattleEntity entity)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Selected);
        if (_selectedVisualEntity != entity)
        {
            SetSelectedPresentation(_selectedVisualEntity, false);
        }

        _selectedVisualEntity = entity;
        SetSelectedPresentation(_selectedVisualEntity, true);
        _selectionVignetteOverlay?.SetTarget(_selectedVisualEntity);

        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            GameLog.Info(nameof(BattlePreviewController), $"Selected highlight cleared because entity has no grid occupant id={entity?.EntityId} name={entity?.DisplayName}");
            return;
        }

        GameLog.Info(nameof(BattlePreviewController), $"Selected unit outline set id={entity.EntityId} cell={gridOccupant.Position}");
    }

    public void ShowMovementRange(BattleEntity entity)
    {
        ActivateOverlayMode(BattleOverlayMode.Execution);
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;
        ClearHoverIntentPreview();
        ClearPreviewFocusPresentation();
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

        _highlightOverlay?.SetCells(
            BattleGridHighlightKind.Move,
            result.DestinationCells.Where(position => position != gridOccupant.Position));
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
        if (!IsHoverPreviewTarget(entity))
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
        if (IsEnemyIntentPreviewTarget(entity))
        {
            ShowIntentPreview(entity, isNewHoverTarget || isStateChanged);
        }
        else
        {
            ShowFriendlyHoverPreview(entity, isNewHoverTarget || isStateChanged);
        }
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
        if (_activeOverlayMode == BattleOverlayMode.Threat)
        {
            _activeOverlayMode = BattleOverlayMode.None;
        }

        ClearThreatOverlay();
    }

    public void ApplyIntentHighlights(BattleIntentPreview preview)
    {
        ActivateOverlayMode(BattleOverlayMode.Execution);
        SetPreviewFocusPresentation(preview?.Actor);
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;
        _hoverIntentEntity = null;
        _hoverIntentPreviewVersion = -1;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        ClearAttackTargetPresentation();

        if (preview == null)
        {
            return;
        }

        if (preview.PathCells.Count > 1)
        {
            _highlightOverlay?.SetPath(preview.PathCells);
        }

        if (preview.Kind == BattleActionKind.Ability || preview.Kind == BattleActionKind.Attack)
        {
            ShowAttackTargetEntities(preview.Target == null ? System.Array.Empty<BattleEntity>() : new[] { preview.Target });
            return;
        }

        GridPosition[] targetCells = preview.AffectedCells
            .Concat(GetIntentTargetCell(preview) is { } targetCell ? new[] { targetCell } : System.Array.Empty<GridPosition>())
            .Distinct()
            .ToArray();
        ShowTargetCells(targetCells);
    }

    public void ShowAbilityTargetHighlight(BattleEntity attacker, AbilityDefinition ability)
    {
        ActivateOverlayMode(BattleOverlayMode.Execution);
        ClearPreviewFocusPresentation();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        ClearHoverIntentPreview();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Attack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        ClearAttackTargetPresentation();
        _activeMovementRangeResult = null;
        _activeMovementPathTarget = null;
        _activeTargetingActor = attacker;
        _activeTargetingAbility = ability;
        _activeTargetHoverCell = null;

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
            .Where(position => position != gridOccupant.Position && gridMap.TryGetCell(position, out _))
            .ToHashSet();

        IReadOnlyList<BattleEntity> entities = _getEntitiesSnapshot?.Invoke() ?? System.Array.Empty<BattleEntity>();
        BattleEntity[] validTargets = entities
            .Where(target => BattleAbilityQueries.IsValidTarget(
                gridMap,
                entities,
                attacker,
                target,
                target.GetComponent<GridOccupantComponent>()?.Position ?? default,
                ability,
                _markEntityDefeated,
                out _))
            .Distinct()
            .ToArray();

        _highlightOverlay?.SetCells(BattleGridHighlightKind.Attack, rangeCells);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _showActionHint?.Invoke(validTargets.Length > 0 ? $"请选择{ability.DisplayName}目标" : "范围内没有有效目标");

        GameLog.Info(
            nameof(BattlePreviewController),
            $"Ability range shown attacker={attacker.EntityId} ability={ability.Id} start={gridOccupant.Position} range={ability.Range} cells={rangeCells.Count} targets={validTargets.Length}");
    }

    public void UpdateAbilityTargetPreview(bool isAbilityTargeting)
    {
        if (!isAbilityTargeting ||
            _activeTargetingActor == null ||
            _activeTargetingAbility == null ||
            _tryResolvePointerGridPosition == null ||
            _getGridMap?.Invoke() == null)
        {
            _activeTargetHoverCell = null;
            ClearAttackTargetPresentation();
            return;
        }

        if (!_tryResolvePointerGridPosition(out GridPosition hoverPosition))
        {
            _activeTargetHoverCell = null;
            ClearAttackTargetPresentation();
            return;
        }

        if (_activeTargetHoverCell == hoverPosition)
        {
            return;
        }

        _activeTargetHoverCell = hoverPosition;
        BattleEntity target = _findEntityAt?.Invoke(hoverPosition);
        IReadOnlyList<BattleEntity> entities = _getEntitiesSnapshot?.Invoke() ?? System.Array.Empty<BattleEntity>();
        if (target == null ||
            !BattleAbilityQueries.IsValidTarget(
                _getGridMap.Invoke(),
                entities,
                _activeTargetingActor,
                target,
                target.GetComponent<GridOccupantComponent>()?.Position ?? default,
                _activeTargetingAbility,
                _markEntityDefeated,
                out _))
        {
            ClearAttackTargetPresentation();
            return;
        }

        ShowAttackTargetEntities(new[] { target });
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

    public void ShowAttackTargetEntities(IEnumerable<BattleEntity> targets)
    {
        BattleEntity[] nextTargets = targets?
            .Where(target => target != null && GodotObject.IsInstanceValid(target))
            .Distinct()
            .ToArray() ?? System.Array.Empty<BattleEntity>();
        BattleTargetPresentationPlan plan = BattleTargetPresentationPlan.Build(nextTargets.Select(target => target.EntityId));

        foreach (BattleEntity entity in _targetPreviewEntities.Except(nextTargets).ToArray())
        {
            SetAttackTargetPresentation(entity, false);
            _targetPreviewEntities.Remove(entity);
        }

        foreach (BattleEntity entity in nextTargets)
        {
            SetAttackTargetPresentation(entity, true);
            _targetPreviewEntities.Add(entity);
        }

        if (!plan.ShowTargetGridCells)
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

        ActivateOverlayMode(BattleOverlayMode.Threat);
        ClearThreatOverlay();
        SetPreviewFocusPresentation(entity);
        BattleHoverPreviewStyle style = BattleHoverPreviewStyle.ForFaction(BattleFaction.Enemy);
        BattleIntentPreview preview = _intentResolver.Preview(_createAiContext?.Invoke(), intent);
        BattleThreatProjection threat = BattleThreatProjectionBuilder.Build(
            _createAiContext?.Invoke(),
            entity,
            _buildBlockedMovementSurfaces?.Invoke(entity) ?? new HashSet<GridSurfacePosition>(),
            includeSources: false);

        SetHoverPreviewCells(style, threat.MovementCells, threat.ThreatCells, threat.TargetCells);
        _showActionHint?.Invoke(DescribeIntentThreatSummary(intent, preview, threat));
        if (shouldLog)
        {
            GameLog.Info(
                nameof(BattlePreviewController),
                $"Intent threat shown enemy={entity.EntityId} intent={intent.TemplateId} moveCells={threat.MovementCells.Count} threatCells={threat.ThreatCells.Count} targets={threat.ThreatenedTargets.Count} resolvedKind={preview.Kind}");
        }
    }

    private void ShowFriendlyHoverPreview(BattleEntity entity, bool shouldLog)
    {
        ActivateOverlayMode(BattleOverlayMode.Threat);
        ClearThreatOverlay();
        SetPreviewFocusPresentation(entity);

        BattleHoverPreviewStyle style = BattleHoverPreviewStyle.ForFaction(BattleFaction.Player);
        BattleThreatProjection threat = BattleThreatProjectionBuilder.Build(
            _createAiContext?.Invoke(),
            entity,
            _buildBlockedMovementSurfaces?.Invoke(entity) ?? new HashSet<GridSurfacePosition>(),
            includeSources: false);

        SetHoverPreviewCells(style, threat.MovementCells, threat.ThreatCells, threat.TargetCells);
        _showActionHint?.Invoke(DescribeFriendlyHoverSummary(entity, threat));

        if (shouldLog)
        {
            GameLog.Info(
                nameof(BattlePreviewController),
                $"Friendly hover preview shown entity={entity.EntityId} moveCells={threat.MovementCells.Count} attackCells={threat.ThreatCells.Count} targets={threat.ThreatenedTargets.Count}");
        }
    }

    private void SetHoverPreviewCells(
        BattleHoverPreviewStyle style,
        IEnumerable<GridPosition> movementCells,
        IEnumerable<GridPosition> attackCells,
        IEnumerable<GridPosition> targetCells)
    {
        BattleHoverCellPresentation presentation = BattleHoverCellPresentation.Build(attackCells, targetCells);

        _highlightOverlay?.SetCellsBatch(
            (style.MoveKind, movementCells ?? System.Array.Empty<GridPosition>()),
            (style.AttackKind, presentation.AttackCells),
            (BattleGridHighlightKind.Target, presentation.TargetCells));
        _highlightOverlay?.SetTargetPointers(presentation.TargetPointerCells);
    }

    private void ActivateOverlayMode(BattleOverlayMode mode)
    {
        if (_activeOverlayMode == mode)
        {
            return;
        }

        if (_activeOverlayMode == BattleOverlayMode.Threat)
        {
            ClearThreatOverlay();
        }
        else if (_activeOverlayMode == BattleOverlayMode.Execution)
        {
            ClearExecutionOverlay();
        }

        _activeOverlayMode = mode;
    }

    private void ClearThreatOverlay()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Threat);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyMove);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyAttack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        ClearAttackTargetPresentation();
        ClearPreviewFocusPresentation();
    }

    private void ClearExecutionOverlay()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Move);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Attack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyMove);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyAttack);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        _activeTargetingActor = null;
        _activeTargetingAbility = null;
        _activeTargetHoverCell = null;
        ClearAttackTargetPresentation();
        ClearPreviewFocusPresentation();
    }

    private static string DescribeIntentThreatSummary(
        BattleIntent intent,
        BattleIntentPreview preview,
        BattleThreatProjection threat)
    {
        if (intent == null)
        {
            return "敌方意图未知";
        }

        if (preview == null || !preview.HasAction)
        {
            return string.IsNullOrWhiteSpace(preview?.DetailText)
                ? intent.Summary
                : preview.DetailText;
        }

        string targetText = preview.Target == null
            ? "目标未定"
            : $"倾向目标：{preview.Target.DisplayName}";
        string threatText = threat == null
            ? ""
            : $"威胁 {threat.ThreatCells.Count} 格，可移动 {threat.MovementCells.Count} 格";
        string hitText = threat?.ThreatenedTargets.Count > 0
            ? $"，可威胁 {string.Join("、", threat.ThreatenedTargets.Select(target => target.DisplayName))}"
            : "";

        return $"{intent.Actor?.DisplayName ?? "敌方单位"}：{intent.DisplayName}。{targetText}，{threatText}{hitText}";
    }

    private static bool IsEnemyIntentPreviewTarget(BattleEntity entity)
    {
        return entity != null &&
               !BattleRuleQueries.IsDefeated(entity) &&
               entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
               entity.GetComponent<GridOccupantComponent>() != null;
    }

    private static bool IsFriendlyHoverPreviewTarget(BattleEntity entity)
    {
        return entity != null &&
               !BattleRuleQueries.IsDefeated(entity) &&
               entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Player &&
               entity.GetComponent<GridOccupantComponent>() != null;
    }

    private static bool IsHoverPreviewTarget(BattleEntity entity)
    {
        return IsEnemyIntentPreviewTarget(entity) || IsFriendlyHoverPreviewTarget(entity);
    }

    private static string DescribeFriendlyHoverSummary(BattleEntity entity, BattleThreatProjection threat)
    {
        if (entity == null)
        {
            return "";
        }

        string hitText = threat?.ThreatenedTargets.Count > 0
            ? $"，可攻击 {string.Join("、", threat.ThreatenedTargets.Select(target => target.DisplayName))}"
            : "";
        return $"{entity.DisplayName}：移动 {threat?.MovementCells.Count ?? 0} 格，攻击范围 {threat?.ThreatCells.Count ?? 0} 格{hitText}";
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

    private static void SetSelectedPresentation(BattleEntity entity, bool selected)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        entity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(selected);
    }

    private void SetPreviewFocusPresentation(BattleEntity entity)
    {
        if (_previewFocusEntity == entity)
        {
            return;
        }

        SetPreviewFocusPresentation(_previewFocusEntity, false);
        _previewFocusEntity = entity;
        SetPreviewFocusPresentation(_previewFocusEntity, true);
    }

    private void ClearPreviewFocusPresentation()
    {
        SetPreviewFocusPresentation(_previewFocusEntity, false);
        _previewFocusEntity = null;
    }

    private static void SetPreviewFocusPresentation(BattleEntity entity, bool focused)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        entity.GetComponent<BattleUnitPresentationComponent>()?.SetPreviewFocus(focused);
    }

    private void ClearAttackTargetPresentation()
    {
        foreach (BattleEntity entity in _targetPreviewEntities.ToArray())
        {
            SetAttackTargetPresentation(entity, false);
        }

        _targetPreviewEntities.Clear();
    }

    private static void SetAttackTargetPresentation(BattleEntity entity, bool previewed)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        // Attack target preview uses the same red outline visual as hit feedback; hit feedback owns clearing after execution starts.
        entity.GetComponent<BattleUnitPresentationComponent>()?.SetAttackTargetPreview(previewed);
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
