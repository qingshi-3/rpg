using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeLivePresentationObserver
{
    private readonly System.Func<BattleUnitRoot> _resolveUnitRoot;
    private readonly System.Func<double, Task> _waitPresentationSeconds;
    private readonly System.Action<BattleEntity, bool> _focusBattleActionEntity;
    private readonly System.Action _queueBattlePerceptionOverlayRefresh;
    private readonly System.Action<long> _recordPresentationObserveElapsedTicks;

    public BattleRuntimeLivePresentationObserver(
        System.Func<BattleUnitRoot> resolveUnitRoot,
        System.Func<double, Task> waitPresentationSeconds,
        System.Action<BattleEntity, bool> focusBattleActionEntity,
        System.Action queueBattlePerceptionOverlayRefresh,
        System.Action<long> recordPresentationObserveElapsedTicks)
    {
        _resolveUnitRoot = resolveUnitRoot;
        _waitPresentationSeconds = waitPresentationSeconds;
        _focusBattleActionEntity = focusBattleActionEntity;
        _queueBattlePerceptionOverlayRefresh = queueBattlePerceptionOverlayRefresh;
        _recordPresentationObserveElapsedTicks = recordPresentationObserveElapsedTicks;
    }

    public Task ObserveAsync(
        IReadOnlyList<BattleEvent> events,
        BattleRuntimeLivePresentationState presentationState)
    {
        long observeStartedAt = Stopwatch.GetTimestamp();
        if (events == null || events.Count == 0 || presentationState == null)
        {
            _recordPresentationObserveElapsedTicks?.Invoke(Stopwatch.GetTimestamp() - observeStartedAt);
            return Task.CompletedTask;
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.SkillUsed))
        {
            presentationState.TrackActorAction(
                runtimeEvent.ActorId,
                () => ObserveRuntimeSkillUsedEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor),
                gateMovementStart: !BattleRuntimeThunderTagPresentationObserver.IsOffhandSkillReleaseEvent(runtimeEvent));
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.ThunderMarkCreated))
        {
            BattleRuntimeThunderTagPresentationObserver.ObserveRuntimeThunderMarkCreatedEvent(runtimeEvent, presentationState.EntitiesByRuntimeActor, UnitRoot);
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.ThunderMarkTeleported))
        {
            presentationState.ObserveActorTeleportNow(
                runtimeEvent.ActorId,
                () => BattleRuntimeTeleportPresentationObserver.ObserveRuntimeTeleportEvent(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor,
                    UnitRoot,
                    _queueBattlePerceptionOverlayRefresh));
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.MovementStarted))
        {
            presentationState.TrackActorMovement(
                runtimeEvent.ActorId,
                () => ObserveRuntimeMovementEvent(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor),
                _waitPresentationSeconds);
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.DamageApplied))
        {
            bool isSkillDamage = IsRuntimeSkillDamageEvent(runtimeEvent);
            BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic =
                BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic.TryCreate(runtimeEvent);
            if (isSkillDamage)
            {
                presentationState.TrackSkillDamageFeedback(
                    runtimeEvent.ActorId,
                    runtimeEvent.TargetId,
                    () => PlayRuntimeDamageFeedbackEventAsync(
                        runtimeEvent,
                        presentationState.EntitiesByRuntimeActor,
                        diagnostic));
            }
            else
            {
                presentationState.TrackActorDamage(
                    runtimeEvent.ActorId,
                    runtimeEvent.TargetId,
                    () => PlayRuntimeDamageFeedbackEventAsync(
                        runtimeEvent,
                        presentationState.EntitiesByRuntimeActor,
                        diagnostic));
            }

            presentationState.TrackTargetDamage(
                runtimeEvent.ActorId,
                runtimeEvent.TargetId,
                previousTargetDamageTail => ApplyRuntimeDamageEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor,
                    diagnostic,
                    previousTargetDamageTail),
                diagnostic);
        }

        _recordPresentationObserveElapsedTicks?.Invoke(Stopwatch.GetTimestamp() - observeStartedAt);
        return Task.CompletedTask;
    }

    public Dictionary<string, BattleEntity> BuildRuntimePlaybackEntityMap()
    {
        BattleUnitRoot unitRoot = UnitRoot;
        if (unitRoot == null)
        {
            return new Dictionary<string, BattleEntity>(System.StringComparer.Ordinal);
        }

        return unitRoot.GetEntitiesSnapshot()
            .Where(entity => entity != null && Godot.GodotObject.IsInstanceValid(entity))
            .GroupBy(entity => entity.EntityId)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);
    }

    private BattleUnitRoot UnitRoot => _resolveUnitRoot?.Invoke();

    private double ObserveRuntimeMovementEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        return ObserveRuntimeMovementEvent(
            runtimeEvent,
            entitiesByRuntimeActor,
            returnToIdleOnComplete: true);
    }

    private double ObserveRuntimeMovementEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        bool returnToIdleOnComplete)
    {
        BattleUnitRoot unitRoot = UnitRoot;
        if (unitRoot == null ||
            runtimeEvent == null ||
            entitiesByRuntimeActor == null ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !runtimeEvent.HasMovementCells)
        {
            return 0;
        }

        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return 0;
        }

        GridSurfacePosition nextStep = new(runtimeEvent.ToGridX, runtimeEvent.ToGridY, runtimeEvent.ToGridHeight);
        // Runtime emits one committed grid step per live tick. The movement lane
        // still holds briefly for a continuation, then closes to idle when no
        // committed follow-up step arrives; later attack/skill playback can
        // override idle through its own animation lane.
        double visualMoveSeconds = unitRoot.MoveEntityTo(
            actor,
            new[] { actorGrid.SurfacePosition, nextStep },
            restartMoveAnimation: false,
            returnToIdleOnComplete: returnToIdleOnComplete,
            stepDurationSeconds: runtimeEvent.ActionDurationSeconds);
        _queueBattlePerceptionOverlayRefresh?.Invoke();
        return visualMoveSeconds;
    }

    private async Task ObserveRuntimeSkillUsedEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        await ObserveRuntimeSkillUsedEventCoreAsync(runtimeEvent, entitiesByRuntimeActor);
    }

    private async Task<double> ObserveRuntimeSkillUsedEventCoreAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        BattleUnitRoot unitRoot = UnitRoot;
        if (unitRoot == null ||
            runtimeEvent == null ||
            entitiesByRuntimeActor == null ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor))
        {
            return 0;
        }

        entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target);
        if (BattleRuntimeThunderSpiralPresentationObserver.IsThunderSpiralSkillUsedEvent(runtimeEvent))
        {
            _focusBattleActionEntity?.Invoke(actor, true);
        }

        double castAnimationSeconds = unitRoot.PlaySkillCastPresentation(
            actor,
            target,
            runtimeEvent.ActionDurationSeconds,
            preserveMovement: BattleRuntimeThunderTagPresentationObserver.IsOffhandSkillReleaseEvent(runtimeEvent),
            sourceDefinitionId: runtimeEvent.SourceDefinitionId);
        if (BattleRuntimeThunderSpiralPresentationObserver.IsThunderSpiralSkillUsedEvent(runtimeEvent))
        {
            BattleRuntimeThunderSpiralPresentationObserver.ObserveRuntimeThunderSpiralSkillUsedEvent(
                runtimeEvent,
                actor,
                unitRoot);
        }

        double runtimeActionSeconds = runtimeEvent.ActionDurationSeconds > 0
            ? runtimeEvent.ActionDurationSeconds
            : castAnimationSeconds;
        double castPresentationSeconds = System.Math.Max(0.42, runtimeActionSeconds);
        await WaitPresentationSeconds(castPresentationSeconds);
        return castPresentationSeconds;
    }

    private async Task<double> PlayRuntimeDamageFeedbackEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic = null)
    {
        if (!TryResolveRuntimeDamageContext(
                runtimeEvent,
                entitiesByRuntimeActor,
                diagnostic,
                out BattleEntity actor,
                out BattleEntity target,
                out HealthComponent health,
                out int damage,
                out int targetHpBefore,
                out int targetHpAfter))
        {
            return 0;
        }

        int previewApplied = damage;
        bool previewDefeated = targetHpBefore > 0 && targetHpAfter <= 0;
        BattleDamageEvent damageEvent = BuildRuntimeDamageEvent(runtimeEvent, target, previewApplied, previewDefeated);
        bool isSkillDamage = IsRuntimeSkillDamageEvent(runtimeEvent);
        BattleUnitRoot unitRoot = UnitRoot;
        double attackAnimationSeconds = isSkillDamage
            ? unitRoot.PlayRuntimeDamageFeedback(actor, damageEvents: new[] { damageEvent }, playSkillImpactFx: true)
            : unitRoot.PlayActionResultAnimation(BattleActionResult.AttackSucceeded(
                actor,
                target,
                new[] { damageEvent },
                runtimeEvent?.ReasonCode ?? ""));
        diagnostic?.LogFeedbackStarted(
            targetHpBefore,
            previewApplied,
            previewDefeated,
            attackAnimationSeconds,
            isSkillDamage);

        // Runtime events are semantic combat facts. Basic attacks use the actor
        // action tail; skill-effect damage uses a separate target-aligned queue
        // because its caster action is already represented by SkillUsed.
        double runtimeActionSeconds = runtimeEvent.ActionDurationSeconds > 0
            ? runtimeEvent.ActionDurationSeconds
            : attackAnimationSeconds;
        double attackPresentationSeconds = System.Math.Max(0.42, runtimeActionSeconds);
        Task attackPresentationTask = WaitPresentationSeconds(attackPresentationSeconds);
        await attackPresentationTask;
        return attackPresentationSeconds;
    }

    private async Task ApplyRuntimeDamageEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic = null,
        Task previousTargetDamageTail = null)
    {
        if (!TryResolveRuntimeDamageContext(
                runtimeEvent,
                entitiesByRuntimeActor,
                diagnostic,
                out BattleEntity actor,
                out BattleEntity target,
                out HealthComponent health,
                out int damage,
                out int targetHpBefore,
                out int targetHpAfter))
        {
            return;
        }

        bool isSkillDamage = IsRuntimeSkillDamageEvent(runtimeEvent);
        double actionDurationSeconds = ResolveRuntimeDamageActionDurationSeconds(runtimeEvent, actor, isSkillDamage);
        // Health and death are semantic Runtime facts. They stay ordered per target
        // but do not wait for the attacker's full visual action backlog.
        await ApplyRuntimeDamageAtImpactAsync(
            actor,
            target,
            health,
            damage,
            targetHpBefore,
            targetHpAfter,
            actionDurationSeconds,
            0,
            diagnostic,
            previousTargetDamageTail,
            fallbackToActorAttackImpactDelay: false);
    }

    private bool TryResolveRuntimeDamageContext(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic,
        out BattleEntity actor,
        out BattleEntity target,
        out HealthComponent health,
        out int damage,
        out int targetHpBefore,
        out int targetHpAfter)
    {
        actor = null;
        target = null;
        health = null;
        damage = 0;
        targetHpBefore = 0;
        targetHpAfter = 0;

        if (UnitRoot == null)
        {
            diagnostic?.LogSkipped("missing_unit_root");
            return false;
        }

        if (runtimeEvent == null)
        {
            diagnostic?.LogSkipped("missing_runtime_event");
            return false;
        }

        if (entitiesByRuntimeActor == null)
        {
            diagnostic?.LogSkipped("missing_entity_map");
            return false;
        }

        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out actor))
        {
            diagnostic?.LogSkipped("missing_actor_entity");
            return false;
        }

        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out target))
        {
            diagnostic?.LogSkipped("missing_target_entity");
            return false;
        }

        health = target.GetComponent<HealthComponent>();
        damage = System.Math.Max(0, -runtimeEvent.CorpsStrengthDelta);
        if (!runtimeEvent.HasTargetHitPoints)
        {
            diagnostic?.LogSkipped("missing_runtime_target_hp");
            return false;
        }

        targetHpBefore = System.Math.Max(0, runtimeEvent.TargetHpBefore);
        targetHpAfter = System.Math.Max(0, runtimeEvent.TargetHpAfter);
        return true;
    }

    private static BattleDamageEvent BuildRuntimeDamageEvent(
        BattleEvent runtimeEvent,
        BattleEntity target,
        int damage,
        bool targetDefeated)
    {
        return new BattleDamageEvent(
            target,
            target?.EntityId ?? runtimeEvent?.TargetId ?? "",
            damage,
            targetDefeated,
            runtimeEvent?.SourceCommandId,
            runtimeEvent?.SourceActionId,
            runtimeEvent?.SourceDefinitionId,
            runtimeEvent?.EffectKind);
    }

    private static double ResolveRuntimeDamageActionDurationSeconds(
        BattleEvent runtimeEvent,
        BattleEntity actor,
        bool isSkillDamage)
    {
        if (runtimeEvent?.ActionDurationSeconds > 0)
        {
            return runtimeEvent.ActionDurationSeconds;
        }

        UnitAnimationComponent actorAnimation = actor?.GetComponent<UnitAnimationComponent>();
        return isSkillDamage
            ? actorAnimation?.ResolveSkillCastDurationSeconds() ?? 0
            : actorAnimation?.ResolveAttackDurationSeconds() ?? 0;
    }

    private static bool IsRuntimeSkillDamageEvent(BattleEvent runtimeEvent)
    {
        return runtimeEvent != null &&
               (!string.IsNullOrWhiteSpace(runtimeEvent.SourceCommandId) ||
                !string.IsNullOrWhiteSpace(runtimeEvent.SourceActionId) ||
                !string.IsNullOrWhiteSpace(runtimeEvent.SourceDefinitionId));
    }

    private static bool IsRuntimeDefeatingDamageEvent(BattleEvent runtimeEvent)
    {
        return !string.IsNullOrWhiteSpace(runtimeEvent?.ReasonCode) &&
               runtimeEvent.ReasonCode.Contains("defeated", System.StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplyRuntimeDamageAtImpactAsync(
        BattleEntity actor,
        BattleEntity target,
        HealthComponent health,
        int damage,
        int targetHpBefore,
        int targetHpAfter,
        double attackAnimationSeconds,
        double runtimeImpactDelaySeconds,
        BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic = null,
        Task previousTargetDamageTail = null,
        bool fallbackToActorAttackImpactDelay = true)
    {
        if (health == null || damage <= 0)
        {
            diagnostic?.LogSkipped(health == null ? "missing_health" : "non_positive_damage");
            return;
        }

        UnitAnimationComponent actorAnimation = actor.GetComponent<UnitAnimationComponent>();
        double impactDelaySeconds = runtimeImpactDelaySeconds > 0
            ? runtimeImpactDelaySeconds
            : fallbackToActorAttackImpactDelay
                ? System.Math.Max(0, actorAnimation?.ResolveAttackImpactDelaySeconds() ?? 0)
                : 0;
        double clampedImpactDelaySeconds = System.Math.Min(impactDelaySeconds, System.Math.Max(0, attackAnimationSeconds));
        diagnostic?.LogImpactDelayResolved(impactDelaySeconds, clampedImpactDelaySeconds, attackAnimationSeconds);
        if (clampedImpactDelaySeconds > 0)
        {
            await WaitPresentationSeconds(clampedImpactDelaySeconds);
        }

        if (previousTargetDamageTail != null)
        {
            await previousTargetDamageTail;
        }

        DamageReactionComponent damageReaction = target.GetComponent<DamageReactionComponent>();
        damageReaction?.BeginImpactAlignedDamageTiming();
        int applied;
        try
        {
            applied = health.MirrorRuntimeDamage(actor, damage, targetHpBefore, targetHpAfter);
        }
        finally
        {
            damageReaction?.EndImpactAlignedDamageTiming();
        }

        bool runtimeDefeated = targetHpBefore > 0 && targetHpAfter <= 0;
        diagnostic?.LogDamageApplied(applied, targetHpBefore, targetHpAfter, runtimeDefeated);
        if (applied <= 0)
        {
            return;
        }

        if (runtimeDefeated)
        {
            diagnostic?.LogMarkDefeatedRequested();
            UnitRoot?.MarkEntityDefeated(target);
            _queueBattlePerceptionOverlayRefresh?.Invoke();
        }
    }

    private Task WaitPresentationSeconds(double seconds)
    {
        return _waitPresentationSeconds?.Invoke(seconds) ?? Task.CompletedTask;
    }
}
