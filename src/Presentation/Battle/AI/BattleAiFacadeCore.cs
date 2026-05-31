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
        WriteLocalCombatObservations(facts, blackboard);
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
        if (blackboard != null)
        {
            WriteLocalCombatObservations(facts, blackboard);
        }

        return new BattleAiDecisionResult(ResolveTemplate(templateId), power, reason, facts);
    }

    private static void WriteLocalCombatObservations(BattleAiDecisionFacts facts, IDictionary<string, object> blackboard)
    {
        if (facts?.HasLocalCombatObservation != true || blackboard == null)
        {
            return;
        }

        // Facade output is observation-only: Runtime owns region creation,
        // validation, and mutation. Presentation/LimboAI only receive facts.
        blackboard["local_combat_owner_group_id"] = facts.LocalCombatOwnerBattleGroupId ?? "";
        blackboard["local_combat_region_id"] = facts.LocalCombatRegionId ?? "";
        blackboard["local_combat_target_id"] = facts.LocalCombatTargetActorId ?? "";
        blackboard["local_combat_center_x"] = facts.LocalCombatCenterCellX;
        blackboard["local_combat_center_y"] = facts.LocalCombatCenterCellY;
        blackboard["local_combat_center_height"] = facts.LocalCombatCenterCellHeight;
        blackboard["local_combat_width"] = System.Math.Max(1, facts.LocalCombatWidth);
        blackboard["local_combat_height"] = System.Math.Max(1, facts.LocalCombatHeight);
        blackboard["local_combat_version"] = facts.LocalCombatVersion;
        blackboard["local_combat_slot_kind"] = facts.LocalCombatSelectedSlotKind ?? "";
        blackboard["local_combat_slot_role"] = facts.LocalCombatSelectedSlotRole ?? "";
        blackboard["local_combat_slot_x"] = facts.LocalCombatSelectedSlotCellX;
        blackboard["local_combat_slot_y"] = facts.LocalCombatSelectedSlotCellY;
        blackboard["local_combat_slot_height"] = facts.LocalCombatSelectedSlotCellHeight;
        blackboard["local_combat_reason_code"] = facts.LocalCombatReasonCode ?? "";
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
