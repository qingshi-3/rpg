using System.Collections.Generic;
using Rpg.Presentation.Battle.Intents;

namespace Rpg.Presentation.Battle.AI;

public sealed class BattleAiFacadeCore
{
    public bool SelectBattleTarget(
        BattleAiDecisionFacts facts,
        string mode,
        IDictionary<string, object> blackboard,
        string targetVar)
    {
        if (!CanUseFacts(facts) || blackboard == null || string.IsNullOrWhiteSpace(targetVar))
        {
            return false;
        }

        string targetId = string.Equals(mode, "lowest_health_hostile", System.StringComparison.Ordinal)
            ? facts.LowestHealthHostileTargetId
            : facts.NearestHostileTargetId;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return false;
        }

        blackboard[targetVar] = targetId;
        blackboard["ability_id"] = facts.PrimaryAbilityId ?? "";
        blackboard["intent_power"] = System.Math.Max(0, facts.PrimaryAbilityPower);
        return true;
    }

    public bool CanStrikeBattleTarget(
        BattleAiDecisionFacts facts,
        IDictionary<string, object> blackboard,
        string targetVar,
        string abilityVar)
    {
        if (!CanUseFacts(facts) || !facts.HasPrimaryAbility || !facts.CanStrikeNow || blackboard == null)
        {
            return false;
        }

        return HasBlackboardValue(blackboard, targetVar) &&
               (string.IsNullOrWhiteSpace(abilityVar) || HasBlackboardValue(blackboard, abilityVar));
    }

    public BattleAiDecisionResult EmitBattleIntent(
        BattleAiDecisionFacts facts,
        string templateId,
        IDictionary<string, object> blackboard,
        string powerVar,
        string reason)
    {
        int power = ReadPower(blackboard, powerVar, facts?.PrimaryAbilityPower ?? 0);
        return new BattleAiDecisionResult(ResolveTemplate(templateId), power, reason);
    }

    internal static BattleIntentTemplate ResolveTemplate(string templateId)
    {
        return templateId switch
        {
            "melee_pressure" => BattleIntentTemplates.MeleePressure,
            "direct_strike" => BattleIntentTemplates.DirectStrike,
            "ranged_pressure" => BattleIntentTemplates.RangedPressure,
            "focus_pressure" => BattleIntentTemplates.FocusPressure,
            "focus_strike" => BattleIntentTemplates.FocusStrike,
            _ => BattleIntentTemplates.Hold
        };
    }

    private static bool CanUseFacts(BattleAiDecisionFacts facts)
    {
        return facts is { HasValidContext: true, ActorCanAct: true, HasTarget: true };
    }

    private static bool HasBlackboardValue(IDictionary<string, object> blackboard, string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               blackboard.TryGetValue(key, out object value) &&
               !string.IsNullOrWhiteSpace(value?.ToString());
    }

    private static int ReadPower(IDictionary<string, object> blackboard, string key, int fallback)
    {
        if (blackboard != null &&
            !string.IsNullOrWhiteSpace(key) &&
            blackboard.TryGetValue(key, out object value) &&
            int.TryParse(value?.ToString(), out int parsed))
        {
            return System.Math.Max(0, parsed);
        }

        return System.Math.Max(0, fallback);
    }
}
