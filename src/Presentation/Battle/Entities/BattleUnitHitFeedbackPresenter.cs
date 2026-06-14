using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.Entities;

internal sealed class BattleUnitHitFeedbackPresenter
{
    private readonly Node _host;
    private readonly Func<Vector2> _resolveDamageNumberGlobalOffset;
    private readonly Func<double, Task> _waitSeconds;
    private readonly HashSet<BattleEntity> _hitOutlinedEntities = new();

    public BattleUnitHitFeedbackPresenter(
        Node host,
        Func<Vector2> resolveDamageNumberGlobalOffset,
        Func<double, Task> waitSeconds)
    {
        _host = host;
        _resolveDamageNumberGlobalOffset = resolveDamageNumberGlobalOffset;
        _waitSeconds = waitSeconds;
    }

    public Task PlayAsync(
        BattleEntity actor,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        bool playSkillImpactFx,
        double? impactDelaySecondsOverride = null)
    {
        BattleDamageEvent[] resolvedDamageEvents = damageEvents?.ToArray() ?? Array.Empty<BattleDamageEvent>();
        BattleHitFeedbackPlan feedbackPlan = BattleHitFeedbackPlanner.Build(resolvedDamageEvents);
        BattleEntity[] hitTargets = ResolveHitFeedbackTargets(resolvedDamageEvents, feedbackPlan).ToArray();
        return PlayResolvedAsync(actor, resolvedDamageEvents, hitTargets, playSkillImpactFx, impactDelaySecondsOverride);
    }

    public void ClearHitOutlines()
    {
        SetHitOutlines(_hitOutlinedEntities.ToArray(), visible: false);
    }

    private async Task PlayResolvedAsync(
        BattleEntity actor,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        IReadOnlyList<BattleEntity> outlinedTargets,
        bool playSkillImpactFx,
        double? impactDelaySecondsOverride)
    {
        UnitAnimationComponent actorAnimation = actor?.GetComponent<UnitAnimationComponent>();
        double impactDelaySeconds = impactDelaySecondsOverride ?? actorAnimation?.ResolveAttackImpactDelaySeconds() ?? 0;

        if (_waitSeconds != null)
        {
            await _waitSeconds(impactDelaySeconds);
        }

        actor?.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.AttackImpact);
        BattleSkillImpactFeedbackPlayer.PlaySkillImpacts(damageEvents, playSkillImpactFx);
        PlayHitOutlinePulses(outlinedTargets);
        SpawnDamageNumbers(damageEvents);
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

    private static void PlayHitOutlinePulses(IReadOnlyList<BattleEntity> targets)
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

            target.GetComponent<BattleUnitPresentationComponent>()?.PlayHitOutlinePulse();
        }
    }

    private void SpawnDamageNumbers(IReadOnlyList<BattleDamageEvent> damageEvents)
    {
        if (damageEvents == null || _host == null || !GodotObject.IsInstanceValid(_host))
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

            _host.AddChild(number);
            number.GlobalPosition = damage.Target.GlobalPosition + ResolveDamageNumberGlobalOffset();
            number.Play($"-{damage.DamageApplied}");
        }
    }

    private Vector2 ResolveDamageNumberGlobalOffset()
    {
        return _resolveDamageNumberGlobalOffset?.Invoke() ?? BattleDamageNumberMotionSpec.Default.SpawnOffset;
    }
}
