using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.AI;

public sealed class GreedyAlliedIntentPlanner
{
    public BattleIntent ChooseIntent(
        BattleAiContext context,
        BattleEntity actor,
        BattleCorpsCommand corpsCommand)
    {
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return BattleIntent.Hold(actor, "友军自动行动上下文无效");
        }

        if (!TryFindTarget(context, actor, corpsCommand, out BattleEntity target))
        {
            return BattleIntent.Hold(actor, "当前没有可攻击目标");
        }

        AbilityDefinition ability = BattleAbilityQueries.GetPrimaryAbility(actor);
        int damage = GetDamageValue(ability);
        GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
        bool canStrikeNow = ability != null &&
                            targetGrid != null &&
                            BattleAbilityQueries.IsValidTarget(
                                context.GridMap,
                                context.Entities,
                                actor,
                                target,
                                targetGrid.Position,
                                ability,
                                null,
                                out _);

        if (corpsCommand == BattleCorpsCommand.HoldLine)
        {
            return canStrikeNow
                ? BattleIntentTemplates.DirectStrike.Create(actor, ability, damage)
                : BattleIntent.Hold(actor, "执行坚守，保持阵线");
        }

        if (corpsCommand == BattleCorpsCommand.FocusFire)
        {
            return canStrikeNow
                ? BattleIntentTemplates.FocusStrike.Create(actor, ability, damage)
                : BattleIntentTemplates.FocusPressure.Create(actor, ability, damage);
        }

        if (ability != null && ability.Range > 1)
        {
            return BattleIntentTemplates.RangedPressure.Create(actor, ability, damage);
        }

        return canStrikeNow
            ? BattleIntentTemplates.DirectStrike.Create(actor, ability, damage)
            : BattleIntentTemplates.MeleePressure.Create(actor, ability, damage);
    }

    private static bool TryFindTarget(
        BattleAiContext context,
        BattleEntity actor,
        BattleCorpsCommand corpsCommand,
        out BattleEntity target)
    {
        target = null;
        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return false;
        }

        var candidates = context.Entities
            .Where(entity => !BattleRuleQueries.IsDefeated(entity) && BattleRuleQueries.AreHostile(actor, entity))
            .Where(entity => entity.GetComponent<GridOccupantComponent>() != null);

        target = corpsCommand == BattleCorpsCommand.FocusFire
            ? candidates
                .OrderBy(entity => entity.GetComponent<HealthComponent>()?.Hp ?? int.MaxValue)
                .ThenBy(entity => BattleRuleQueries.GetManhattanDistance(
                    actorGrid.Position,
                    entity.GetComponent<GridOccupantComponent>().Position))
                .FirstOrDefault()
            : candidates
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
