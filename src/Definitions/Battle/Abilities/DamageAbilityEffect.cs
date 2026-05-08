using Godot;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Definitions.Battle.Abilities;

[GlobalClass]
public partial class DamageAbilityEffect : AbilityEffect
{
    [Export]
    public int Damage { get; set; } = 1;

    public override AbilityEffectResult Apply(AbilityUseContext context)
    {
        if (context?.Target == null)
        {
            return AbilityEffectResult.None;
        }

        HealthComponent health = context.Target.GetComponent<HealthComponent>();
        if (health == null)
        {
            return AbilityEffectResult.None;
        }

        int damageApplied = health.ApplyDamage(Damage, context.Actor);
        bool defeated = health.IsDead;
        if (defeated)
        {
            context.MarkEntityDefeated?.Invoke(context.Target);
        }

        return new AbilityEffectResult(damageApplied, defeated);
    }
}
