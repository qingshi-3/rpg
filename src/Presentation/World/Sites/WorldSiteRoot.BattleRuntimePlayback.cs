using System.Collections.Generic;
using System.Threading.Tasks;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private double ObserveRuntimeMovementEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        return ObserveRuntimeMovementEvent(
            runtimeEvent,
            entitiesByRuntimeActor,
            returnToIdleOnComplete: false);
    }

    private double ObserveRuntimeMovementEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        bool returnToIdleOnComplete)
    {
        if (_unitRoot == null ||
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
        // Runtime emits one committed grid step per live tick. Presentation keeps
        // the move loop open while later ticks may retarget the same actor.
        _unitRoot.MoveEntityTo(
            actor,
            new[] { actorGrid.SurfacePosition, nextStep },
            restartMoveAnimation: false,
            returnToIdleOnComplete: returnToIdleOnComplete,
            stepDurationSeconds: runtimeEvent.ActionDurationSeconds);
        RefreshBattlePerceptionOverlay();
        return _unitRoot.ResolveVisualMoveStepDurationSeconds(runtimeEvent.ActionDurationSeconds);
    }

    private async Task ObserveRuntimeDamageEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        await ObserveRuntimeDamageEventCoreAsync(runtimeEvent, entitiesByRuntimeActor);
    }

    private async Task<double> ObserveRuntimeDamageEventCoreAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        if (_unitRoot == null ||
            runtimeEvent == null ||
            entitiesByRuntimeActor == null ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target))
        {
            return 0;
        }

        int damage = System.Math.Max(0, -runtimeEvent.CorpsStrengthDelta);
        HealthComponent health = target.GetComponent<HealthComponent>();
        int targetHpBeforeHit = health?.Hp ?? 0;
        int previewApplied = health == null
            ? damage
            : System.Math.Min(System.Math.Max(0, targetHpBeforeHit), damage);
        bool previewDefeated = health != null && targetHpBeforeHit > 0 && targetHpBeforeHit - previewApplied <= 0;
        BattleDamageEvent damageEvent = new(
            target,
            target.EntityId ?? "",
            previewApplied,
            previewDefeated,
            runtimeEvent.SourceCommandId,
            runtimeEvent.SourceActionId,
            runtimeEvent.SourceDefinitionId,
            runtimeEvent.EffectKind);
        double attackAnimationSeconds = _unitRoot.PlayActionResultAnimation(BuildRuntimeDamageActionResult(
            runtimeEvent,
            actor,
            target,
            damageEvent));

        // Runtime remains authoritative for outcome, but presentation health and
        // defeat feedback must land at attack impact, not at attack start.
        Task impactDamageTask = ApplyRuntimeDamageAtImpactAsync(
            actor,
            target,
            health,
            damage,
            attackAnimationSeconds,
            runtimeEvent.ActionImpactDelaySeconds);

        // Runtime events are semantic combat facts. Live presentation waits for
        // this actor's own attack task in the background, without blocking the
        // shared simulation clock or unrelated units.
        double runtimeActionSeconds = runtimeEvent.ActionDurationSeconds > 0
            ? runtimeEvent.ActionDurationSeconds
            : attackAnimationSeconds;
        double attackPresentationSeconds = System.Math.Max(0.42, runtimeActionSeconds);
        Task attackPresentationTask = WaitSiteBattlePresentationSeconds(attackPresentationSeconds);
        await impactDamageTask;
        await attackPresentationTask;
        return attackPresentationSeconds;
    }

    private static BattleActionResult BuildRuntimeDamageActionResult(
        BattleEvent runtimeEvent,
        BattleEntity actor,
        BattleEntity target,
        BattleDamageEvent damageEvent)
    {
        BattleDamageEvent[] damageEvents = { damageEvent };
        return IsRuntimeSkillDamageEvent(runtimeEvent)
            ? BattleActionResult.AbilitySucceeded(actor, target, null, damageEvents, runtimeEvent?.ReasonCode ?? "")
            : BattleActionResult.AttackSucceeded(actor, target, damageEvents, runtimeEvent?.ReasonCode ?? "");
    }

    private static bool IsRuntimeSkillDamageEvent(BattleEvent runtimeEvent)
    {
        return runtimeEvent != null &&
               (!string.IsNullOrWhiteSpace(runtimeEvent.SourceCommandId) ||
                !string.IsNullOrWhiteSpace(runtimeEvent.SourceActionId) ||
                !string.IsNullOrWhiteSpace(runtimeEvent.SourceDefinitionId));
    }

    private async Task ApplyRuntimeDamageAtImpactAsync(
        BattleEntity actor,
        BattleEntity target,
        HealthComponent health,
        int damage,
        double attackAnimationSeconds,
        double runtimeImpactDelaySeconds)
    {
        if (health == null || damage <= 0)
        {
            return;
        }

        UnitAnimationComponent actorAnimation = actor.GetComponent<UnitAnimationComponent>();
        double impactDelaySeconds = runtimeImpactDelaySeconds > 0
            ? runtimeImpactDelaySeconds
            : System.Math.Max(0, actorAnimation?.ResolveAttackImpactDelaySeconds() ?? 0);
        double clampedImpactDelaySeconds = System.Math.Min(impactDelaySeconds, System.Math.Max(0, attackAnimationSeconds));
        if (clampedImpactDelaySeconds > 0)
        {
            await WaitSiteBattlePresentationSeconds(clampedImpactDelaySeconds);
        }

        DamageReactionComponent damageReaction = target.GetComponent<DamageReactionComponent>();
        damageReaction?.BeginImpactAlignedDamageTiming();
        int applied;
        try
        {
            applied = health.ApplyDamage(damage, actor);
        }
        finally
        {
            damageReaction?.EndImpactAlignedDamageTiming();
        }

        if (applied <= 0)
        {
            return;
        }

        if (BattleRuleQueries.IsDefeated(target))
        {
            _unitRoot.MarkEntityDefeated(target);
            RefreshBattlePerceptionOverlay();
        }
    }
}
