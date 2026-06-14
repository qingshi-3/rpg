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

    private readonly Dictionary<BattleEntity, BattleIntentMarker> _intentMarkers = new();
    private readonly Dictionary<BattleEntity, BattleActionCue> _actionCues = new();
    private readonly HashSet<BattleEntity> _commandSelectedEntities = new();
    private readonly HashSet<BattleEntity> _attackTargetPreviewedEntities = new();
    private readonly HashSet<BattleEntity> _defeatedEntities = new();
    private readonly HashSet<BattleEntity> _pendingDefeatedPresentations = new();
    private readonly List<TaskCompletionSource<bool>> _defeatedPresentationWaiters = new();
    private BattleUnitHitFeedbackPresenter _hitFeedbackPresenter;
    private TryResolveCellGlobalPosition _tryResolveCellGlobalPosition;
    private TryResolveFootprintGlobalPosition _tryResolveFootprintGlobalPosition;
    private ApplyEntityRenderSort _applyEntityRenderSort;
    private bool _battlePresentationPaused;

    public bool HasPendingDefeatedPresentations => _pendingDefeatedPresentations.Count > 0;

    private BattleUnitHitFeedbackPresenter HitFeedbackPresenter =>
        _hitFeedbackPresenter ??= new BattleUnitHitFeedbackPresenter(this, () => DamageNumberGlobalOffset, WaitSeconds);

    public override void _Ready()
    {
        ActionCueScene ??= GD.Load<PackedScene>("res://scenes/battle/feedback/BattleActionCue.tscn");
        GameLog.Info(nameof(BattleUnitRoot), $"Ready path={GetPath()} entities={GetEntitiesSnapshot().Count}");
    }

    public override void _ExitTree()
    {
        ClearMovementPresentationState();
        ClearAllActionCues();
        ClearCommandSelection();
        ClearAttackTargetPreview();
        _hitFeedbackPresenter?.ClearHitOutlines();
        ClearThunderMarkPresentations();
        _pendingDefeatedPresentations.Clear();
        CompleteDefeatedPresentationWaiters();
    }

    public void SetBattlePresentationPaused(bool paused)
    {
        _battlePresentationPaused = paused;
        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (entity == null || !GodotObject.IsInstanceValid(entity))
            {
                continue;
            }

            entity.GetComponent<UnitAnimationComponent>()?.SetPresentationPaused(paused);
        }

        // Runtime can stay paused while HUD/input remain live; unit visuals own a
        // separate freeze so queued movement does not consume lane time or replay.
        if (!paused && (_movementLanes.Count > 0 || _pendingMovementIdleSeconds.Count > 0))
        {
            SetProcess(true);
        }
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
        BattleEntity actor = result.Actor;
        UnitAnimationComponent actorAnimation = actor?.GetComponent<UnitAnimationComponent>();
        if (result.Target != null)
        {
            actorAnimation?.FaceToward(result.Target.GlobalPosition);
        }

        double actionDurationSeconds;
        if (result.Kind == BattleActionKind.Ability)
        {
            StopEntityMovement(actor, snapToLogicalGrid: true);
            actionDurationSeconds = actorAnimation?.ResolveSkillCastDurationSeconds() ?? 0;
            actorAnimation?.PlaySkillCast();
            if (actor != null)
            {
                actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx(actionDurationSeconds);
            }
        }
        else
        {
            actionDurationSeconds = actorAnimation?.ResolveAttackDurationSeconds() ?? 0;
            actorAnimation?.PlayAttack();
        }
        actor?.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        _ = HitFeedbackPresenter.PlayAsync(actor, damageEvents, result.Kind == BattleActionKind.Ability);
        return actionDurationSeconds;
    }

    private void StopEntityMovement(BattleEntity entity, bool snapToLogicalGrid)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        bool hadMovement = _movementLanes.Remove(entity);
        bool hadPendingIdle = _pendingMovementIdleSeconds.Remove(entity);
        if (!snapToLogicalGrid)
        {
            return;
        }

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        // Skill casting is an anchored action. If a visual movement lane is
        // still catching up to Runtime's grid state, finish that presentation
        // boundary before the cast FX starts so the unit does not drift mid-cast.
        if (TryResolveMovementGlobalPosition(gridOccupant, gridOccupant.SurfacePosition, out Vector2 globalPosition))
        {
            entity.GlobalPosition = globalPosition;
            ApplyRenderSort(entity, gridOccupant.SurfacePosition);
            if (hadMovement || hadPendingIdle)
            {
                entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
            }
        }
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

    private async Task WaitSeconds(double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(seconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
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
        // Runtime has ended or left battle presentation; queued visual movement
        // lanes must stop before survivors settle, or move loops can outlive combat.
        ClearMovementPresentationState();

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

    public int SetAttackTargetPreviewByEntityId(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            ClearAttackTargetPreview();
            return 0;
        }

        BattleEntity[] nextPreview = GetEntitiesSnapshot()
            .Where(entity => entity != null &&
                             !BattleRuleQueries.IsDefeated(entity) &&
                             string.Equals(entity.EntityId ?? "", entityId, System.StringComparison.Ordinal))
            .Take(1)
            .ToArray();
        SetAttackTargetPreview(nextPreview);
        return nextPreview.Length;
    }

    public void ClearAttackTargetPreview()
    {
        SetAttackTargetPreview(System.Array.Empty<BattleEntity>());
    }

    private void SetAttackTargetPreview(IReadOnlyList<BattleEntity> nextPreview)
    {
        HashSet<BattleEntity> next = (nextPreview ?? System.Array.Empty<BattleEntity>())
            .Where(entity => entity != null && GodotObject.IsInstanceValid(entity))
            .ToHashSet();

        foreach (BattleEntity entity in _attackTargetPreviewedEntities.Where(entity => !next.Contains(entity)).ToArray())
        {
            entity.GetComponent<BattleUnitPresentationComponent>()?.SetAttackTargetPreview(false);
            _attackTargetPreviewedEntities.Remove(entity);
        }

        foreach (BattleEntity entity in next)
        {
            entity.GetComponent<BattleUnitPresentationComponent>()?.SetAttackTargetPreview(true);
            _attackTargetPreviewedEntities.Add(entity);
        }
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

        entity.DebugMarkerColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        entity.QueueRedraw();
        HideHealthBarImmediately(entity);

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

    private static void HideHealthBarImmediately(BattleEntity entity)
    {
        entity?.GetComponent<BattleUnitHealthBarComponent>()?.HideImmediately();
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

    private void ApplyRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition)
    {
        _applyEntityRenderSort?.Invoke(entity, surfacePosition);
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
