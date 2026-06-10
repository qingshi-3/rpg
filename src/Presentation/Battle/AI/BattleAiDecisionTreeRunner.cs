using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Intents;

namespace Rpg.Presentation.Battle.AI;

// First migration slice for LimboAI: keep intent selection in a tree-shaped
// decision layer while Runtime remains the action and combat authority.
public sealed class BattleAiDecisionTreeRunner
{
    public BattleAiDecisionResult ChooseEnemyIntent(BattleAiDecisionFacts facts)
    {
        if (!CanDecide(facts))
        {
            return Hold("意图上下文无效");
        }

        if (!facts.HasTarget)
        {
            return Hold("没有可用目标");
        }

        if (facts.HasPrimaryAbility && facts.PrimaryAbilityRange > 1)
        {
            return Create(BattleIntentTemplates.RangedPressure, facts.PrimaryAbilityPower);
        }

        if (facts.HasPrimaryAbility && facts.CanStrikeNow)
        {
            return Create(BattleIntentTemplates.DirectStrike, facts.PrimaryAbilityPower);
        }

        int pressureValue = facts.MoveRange.HasValue
            ? facts.MoveRange.Value
            : facts.PrimaryAbilityPower;
        return Create(BattleIntentTemplates.MeleePressure, pressureValue);
    }

    public BattleAiDecisionResult ChooseAlliedIntent(BattleAiDecisionFacts facts, BattleCorpsCommand corpsCommand)
    {
        if (!CanDecide(facts))
        {
            return Hold("友军自动行动上下文无效");
        }

        if (!facts.HasTarget)
        {
            return Hold("当前没有可攻击目标");
        }

        if (corpsCommand == BattleCorpsCommand.HoldLine)
        {
            return facts.CanStrikeNow
                ? Create(BattleIntentTemplates.DirectStrike, facts.PrimaryAbilityPower)
                : Hold("执行坚守，保持阵线");
        }

        if (corpsCommand == BattleCorpsCommand.FocusFire)
        {
            return facts.CanStrikeNow
                ? Create(BattleIntentTemplates.FocusStrike, facts.PrimaryAbilityPower)
                : Create(BattleIntentTemplates.FocusPressure, facts.PrimaryAbilityPower);
        }

        if (facts.HasPrimaryAbility && facts.PrimaryAbilityRange > 1)
        {
            return Create(BattleIntentTemplates.RangedPressure, facts.PrimaryAbilityPower);
        }

        return facts.CanStrikeNow
            ? Create(BattleIntentTemplates.DirectStrike, facts.PrimaryAbilityPower)
            : Create(BattleIntentTemplates.MeleePressure, facts.PrimaryAbilityPower);
    }

    private static bool CanDecide(BattleAiDecisionFacts facts)
    {
        return facts is { HasValidContext: true, ActorCanAct: true };
    }

    private static BattleAiDecisionResult Create(BattleIntentTemplate template, int power)
    {
        return new BattleAiDecisionResult(template, power);
    }

    private static BattleAiDecisionResult Hold(string reason)
    {
        return new BattleAiDecisionResult(BattleIntentTemplates.Hold, 0, reason);
    }
}
