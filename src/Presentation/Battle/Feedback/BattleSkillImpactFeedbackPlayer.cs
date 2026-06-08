using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Feedback;

public static class BattleSkillImpactFeedbackPlayer
{
    public static void PlaySkillImpacts(IReadOnlyList<BattleDamageEvent> damageEvents, bool enabled)
    {
        if (!enabled || damageEvents == null)
        {
            return;
        }

        foreach (BattleDamageEvent damage in damageEvents.Where(damage =>
                     damage?.DamageApplied > 0 &&
                     damage.Target != null &&
                     GodotObject.IsInstanceValid(damage.Target)))
        {
            // Skill impact is target-side presentation; runtime damage facts stay unchanged.
            damage.Target.GetComponent<BattleSkillImpactFxComponent>()?.PlaySkillImpactFx();
        }
    }
}
