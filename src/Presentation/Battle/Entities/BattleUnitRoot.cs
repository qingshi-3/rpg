using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.Entities;

public delegate bool TryResolveCellGlobalPosition(GridPosition position, out Vector2 globalPosition);
public delegate void ApplyEntityRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition);

public partial class BattleUnitRoot : Node2D
{
    [ExportGroup("单位移动表现")]

    [Export]
    public double UnitMoveDuration { get; set; } = 0.28;

    private readonly Dictionary<BattleEntity, Tween> _movementTweens = new();
    private readonly Dictionary<BattleEntity, BattleIntentMarker> _intentMarkers = new();
    private readonly HashSet<BattleEntity> _defeatedEntities = new();
    private readonly HashSet<BattleEntity> _pendingDefeatedPresentations = new();
    private readonly List<TaskCompletionSource<bool>> _defeatedPresentationWaiters = new();
    private TryResolveCellGlobalPosition _tryResolveCellGlobalPosition;
    private ApplyEntityRenderSort _applyEntityRenderSort;

    public bool HasActiveMovementTweens => _movementTweens.Count > 0;
    public bool HasPendingDefeatedPresentations => _pendingDefeatedPresentations.Count > 0;

    public override void _Ready()
    {
        GameLog.Info(nameof(BattleUnitRoot), $"Ready path={GetPath()} entities={GetEntitiesSnapshot().Count}");
    }

    public override void _ExitTree()
    {
        _pendingDefeatedPresentations.Clear();
        CompleteDefeatedPresentationWaiters();
    }

    public void Initialize(
        TryResolveCellGlobalPosition tryResolveCellGlobalPosition,
        ApplyEntityRenderSort applyEntityRenderSort = null)
    {
        _tryResolveCellGlobalPosition = tryResolveCellGlobalPosition;
        _applyEntityRenderSort = applyEntityRenderSort;
        GameLog.Info(nameof(BattleUnitRoot), $"Initialized path={GetPath()} hasCellResolver={_tryResolveCellGlobalPosition != null} hasRenderSort={_applyEntityRenderSort != null}");
    }

    public IReadOnlyList<BattleEntity> GetEntitiesSnapshot()
    {
        return EnumerateBattleEntities(this).ToArray();
    }

    public BattleEntity FindEntityAt(GridPosition position)
    {
        return GetEntitiesSnapshot()
            .Select(entity => new
            {
                Entity = entity,
                GridOccupant = entity.GetComponent<GridOccupantComponent>()
            })
            .Where(item => item.GridOccupant?.Position == position && !BattleRuleQueries.IsDefeated(item.Entity))
            .OrderByDescending(item => item.GridOccupant.GridHeight)
            .Select(item => item.Entity)
            .FirstOrDefault();
    }

    public IEnumerable<BattleEntity> EnumerateAliveFaction(BattleFaction faction)
    {
        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (!BattleRuleQueries.IsDefeated(entity) &&
                entity.GetComponent<FactionComponent>()?.Faction == faction)
            {
                yield return entity;
            }
        }
    }

    public void RestoreTurnResourcesForFaction(BattleFaction faction)
    {
        foreach (BattleEntity entity in EnumerateAliveFaction(faction))
        {
            entity.GetComponent<ActionPointComponent>()?.RestoreToMax();
            entity.GetComponent<MovementComponent>()?.RestoreMoveUses();
        }
    }

    public ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(BattleEntity movingEntity)
    {
        var blockedSurfaces = new HashSet<GridSurfacePosition>();

        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (!BattleRuleQueries.TryGetMovementBlockSurface(
                    movingEntity,
                    entity,
                    out GridSurfacePosition blockSurface))
            {
                continue;
            }

            blockedSurfaces.Add(blockSurface);
        }

        return blockedSurfaces;
    }

    public void MoveEntityTo(BattleEntity entity, IReadOnlyList<GridSurfacePosition> path)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        GridSurfacePosition previousPosition = gridOccupant.SurfacePosition;
        GridSurfacePosition targetPosition = path == null || path.Count == 0
            ? previousPosition
            : path[^1];
        Vector2 previousGlobal = entity.GlobalPosition;
        gridOccupant.SetSurfacePosition(targetPosition);
        ApplyRenderSort(entity, previousPosition);
        if (TryBuildMovementGlobalPath(path, previousGlobal, previousPosition, out Vector2[] globalPath, out GridSurfacePosition[] surfacePath))
        {
            UnitAnimationComponent animation = entity.GetComponent<UnitAnimationComponent>();
            FaceAlongSegment(animation, globalPath[0], globalPath[1]);
            animation?.PlayMove();
            AnimateEntityMove(entity, globalPath, surfacePath);
            GameLog.Info(
                nameof(BattleUnitRoot),
                $"Entity visual move id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition} steps={System.Math.Max(0, globalPath.Length - 1)} fromGlobal={previousGlobal} toGlobal={globalPath[^1]} stepDuration={UnitMoveDuration:0.00}");
            return;
        }

        if (_tryResolveCellGlobalPosition?.Invoke(targetPosition.Position, out Vector2 fallbackGlobal) == true)
        {
            entity.GetComponent<UnitAnimationComponent>()?.FaceToward(fallbackGlobal);
            entity.GlobalPosition = fallbackGlobal;
            ApplyRenderSort(entity, targetPosition);
        }

        GameLog.Warn(
            nameof(BattleUnitRoot),
            $"Entity grid moved without step animation id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition}");
    }

    public void PlayActionResultAnimation(BattleActionResult result)
    {
        if (result?.Success != true)
        {
            return;
        }

        if (result.Kind is not (BattleActionKind.Ability or BattleActionKind.Attack))
        {
            return;
        }

        UnitAnimationComponent actorAnimation = result.Actor?.GetComponent<UnitAnimationComponent>();
        if (result.Target != null)
        {
            actorAnimation?.FaceToward(result.Target.GlobalPosition);
        }

        actorAnimation?.PlayAttack();
    }

    public void SetIntentMarker(BattleEntity entity, BattleIntent intent)
    {
        if (entity == null)
        {
            return;
        }

        if (!_intentMarkers.TryGetValue(entity, out BattleIntentMarker marker) || marker == null || !GodotObject.IsInstanceValid(marker))
        {
            marker = entity.GetNodeOrNull<BattleIntentMarker>("IntentMarker");
            if (marker == null && intent == null)
            {
                return;
            }

            if (marker == null)
            {
                marker = GameUiSceneFactory.CreateBattleIntentMarker(nameof(BattleUnitRoot));
                if (marker == null)
                {
                    return;
                }

                marker.Name = "IntentMarker";
                entity.AddChild(marker);
            }

            _intentMarkers[entity] = marker;
        }

        marker.SetIntent(intent);
    }

    public void ClearIntentMarkers()
    {
        foreach (BattleIntentMarker marker in _intentMarkers.Values.ToArray())
        {
            if (marker != null && GodotObject.IsInstanceValid(marker))
            {
                marker.SetIntent(null);
            }
        }
    }

    public void PlayIdleForActiveEntities()
    {
        int count = 0;
        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (entity == null ||
                !GodotObject.IsInstanceValid(entity) ||
                BattleRuleQueries.IsDefeated(entity))
            {
                continue;
            }

            entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
            count++;
        }

        GameLog.Info(nameof(BattleUnitRoot), $"Active unit animations set to idle count={count}");
    }

    public Task WaitForDefeatedPresentationsAsync()
    {
        if (!HasPendingDefeatedPresentations)
        {
            return Task.CompletedTask;
        }

        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _defeatedPresentationWaiters.Add(waiter);
        return waiter.Task;
    }

    public void MarkEntityDefeated(BattleEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        if (!_defeatedEntities.Add(entity))
        {
            return;
        }

        SetIntentMarker(entity, null);

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant != null)
        {
            gridOccupant.BlocksMovement = false;
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable != null)
        {
            selectable.IsSelectable = false;
        }

        TargetableComponent targetable = entity.GetComponent<TargetableComponent>();
        if (targetable != null)
        {
            targetable.IsTargetable = false;
        }

        entity.InputPickable = false;
        SetCollisionShapesDisabled(entity, true);
        entity.DebugMarkerColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        entity.QueueRedraw();

        UnitAnimationComponent animation = entity.GetComponent<UnitAnimationComponent>();
        bool hideAfterDefeated = animation?.AnimationSet?.HideAfterDefeatedAnimation != false;
        double defeatedDelaySeconds = 0;
        double defeatedMinimumDurationSeconds = 0;
        DamageReactionComponent damageReaction = entity.GetComponent<DamageReactionComponent>();
        damageReaction?.TryConsumeDefeatedPresentationTiming(
            out defeatedDelaySeconds,
            out defeatedMinimumDurationSeconds);

        if (animation != null)
        {
            _pendingDefeatedPresentations.Add(entity);
            if (animation.PlayDefeated(
                    () => CompleteDefeatedPresentation(entity, hideAfterDefeated),
                    defeatedDelaySeconds,
                    defeatedMinimumDurationSeconds))
            {
                GameLog.Info(nameof(BattleUnitRoot), $"Entity defeated presentation started id={entity.EntityId} name={entity.DisplayName} delay={defeatedDelaySeconds:0.00} minDuration={defeatedMinimumDurationSeconds:0.00} pending={_pendingDefeatedPresentations.Count}");
                return;
            }

            _pendingDefeatedPresentations.Remove(entity);
        }

        if (hideAfterDefeated)
        {
            HideDefeatedEntity(entity);
        }

        CompleteDefeatedPresentationWaitersIfIdle();
        GameLog.Info(nameof(BattleUnitRoot), $"Entity defeated id={entity.EntityId} name={entity.DisplayName}");
    }

    private bool TryBuildMovementGlobalPath(
        IReadOnlyList<GridSurfacePosition> path,
        Vector2 currentGlobal,
        GridSurfacePosition currentSurfacePosition,
        out Vector2[] globalPath,
        out GridSurfacePosition[] surfacePath)
    {
        var points = new List<Vector2> { currentGlobal };
        var surfaces = new List<GridSurfacePosition> { currentSurfacePosition };

        if (path == null || path.Count == 0)
        {
            globalPath = points.ToArray();
            surfacePath = surfaces.ToArray();
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            if (_tryResolveCellGlobalPosition?.Invoke(path[index].Position, out Vector2 globalPosition) != true)
            {
                globalPath = points.ToArray();
                surfacePath = surfaces.ToArray();
                return false;
            }

            if (points[^1].DistanceSquaredTo(globalPosition) > 0.01f)
            {
                points.Add(globalPosition);
                surfaces.Add(path[index]);
            }
        }

        globalPath = points.ToArray();
        surfacePath = surfaces.ToArray();
        return globalPath.Length > 1;
    }

    private void AnimateEntityMove(
        BattleEntity entity,
        IReadOnlyList<Vector2> globalPath,
        IReadOnlyList<GridSurfacePosition> surfacePath)
    {
        if (_movementTweens.Remove(entity, out Tween previousTween))
        {
            previousTween.Kill();
        }

        if (!IsInsideTree() || UnitMoveDuration <= 0)
        {
            entity.GlobalPosition = globalPath[^1];
            if (surfacePath?.Count > 0)
            {
                ApplyRenderSort(entity, surfacePath[^1]);
            }
            return;
        }

        entity.GlobalPosition = globalPath[0];
        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);

        for (int index = 1; index < globalPath.Count; index++)
        {
            Vector2 from = globalPath[index - 1];
            Vector2 to = globalPath[index];
            GridSurfacePosition segmentSurface = surfacePath != null && index < surfacePath.Count
                ? surfacePath[index]
                : surfacePath is { Count: > 0 }
                    ? surfacePath[^1]
                    : entity.GetComponent<GridOccupantComponent>()?.SurfacePosition ?? default;
            tween.TweenCallback(Callable.From(() =>
            {
                ApplyRenderSort(entity, segmentSurface);
                FaceAlongSegment(entity.GetComponent<UnitAnimationComponent>(), from, to);
            }));
            tween.TweenProperty(entity, "global_position", to, UnitMoveDuration);
        }

        tween.Finished += () =>
        {
            entity.GlobalPosition = globalPath[^1];
            if (surfacePath?.Count > 0)
            {
                ApplyRenderSort(entity, surfacePath[^1]);
            }
            _movementTweens.Remove(entity);
            entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        };

        _movementTweens[entity] = tween;
    }

    private static void HideDefeatedEntity(BattleEntity entity)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        entity.Visible = false;
        entity.Modulate = new Color(1f, 1f, 1f, 0.45f);
        entity.QueueRedraw();
    }

    private void CompleteDefeatedPresentation(BattleEntity entity, bool hideAfterDefeated)
    {
        if (hideAfterDefeated)
        {
            HideDefeatedEntity(entity);
        }

        _pendingDefeatedPresentations.Remove(entity);
        GameLog.Info(nameof(BattleUnitRoot), $"Entity defeated presentation completed id={entity?.EntityId} pending={_pendingDefeatedPresentations.Count}");
        CompleteDefeatedPresentationWaitersIfIdle();
    }

    private void CompleteDefeatedPresentationWaitersIfIdle()
    {
        if (HasPendingDefeatedPresentations)
        {
            return;
        }

        CompleteDefeatedPresentationWaiters();
    }

    private void CompleteDefeatedPresentationWaiters()
    {
        foreach (TaskCompletionSource<bool> waiter in _defeatedPresentationWaiters.ToArray())
        {
            waiter.TrySetResult(true);
        }

        _defeatedPresentationWaiters.Clear();
    }

    private static void FaceAlongSegment(UnitAnimationComponent animation, Vector2 fromGlobal, Vector2 toGlobal)
    {
        animation?.FaceHorizontalDirection(toGlobal.X - fromGlobal.X);
    }

    private void ApplyRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition)
    {
        _applyEntityRenderSort?.Invoke(entity, surfacePosition);
    }

    private static void SetCollisionShapesDisabled(Node node, bool disabled)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is CollisionShape2D collisionShape)
            {
                collisionShape.Disabled = disabled;
            }

            SetCollisionShapesDisabled(child, disabled);
        }
    }

    private static IEnumerable<BattleEntity> EnumerateBattleEntities(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BattleEntity entity)
            {
                yield return entity;
            }

            foreach (BattleEntity descendant in EnumerateBattleEntities(child))
            {
                yield return descendant;
            }
        }
    }
}
