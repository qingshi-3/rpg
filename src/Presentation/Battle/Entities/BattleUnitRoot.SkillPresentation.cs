using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Feedback;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitRoot
{
    public double PlaySkillCastPresentation(BattleEntity actor, BattleEntity target, double durationSeconds = 0)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor))
        {
            return 0;
        }

        StopEntityMovement(actor, snapToLogicalGrid: true);
        UnitAnimationComponent actorAnimation = actor.GetComponent<UnitAnimationComponent>();
        if (target != null && GodotObject.IsInstanceValid(target))
        {
            actorAnimation?.FaceToward(target.GlobalPosition);
        }

        double actionDurationSeconds = durationSeconds > 0
            ? durationSeconds
            : actorAnimation?.ResolveSkillCastDurationSeconds() ?? 0;
        actorAnimation?.PlaySkillCast();
        actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx(actionDurationSeconds);
        actor.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        return actionDurationSeconds;
    }

    public double PlayRuntimeDamageFeedback(
        BattleEntity actor,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        bool playSkillImpactFx)
    {
        BattleDamageEvent[] resolvedDamageEvents = damageEvents?.ToArray() ?? System.Array.Empty<BattleDamageEvent>();
        BattleHitFeedbackPlan feedbackPlan = BattleHitFeedbackPlanner.Build(resolvedDamageEvents);
        BattleEntity[] hitTargets = ResolveHitFeedbackTargets(resolvedDamageEvents, feedbackPlan).ToArray();
        _ = PlayHitFeedbackAsync(
            actor,
            resolvedDamageEvents,
            hitTargets,
            playSkillImpactFx,
            impactDelaySecondsOverride: 0);
        return 0;
    }
}
