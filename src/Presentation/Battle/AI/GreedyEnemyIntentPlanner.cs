using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.AI;

public sealed class GreedyEnemyIntentPlanner : IEnemyIntentPlanner
{
    private readonly BattleAiDecisionTreeRunner _decisionRunner;

    public GreedyEnemyIntentPlanner(BattleAiDecisionTreeRunner decisionRunner = null)
    {
        _decisionRunner = decisionRunner ?? new BattleAiDecisionTreeRunner();
    }

    public BattleIntent ChooseIntent(BattleAiContext context, BattleEntity actor)
    {
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return _decisionRunner
                .ChooseEnemyIntent(new BattleAiDecisionFacts { HasValidContext = context?.GridMap != null, ActorCanAct = false })
                .ToIntent(actor, null);
        }

        if (!TryFindClosestHostileTarget(context, actor, out BattleEntity target))
        {
            return _decisionRunner
                .ChooseEnemyIntent(new BattleAiDecisionFacts { HasValidContext = true, ActorCanAct = true, HasTarget = false })
                .ToIntent(actor, null);
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

        MovementComponent movement = actor.GetComponent<MovementComponent>();
        BattleAiDecisionResult decision = _decisionRunner.ChooseEnemyIntent(new BattleAiDecisionFacts
        {
            HasValidContext = true,
            ActorCanAct = true,
            HasTarget = true,
            HasPrimaryAbility = ability != null,
            PrimaryAbilityId = BattleAbilityQueries.ToCommandId(ability),
            PrimaryAbilityRange = ability?.Range ?? 0,
            PrimaryAbilityPower = damage,
            CanStrikeNow = canStrikeNow,
            MoveRange = movement?.MoveRange,
            NearestHostileTargetId = target.EntityId
        });
        return decision.ToIntent(actor, ability);
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
