using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
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
    // Default is 0.28s; battle site scenes may lower this during prototype tuning to keep turn flow readable.
    public double UnitMoveDuration { get; set; } = 0.28;

    [ExportGroup("Hit Feedback")]

    [Export]
    // Damage numbers start close to the unit body; the label tween supplies the upward drift.
    public Vector2 DamageNumberGlobalOffset { get; set; } = BattleDamageNumberMotionSpec.Default.SpawnOffset;

    [ExportGroup("Action Cue")]

    [Export]
    // A short pre-action cue lowers information density without changing battle rules.
    public double ActionCueDurationSeconds { get; set; } = BattleActionCueSequencer.DefaultDurationSeconds;

    [Export]
    public PackedScene ActionCueScene { get; set; }

    private readonly Dictionary<BattleEntity, Tween> _movementTweens = new();
    private readonly Dictionary<BattleEntity, BattleIntentMarker> _intentMarkers = new();
    private readonly Dictionary<BattleEntity, BattleActionCue> _actionCues = new();
    private readonly HashSet<BattleEntity> _hitOutlinedEntities = new();
    private readonly HashSet<BattleEntity> _defeatedEntities = new();
    private readonly HashSet<BattleEntity> _pendingDefeatedPresentations = new();
    private readonly List<TaskCompletionSource<bool>> _defeatedPresentationWaiters = new();
    private TryResolveCellGlobalPosition _tryResolveCellGlobalPosition;
    private ApplyEntityRenderSort _applyEntityRenderSort;

    public bool HasActiveMovementTweens => _movementTweens.Count > 0;
    public bool HasPendingDefeatedPresentations => _pendingDefeatedPresentations.Count > 0;

    public override void _Ready()
    {
        ActionCueScene ??= GD.Load<PackedScene>("res://scenes/battle/feedback/BattleActionCue.tscn");
        GameLog.Info(nameof(BattleUnitRoot), $"Ready path={GetPath()} entities={GetEntitiesSnapshot().Count}");
    }

    public override void _ExitTree()
    {
        // Movement tweens own frame callbacks that close over BattleEntity; stop them before entities are freed.
        KillAllMovementTweens();
        ClearAllActionCues();
        SetHitOutlines(_hitOutlinedEntities.ToArray(), visible: false);
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

    public void MoveEntityTo(
        BattleEntity entity,
        IReadOnlyList<GridSurfacePosition> path,
        bool restartMoveAnimation = true,
        bool returnToIdleOnComplete = true)
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
            animation?.PlayMove(restartMoveAnimation);
            entity.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Move);
            AnimateEntityMove(entity, globalPath, surfacePath, returnToIdleOnComplete);
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

        BattleDamageEvent[] damageEvents = result.DamageEvents?.ToArray() ?? System.Array.Empty<BattleDamageEvent>();
        BattleHitFeedbackPlan feedbackPlan = BattleHitFeedbackPlanner.Build(damageEvents);
        BattleEntity[] hitTargets = ResolveHitFeedbackTargets(damageEvents, feedbackPlan).ToArray();
        UnitAnimationComponent actorAnimation = result.Actor?.GetComponent<UnitAnimationComponent>();
        if (result.Target != null)
        {
            actorAnimation?.FaceToward(result.Target.GlobalPosition);
        }

        actorAnimation?.PlayAttack();
        result.Actor?.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        // Hit outline covers the whole attack animation so actual impact feedback belongs to units, not grid cells.
        SetHitOutlines(hitTargets, visible: true);
        _ = PlayHitFeedbackAsync(result.Actor, damageEvents, hitTargets);
    }

    public Task ShowActionCueAsync(BattleEntity entity, BattleFaction faction, double durationSeconds)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity) || ActionCueScene == null)
        {
            return Task.CompletedTask;
        }

        HideActionCue(entity);
        if (ActionCueScene.Instantiate() is not BattleActionCue cue)
        {
            GameLog.Warn(nameof(BattleUnitRoot), "Action cue scene does not instantiate BattleActionCue");
            return Task.CompletedTask;
        }

        entity.AddChild(cue);
        cue.Position = Vector2.Zero;
        cue.Play(faction, durationSeconds);
        _actionCues[entity] = cue;
        entity.GetComponent<BattleUnitPresentationComponent>()?.SetPreviewFocus(true);
        GameLog.Info(nameof(BattleUnitRoot), $"Action cue shown entity={entity.EntityId} faction={faction} duration={durationSeconds:0.00}");
        return Task.CompletedTask;
    }

    public Task HideActionCueAsync(BattleEntity entity)
    {
        HideActionCue(entity);
        return Task.CompletedTask;
    }

    private void HideActionCue(BattleEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        entity.GetComponent<BattleUnitPresentationComponent>()?.SetPreviewFocus(false);
        if (!_actionCues.Remove(entity, out BattleActionCue cue) ||
            cue == null ||
            !GodotObject.IsInstanceValid(cue))
        {
            return;
        }

        cue.Finish();
    }

    private void ClearAllActionCues()
    {
        foreach (BattleEntity entity in _actionCues.Keys.ToArray())
        {
            HideActionCue(entity);
        }
    }

    private async Task PlayHitFeedbackAsync(
        BattleEntity actor,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        IReadOnlyList<BattleEntity> outlinedTargets)
    {
        UnitAnimationComponent actorAnimation = actor?.GetComponent<UnitAnimationComponent>();
        double impactDelaySeconds = actorAnimation?.ResolveAttackImpactDelaySeconds() ?? 0;
        double attackDurationSeconds = actorAnimation?.ResolveAttackDurationSeconds() ?? 0.45;

        await WaitSeconds(impactDelaySeconds);
        actor?.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.AttackImpact);
        SpawnDamageNumbers(damageEvents);

        double remainingSeconds = System.Math.Max(0.05, attackDurationSeconds - System.Math.Max(0, impactDelaySeconds));
        await WaitSeconds(remainingSeconds);
        SetHitOutlines(outlinedTargets, visible: false);
    }

    private IEnumerable<BattleEntity> ResolveHitFeedbackTargets(
        IReadOnlyList<BattleDamageEvent> damageEvents,
        BattleHitFeedbackPlan plan)
    {
        if (damageEvents == null || plan == null)
        {
            yield break;
        }

        foreach (string targetId in plan.OutlinedTargetIds)
        {
            BattleEntity target = damageEvents
                .FirstOrDefault(damage => damage != null && damage.TargetId == targetId)
                ?.Target;
            if (target != null && GodotObject.IsInstanceValid(target))
            {
                yield return target;
            }
        }
    }

    private void SetHitOutlines(IReadOnlyList<BattleEntity> targets, bool visible)
    {
        if (targets == null)
        {
            return;
        }

        foreach (BattleEntity target in targets)
        {
            if (target == null || !GodotObject.IsInstanceValid(target))
            {
                continue;
            }

            target.GetComponent<BattleUnitPresentationComponent>()?.SetHitOutlineVisible(visible);
            if (visible)
            {
                _hitOutlinedEntities.Add(target);
            }
            else
            {
                _hitOutlinedEntities.Remove(target);
            }
        }
    }

    private void SpawnDamageNumbers(IReadOnlyList<BattleDamageEvent> damageEvents)
    {
        if (damageEvents == null)
        {
            return;
        }

        foreach (BattleDamageEvent damage in damageEvents.Where(damage => damage?.DamageApplied > 0))
        {
            if (damage.Target == null || !GodotObject.IsInstanceValid(damage.Target))
            {
                continue;
            }

            BattleDamageNumber number = GameUiSceneFactory.CreateBattleDamageNumber(nameof(BattleUnitRoot));
            if (number == null)
            {
                continue;
            }

            AddChild(number);
            number.GlobalPosition = damage.Target.GlobalPosition + DamageNumberGlobalOffset;
            number.Play($"-{damage.DamageApplied}");
        }
    }

    private async Task WaitSeconds(double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
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
        entity.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Defeated);

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
        IReadOnlyList<GridSurfacePosition> surfacePath,
        bool returnToIdleOnComplete)
    {
        if (_movementTweens.Remove(entity, out Tween previousTween))
        {
            KillMovementTween(previousTween);
        }

        if (!IsEntityAlive(entity))
        {
            return;
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
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.SetEase(Tween.EaseType.In);

        int lastSegmentIndex = -1;
        int stepCount = System.Math.Max(1, globalPath.Count - 1);
        double totalDuration = UnitMoveDuration * stepCount;
        tween.TweenMethod(Callable.From<float>(progress =>
        {
            if (!IsInsideTree() || !IsEntityAlive(entity))
            {
                KillTrackedMovementTween(entity);
                return;
            }

            ResolveMovementSegment(globalPath, progress, out int segmentIndex, out float segmentProgress);
            Vector2 from = globalPath[segmentIndex - 1];
            Vector2 to = globalPath[segmentIndex];
            entity.GlobalPosition = from.Lerp(to, segmentProgress);

            if (segmentIndex == lastSegmentIndex)
            {
                return;
            }

            lastSegmentIndex = segmentIndex;
            ApplyRenderSort(entity, ResolveSurfaceAt(surfacePath, segmentIndex));
            FaceAlongSegment(entity.GetComponent<UnitAnimationComponent>(), from, to);
        }), 0f, 1f, totalDuration);

        tween.Finished += () =>
        {
            if (!IsInsideTree() || !IsEntityAlive(entity))
            {
                _movementTweens.Remove(entity);
                return;
            }

            entity.GlobalPosition = globalPath[^1];
            if (surfacePath?.Count > 0)
            {
                ApplyRenderSort(entity, surfacePath[^1]);
            }
            _movementTweens.Remove(entity);
            if (returnToIdleOnComplete)
            {
                entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
            }
        };

        _movementTweens[entity] = tween;
    }

    private void KillAllMovementTweens()
    {
        foreach (Tween tween in _movementTweens.Values.ToArray())
        {
            KillMovementTween(tween);
        }

        _movementTweens.Clear();
    }

    private void KillTrackedMovementTween(BattleEntity entity)
    {
        if (_movementTweens.Remove(entity, out Tween tween))
        {
            KillMovementTween(tween);
        }
    }

    private static void KillMovementTween(Tween tween)
    {
        if (tween != null && GodotObject.IsInstanceValid(tween))
        {
            tween.Kill();
        }
    }

    private static bool IsEntityAlive(BattleEntity entity)
    {
        return entity != null && GodotObject.IsInstanceValid(entity);
    }

    private static void ResolveMovementSegment(
        IReadOnlyList<Vector2> globalPath,
        float progress,
        out int segmentIndex,
        out float segmentProgress)
    {
        int stepCount = System.Math.Max(1, (globalPath?.Count ?? 0) - 1);
        float clampedProgress = Mathf.Clamp(progress, 0f, 1f);
        if (clampedProgress >= 1f)
        {
            segmentIndex = stepCount;
            segmentProgress = 1f;
            return;
        }

        float pathProgress = clampedProgress * stepCount;
        int segmentOffset = System.Math.Clamp((int)System.Math.Floor(pathProgress), 0, stepCount - 1);
        segmentIndex = segmentOffset + 1;
        segmentProgress = Mathf.Clamp(pathProgress - segmentOffset, 0f, 1f);
    }

    private static GridSurfacePosition ResolveSurfaceAt(
        IReadOnlyList<GridSurfacePosition> surfacePath,
        int index)
    {
        if (surfacePath == null || surfacePath.Count == 0)
        {
            return default;
        }

        return surfacePath[System.Math.Clamp(index, 0, surfacePath.Count - 1)];
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
