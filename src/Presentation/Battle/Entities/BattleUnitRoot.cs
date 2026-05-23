using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle;
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
public delegate bool TryResolveFootprintGlobalPosition(GridPosition anchor, Vector2I footprintSize, out Vector2 globalPosition);
public delegate void ApplyEntityRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition);

public partial class BattleUnitRoot : Node2D
{
    [ExportGroup("单位移动表现")]

    [Export]
    // Default is 0.28s; battle site scenes may lower this during prototype tuning to keep turn flow readable.
    public double UnitMoveDuration { get; set; } = 0.28;

    [Export]
    // Historical tuning slot kept for scene compatibility. Runtime action seconds
    // remain the visual movement clock so damage and attack feedback stay aligned.
    public double VisualMoveSmoothingSeconds { get; set; } = 0.06;

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

    private readonly Dictionary<BattleEntity, MovementLane> _movementLanes = new();
    private readonly Dictionary<BattleEntity, double> _pendingMovementIdleSeconds = new();
    private readonly Dictionary<BattleEntity, BattleIntentMarker> _intentMarkers = new();
    private readonly Dictionary<BattleEntity, BattleActionCue> _actionCues = new();
    private readonly HashSet<BattleEntity> _hitOutlinedEntities = new();
    private readonly HashSet<BattleEntity> _commandSelectedEntities = new();
    private readonly HashSet<BattleEntity> _defeatedEntities = new();
    private readonly HashSet<BattleEntity> _pendingDefeatedPresentations = new();
    private readonly List<TaskCompletionSource<bool>> _defeatedPresentationWaiters = new();
    private TryResolveCellGlobalPosition _tryResolveCellGlobalPosition;
    private TryResolveFootprintGlobalPosition _tryResolveFootprintGlobalPosition;
    private ApplyEntityRenderSort _applyEntityRenderSort;

    public bool HasActiveMovementTweens => _movementLanes.Count > 0;
    public int ActiveMovementTweenCount => _movementLanes.Count;
    public bool HasPendingDefeatedPresentations => _pendingDefeatedPresentations.Count > 0;

    public override void _Ready()
    {
        ActionCueScene ??= GD.Load<PackedScene>("res://scenes/battle/feedback/BattleActionCue.tscn");
        GameLog.Info(nameof(BattleUnitRoot), $"Ready path={GetPath()} entities={GetEntitiesSnapshot().Count}");
    }

    public override void _Process(double delta)
    {
        if ((_movementLanes.Count == 0 && _pendingMovementIdleSeconds.Count == 0) || delta <= 0)
        {
            return;
        }

        foreach ((BattleEntity entity, MovementLane lane) in _movementLanes.ToArray())
        {
            if (!IsEntityAlive(entity) || !AdvanceMovementLane(entity, lane, delta))
            {
                _movementLanes.Remove(entity);
            }
        }

        UpdatePendingMovementIdle(delta);
    }

    public override void _ExitTree()
    {
        _movementLanes.Clear();
        _pendingMovementIdleSeconds.Clear();
        ClearAllActionCues();
        ClearCommandSelection();
        SetHitOutlines(_hitOutlinedEntities.ToArray(), visible: false);
        _pendingDefeatedPresentations.Clear();
        CompleteDefeatedPresentationWaiters();
    }

    public void Initialize(
        TryResolveCellGlobalPosition tryResolveCellGlobalPosition,
        TryResolveFootprintGlobalPosition tryResolveFootprintGlobalPosition,
        ApplyEntityRenderSort applyEntityRenderSort = null)
    {
        _tryResolveCellGlobalPosition = tryResolveCellGlobalPosition;
        _tryResolveFootprintGlobalPosition = tryResolveFootprintGlobalPosition;
        _applyEntityRenderSort = applyEntityRenderSort;
        GameLog.Info(nameof(BattleUnitRoot), $"Initialized path={GetPath()} hasCellResolver={_tryResolveCellGlobalPosition != null} hasFootprintResolver={_tryResolveFootprintGlobalPosition != null} hasRenderSort={_applyEntityRenderSort != null}");
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
            .Where(item => item.GridOccupant != null &&
                           ContainsGridFootprint(item.GridOccupant, position) &&
                           !BattleRuleQueries.IsDefeated(item.Entity))
            .OrderByDescending(item => item.GridOccupant.GridHeight)
            .Select(item => item.Entity)
            .FirstOrDefault();
    }

    private static bool ContainsGridFootprint(GridOccupantComponent gridOccupant, GridPosition position)
    {
        if (gridOccupant == null)
        {
            return false;
        }

        int width = BattleFootprintCells.NormalizeSize(gridOccupant.FootprintWidth);
        int height = BattleFootprintCells.NormalizeSize(gridOccupant.FootprintHeight);
        return position.X >= gridOccupant.GridX &&
               position.X < gridOccupant.GridX + width &&
               position.Y >= gridOccupant.GridY &&
               position.Y < gridOccupant.GridY + height;
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
        bool returnToIdleOnComplete = true,
        double stepDurationSeconds = -1)
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
        if (TryBuildMovementGlobalPath(
                path,
                gridOccupant,
                previousGlobal,
                previousPosition,
                out Vector2[] globalPath,
                out GridSurfacePosition[] surfacePath))
        {
            UnitAnimationComponent animation = entity.GetComponent<UnitAnimationComponent>();
            FaceAlongSegment(animation, globalPath[0], globalPath[1]);
            _pendingMovementIdleSeconds.Remove(entity);
            animation?.PlayMove(restartMoveAnimation);
            entity.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Move);
            AnimateEntityMove(entity, globalPath, surfacePath, returnToIdleOnComplete, stepDurationSeconds);
            GameLog.Trace(nameof(BattleUnitRoot),
                $"Entity visual move id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition} footprint={gridOccupant.FootprintWidth}x{gridOccupant.FootprintHeight} steps={System.Math.Max(0, globalPath.Length - 1)} fromGlobal={previousGlobal} toGlobal={globalPath[^1]} stepDuration={ResolveMoveStepDurationSeconds(stepDurationSeconds):0.00}");
            return;
        }

        if (TryResolveMovementGlobalPosition(gridOccupant, targetPosition, out Vector2 fallbackGlobal))
        {
            entity.GetComponent<UnitAnimationComponent>()?.FaceToward(fallbackGlobal);
            entity.GlobalPosition = fallbackGlobal;
            ApplyRenderSort(entity, targetPosition);
        }

        GameLog.Warn(
            nameof(BattleUnitRoot),
            $"Entity grid moved without step animation id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition}");
    }

    public double PlayActionResultAnimation(BattleActionResult result)
    {
        if (result?.Success != true)
        {
            return 0;
        }

        if (result.Kind is not (BattleActionKind.Ability or BattleActionKind.Attack))
        {
            return 0;
        }

        BattleDamageEvent[] damageEvents = result.DamageEvents?.ToArray() ?? System.Array.Empty<BattleDamageEvent>();
        BattleHitFeedbackPlan feedbackPlan = BattleHitFeedbackPlanner.Build(damageEvents);
        BattleEntity[] hitTargets = ResolveHitFeedbackTargets(damageEvents, feedbackPlan).ToArray();
        UnitAnimationComponent actorAnimation = result.Actor?.GetComponent<UnitAnimationComponent>();
        if (result.Target != null)
        {
            actorAnimation?.FaceToward(result.Target.GlobalPosition);
        }

        double attackDurationSeconds = actorAnimation?.ResolveAttackDurationSeconds() ?? 0;
        actorAnimation?.PlayAttack();
        result.Actor?.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        // Hit outline covers the whole attack animation so actual impact feedback belongs to units, not grid cells.
        SetHitOutlines(hitTargets, visible: true);
        _ = PlayHitFeedbackAsync(result.Actor, damageEvents, hitTargets);
        return attackDurationSeconds;
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

    public int SetCommandSelectionByEntityIds(ISet<string> entityIds)
    {
        HashSet<string> selectedIds = entityIds == null
            ? new HashSet<string>(System.StringComparer.Ordinal)
            : new HashSet<string>(entityIds.Where(id => !string.IsNullOrWhiteSpace(id)), System.StringComparer.Ordinal);
        BattleEntity[] nextSelection = GetEntitiesSnapshot()
            .Where(entity =>
                entity != null &&
                GodotObject.IsInstanceValid(entity) &&
                selectedIds.Contains(entity.EntityId ?? ""))
            .ToArray();
        SetCommandSelection(nextSelection);
        return nextSelection.Length;
    }

    public void ClearCommandSelection()
    {
        SetCommandSelection(System.Array.Empty<BattleEntity>());
    }

    private void SetCommandSelection(IReadOnlyList<BattleEntity> nextSelection)
    {
        HashSet<BattleEntity> next = (nextSelection ?? System.Array.Empty<BattleEntity>())
            .Where(entity => entity != null && GodotObject.IsInstanceValid(entity))
            .ToHashSet();

        foreach (BattleEntity entity in _commandSelectedEntities.Where(entity => !next.Contains(entity)).ToArray())
        {
            entity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(false);
            _commandSelectedEntities.Remove(entity);
        }

        foreach (BattleEntity entity in next)
        {
            entity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(true);
            _commandSelectedEntities.Add(entity);
        }
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
        GridOccupantComponent gridOccupant,
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
            if (!TryResolveMovementGlobalPosition(gridOccupant, path[index], out Vector2 globalPosition))
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

    private bool TryResolveMovementGlobalPosition(
        GridOccupantComponent gridOccupant,
        GridSurfacePosition surfacePosition,
        out Vector2 globalPosition)
    {
        globalPosition = default;
        if (gridOccupant == null)
        {
            return false;
        }

        if (_tryResolveFootprintGlobalPosition?.Invoke(
                surfacePosition.Position,
                new Vector2I(gridOccupant.FootprintWidth, gridOccupant.FootprintHeight),
                out globalPosition) == true)
        {
            return true;
        }

        return false;
    }

    private void AnimateEntityMove(
        BattleEntity entity,
        IReadOnlyList<Vector2> globalPath,
        IReadOnlyList<GridSurfacePosition> surfacePath,
        bool returnToIdleOnComplete,
        double stepDurationSeconds)
    {
        if (!IsEntityAlive(entity))
        {
            return;
        }

        double resolvedStepDurationSeconds = ResolveMoveStepDurationSeconds(stepDurationSeconds);
        if (!IsInsideTree() || resolvedStepDurationSeconds <= 0)
        {
            entity.GlobalPosition = globalPath[^1];
            if (surfacePath?.Count > 0)
            {
                ApplyRenderSort(entity, surfacePath[^1]);
            }
            return;
        }

        if (!_movementLanes.TryGetValue(entity, out MovementLane lane))
        {
            lane = new MovementLane(entity.GlobalPosition, ResolveSurfaceAt(surfacePath, 0));
            _movementLanes[entity] = lane;
        }

        // Movement events are Runtime facts, but the visual path is actor-local
        // so consecutive grid steps can be stitched into one continuous stream.
        lane.Enqueue(globalPath, surfacePath, resolvedStepDurationSeconds, returnToIdleOnComplete);
        if (lane.HasSegments)
        {
            SetProcess(true);
        }
    }

    private bool AdvanceMovementLane(BattleEntity entity, MovementLane lane, double delta)
    {
        if (!IsInsideTree() || lane == null || !lane.HasSegments)
        {
            return false;
        }

        double remainingSeconds = delta;
        while (remainingSeconds > 0 && lane.TryGetCurrent(out MovementSegment segment))
        {
            if (lane.IsNewSegment)
            {
                ApplyRenderSort(entity, segment.FromSurface);
                FaceAlongSegment(entity.GetComponent<UnitAnimationComponent>(), segment.From, segment.To);
                lane.MarkSegmentApplied();
            }

            double segmentDuration = System.Math.Max(0.001, segment.DurationSeconds);
            double consumeSeconds = System.Math.Min(remainingSeconds, segmentDuration - lane.ElapsedSeconds);
            lane.ElapsedSeconds += consumeSeconds;
            remainingSeconds -= consumeSeconds;

            float progress = (float)Mathf.Clamp(lane.ElapsedSeconds / segmentDuration, 0, 1);
            entity.GlobalPosition = segment.From.Lerp(segment.To, progress);

            if (lane.ElapsedSeconds + 0.0001 < segmentDuration)
            {
                break;
            }

            entity.GlobalPosition = segment.To;
            ApplyRenderSort(entity, segment.ToSurface);
            lane.CompleteCurrentSegment();
        }

        if (lane.HasSegments)
        {
            return true;
        }

        if (lane.ReturnToIdleOnComplete)
        {
            // Fixed-clock RTS runtime can enqueue a continuation shortly after one segment drains.
            // This grace window keeps move animation stable instead of flashing idle between cells.
            _pendingMovementIdleSeconds[entity] = ResolveMovementIdleGraceSeconds();
        }

        return false;
    }

    private void UpdatePendingMovementIdle(double delta)
    {
        if (_pendingMovementIdleSeconds.Count == 0)
        {
            return;
        }

        foreach ((BattleEntity entity, double seconds) in _pendingMovementIdleSeconds.ToArray())
        {
            if (!IsEntityAlive(entity) || _movementLanes.ContainsKey(entity))
            {
                _pendingMovementIdleSeconds.Remove(entity);
                continue;
            }

            double remaining = seconds - delta;
            if (remaining > 0)
            {
                _pendingMovementIdleSeconds[entity] = remaining;
                continue;
            }

            _pendingMovementIdleSeconds.Remove(entity);
            entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        }
    }

    private double ResolveMovementIdleGraceSeconds()
    {
        return System.Math.Min(
            0.04,
            System.Math.Max(0.001, UnitMoveDuration * 0.35));
    }

    private sealed class MovementLane
    {
        private readonly Queue<MovementSegment> _segments = new();
        private MovementSegment _current;
        private bool _hasCurrent;
        private Vector2 _queuedEndPoint;
        private GridSurfacePosition _queuedEndSurface;

        public MovementLane(Vector2 startPoint, GridSurfacePosition startSurface)
        {
            _queuedEndPoint = startPoint;
            _queuedEndSurface = startSurface;
        }

        public double ElapsedSeconds { get; set; }
        public bool IsNewSegment { get; private set; }
        public bool ReturnToIdleOnComplete { get; private set; }
        public bool HasSegments => _hasCurrent || _segments.Count > 0;

        public void Enqueue(
            IReadOnlyList<Vector2> globalPath,
            IReadOnlyList<GridSurfacePosition> surfacePath,
            double stepDurationSeconds,
            bool returnToIdleOnComplete)
        {
            ReturnToIdleOnComplete = returnToIdleOnComplete;
            if (globalPath == null || globalPath.Count < 2)
            {
                return;
            }

            Vector2 from = _queuedEndPoint;
            GridSurfacePosition fromSurface = _queuedEndSurface;
            for (int index = 1; index < globalPath.Count; index++)
            {
                Vector2 to = globalPath[index];
                GridSurfacePosition toSurface = ResolveSurfaceAt(surfacePath, index);
                if (from.DistanceSquaredTo(to) <= 0.01f)
                {
                    from = to;
                    fromSurface = toSurface;
                    continue;
                }

                _segments.Enqueue(new MovementSegment(from, to, fromSurface, toSurface, stepDurationSeconds));
                from = to;
                fromSurface = toSurface;
            }

            _queuedEndPoint = from;
            _queuedEndSurface = fromSurface;
        }

        public bool TryGetCurrent(out MovementSegment segment)
        {
            if (_hasCurrent)
            {
                segment = _current;
                return true;
            }

            if (_segments.Count == 0)
            {
                segment = default;
                return false;
            }

            _current = _segments.Dequeue();
            _hasCurrent = true;
            IsNewSegment = true;
            ElapsedSeconds = 0;
            segment = _current;
            return true;
        }

        public void MarkSegmentApplied()
        {
            IsNewSegment = false;
        }

        public void CompleteCurrentSegment()
        {
            _hasCurrent = false;
            IsNewSegment = false;
            ElapsedSeconds = 0;
        }
    }

    private readonly record struct MovementSegment(
        Vector2 From,
        Vector2 To,
        GridSurfacePosition FromSurface,
        GridSurfacePosition ToSurface,
        double DurationSeconds);

    private double ResolveMoveStepDurationSeconds(double stepDurationSeconds)
    {
        return ResolveVisualMoveStepDurationSeconds(stepDurationSeconds);
    }

    public double ResolveVisualMoveStepDurationSeconds(double stepDurationSeconds)
    {
        double baseSeconds = stepDurationSeconds > 0 ? stepDurationSeconds : UnitMoveDuration;
        return System.Math.Max(0.01, baseSeconds);
    }

    private static bool IsEntityAlive(BattleEntity entity)
    {
        return entity != null && GodotObject.IsInstanceValid(entity);
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
