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
    private readonly BattleAiDecisionTreeRunner _decisionRunner;

    public GreedyAlliedIntentPlanner(BattleAiDecisionTreeRunner decisionRunner = null)
    {
        _decisionRunner = decisionRunner ?? new BattleAiDecisionTreeRunner();
    }

    public BattleIntent ChooseIntent(
        BattleAiContext context,
        BattleEntity actor,
        BattleCorpsCommand corpsCommand)
    {
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return _decisionRunner
                .ChooseAlliedIntent(new BattleAiDecisionFacts { HasValidContext = context?.GridMap != null, ActorCanAct = false }, corpsCommand)
                .ToIntent(actor, null);
        }

        if (!TryFindTarget(context, actor, corpsCommand, out BattleEntity target))
        {
            return _decisionRunner
                .ChooseAlliedIntent(new BattleAiDecisionFacts { HasValidContext = true, ActorCanAct = true, HasTarget = false }, corpsCommand)
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

        BattleAiDecisionResult decision = _decisionRunner.ChooseAlliedIntent(new BattleAiDecisionFacts
        {
            HasValidContext = true,
            ActorCanAct = true,
            HasTarget = true,
            HasPrimaryAbility = ability != null,
            PrimaryAbilityId = BattleAbilityQueries.ToCommandId(ability),
            PrimaryAbilityRange = ability?.Range ?? 0,
            PrimaryAbilityPower = damage,
            CanStrikeNow = canStrikeNow,
            MoveRange = actor.GetComponent<MovementComponent>()?.MoveRange,
            NearestHostileTargetId = target.EntityId,
            LowestHealthHostileTargetId = corpsCommand == BattleCorpsCommand.FocusFire ? target.EntityId : ""
        }, corpsCommand);
        return decision.ToIntent(actor, ability);
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
