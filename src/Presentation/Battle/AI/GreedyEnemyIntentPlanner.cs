using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.AI;

public sealed class GreedyEnemyIntentPlanner : IEnemyIntentPlanner
{
    public BattleIntent ChooseIntent(BattleAiContext context, BattleEntity actor)
    {
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return BattleIntent.Hold(actor, "意图上下文无效");
        }

        if (!TryFindClosestHostileTarget(context, actor, out BattleEntity target))
        {
            return BattleIntent.Hold(actor, "没有可用目标");
        }

        AbilityDefinition ability = BattleAbilityQueries.GetPrimaryAbility(actor);
        int damage = GetDamageValue(ability);
        if (ability != null && ability.Range > 1)
        {
            return BattleIntentTemplates.RangedPressure.Create(actor, ability, damage);
        }

        GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
        if (ability != null &&
            targetGrid != null &&
            BattleAbilityQueries.IsValidTarget(
                context.GridMap,
                context.Entities,
                actor,
                target,
                targetGrid.Position,
                ability,
                null,
                out _))
        {
            return BattleIntentTemplates.DirectStrike.Create(actor, ability, damage);
        }

        MovementComponent movement = actor.GetComponent<MovementComponent>();
        int pressureValue = movement == null ? damage : movement.MoveRange;
        return BattleIntentTemplates.MeleePressure.Create(actor, ability, pressureValue);
    }

    private static bool TryFindClosestHostileTarget(
        BattleAiContext context,
        BattleEntity actor,
        out BattleEntity target)
    {
        target = null;
        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return false;
        }

        target = context.Entities
            .Where(entity => !BattleRuleQueries.IsDefeated(entity) && BattleRuleQueries.AreHostile(actor, entity))
            .Where(entity => entity.GetComponent<GridOccupantComponent>() != null)
            .OrderBy(entity => BattleRuleQueries.GetManhattanDistance(
                actorGrid.Position,
                entity.GetComponent<GridOccupantComponent>().Position))
            .FirstOrDefault();

        return target != null;
    }

    private static int GetDamageValue(AbilityDefinition ability)
    {
        if (ability?.Effects == null)
        {
            return 0;
        }

        return ability.Effects
            .OfType<DamageAbilityEffect>()
            .Sum(effect => System.Math.Max(0, effect.Damage));
    }
}
