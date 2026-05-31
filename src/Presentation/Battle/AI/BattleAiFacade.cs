using Godot;

namespace Rpg.Presentation.Battle.AI;

public partial class BattleAiFacade : Node
{
    private readonly BattleAiFacadeCore _core = new();
    private BattleAiDecisionFacts _facts = new();

    public BattleAiDecisionResult LastDecision { get; private set; }

    public void Configure(BattleAiDecisionFacts facts)
    {
        _facts = facts ?? new BattleAiDecisionFacts();
        LastDecision = null;
    }

    public bool has_battle_command(string expectedCommand, GodotObject blackboard)
    {
        if (_facts is not { HasValidContext: true, ActorCanAct: true } ||
            string.IsNullOrWhiteSpace(expectedCommand))
        {
            return false;
        }

        string command = !string.IsNullOrWhiteSpace(_facts.Command)
            ? _facts.Command
            : ReadBlackboardString(blackboard, new StringName("command"), "");
        return string.Equals(command, expectedCommand, System.StringComparison.Ordinal);
    }

    public bool has_ranged_battle_ability(StringName abilityVar, GodotObject blackboard)
    {
        return CanUseFacts() &&
               _facts.HasPrimaryAbility &&
               _facts.PrimaryAbilityRange > 1 &&
               (string.IsNullOrWhiteSpace(abilityVar.ToString()) || HasBlackboardValue(blackboard, abilityVar));
    }

    // LimboAI custom tasks call this snake_case method from GDScript. It writes
    // only blackboard decision facts; C# planners and resolvers still own action truth.
    public bool select_battle_target(string mode, StringName targetVar, GodotObject blackboard)
    {
        if (!CanUseFacts() || blackboard == null || string.IsNullOrWhiteSpace(targetVar.ToString()))
        {
            return false;
        }

        string targetId = string.Equals(mode, "lowest_health_hostile", System.StringComparison.Ordinal)
            ? _facts.LowestHealthHostileTargetId
            : _facts.NearestHostileTargetId;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return false;
        }

        SetBlackboardString(blackboard, targetVar, targetId);
        SetBlackboardString(blackboard, new StringName("ability_id"), _facts.PrimaryAbilityId ?? "");
        SetBlackboardInt(blackboard, new StringName("intent_power"), _facts.PrimaryAbilityPower);
        WriteLocalCombatObservations(_facts, blackboard);
        return true;
    }

    public bool can_strike_battle_target(StringName targetVar, StringName abilityVar, GodotObject blackboard)
    {
        return CanUseFacts() &&
               _facts.HasPrimaryAbility &&
               _facts.CanStrikeNow &&
               HasBlackboardValue(blackboard, targetVar) &&
               (string.IsNullOrWhiteSpace(abilityVar.ToString()) || HasBlackboardValue(blackboard, abilityVar));
    }

    public bool emit_battle_intent(StringName templateId, StringName powerVar, string reason, GodotObject blackboard)
    {
        int power = ReadBlackboardInt(blackboard, powerVar, _facts.PrimaryAbilityPower);
        WriteLocalCombatObservations(_facts, blackboard);
        LastDecision = new BattleAiDecisionResult(BattleAiFacadeCore.ResolveTemplate(templateId.ToString()), power, reason, _facts);
        return true;
    }

    private bool CanUseFacts()
    {
        return _facts is { HasValidContext: true, ActorCanAct: true, HasTarget: true };
    }

    private static void SetBlackboardString(GodotObject blackboard, StringName key, string value)
    {
        blackboard.Call("set_var", key, value ?? "");
    }

    private static void SetBlackboardInt(GodotObject blackboard, StringName key, int value)
    {
        blackboard.Call("set_var", key, System.Math.Max(0, value));
    }

    private static void WriteLocalCombatObservations(BattleAiDecisionFacts facts, GodotObject blackboard)
    {
        if (facts?.HasLocalCombatObservation != true || blackboard == null)
        {
            return;
        }

        // LimboAI receives Runtime-owned tactical facts as observations only.
        // Region ownership and mutation remain outside Presentation.
        SetBlackboardString(blackboard, new StringName("local_combat_owner_group_id"), facts.LocalCombatOwnerBattleGroupId);
        SetBlackboardString(blackboard, new StringName("local_combat_region_id"), facts.LocalCombatRegionId);
        SetBlackboardString(blackboard, new StringName("local_combat_target_id"), facts.LocalCombatTargetActorId);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_center_x"), facts.LocalCombatCenterCellX);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_center_y"), facts.LocalCombatCenterCellY);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_center_height"), facts.LocalCombatCenterCellHeight);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_width"), System.Math.Max(1, facts.LocalCombatWidth));
        SetBlackboardRawInt(blackboard, new StringName("local_combat_height"), System.Math.Max(1, facts.LocalCombatHeight));
        SetBlackboardRawInt(blackboard, new StringName("local_combat_version"), facts.LocalCombatVersion);
        SetBlackboardString(blackboard, new StringName("local_combat_slot_kind"), facts.LocalCombatSelectedSlotKind);
        SetBlackboardString(blackboard, new StringName("local_combat_slot_role"), facts.LocalCombatSelectedSlotRole);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_slot_x"), facts.LocalCombatSelectedSlotCellX);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_slot_y"), facts.LocalCombatSelectedSlotCellY);
        SetBlackboardRawInt(blackboard, new StringName("local_combat_slot_height"), facts.LocalCombatSelectedSlotCellHeight);
        SetBlackboardString(blackboard, new StringName("local_combat_reason_code"), facts.LocalCombatReasonCode);
    }

    private static void SetBlackboardRawInt(GodotObject blackboard, StringName key, int value)
    {
        blackboard.Call("set_var", key, value);
    }

    private static int ReadBlackboardInt(GodotObject blackboard, StringName key, int fallback)
    {
        if (blackboard == null)
        {
            return System.Math.Max(0, fallback);
        }

        Variant value = blackboard.Call("get_var", key, fallback);
        return value.VariantType == Variant.Type.Int
            ? System.Math.Max(0, value.AsInt32())
            : System.Math.Max(0, fallback);
    }

    private static bool HasBlackboardValue(GodotObject blackboard, StringName key)
    {
        return !string.IsNullOrWhiteSpace(ReadBlackboardString(blackboard, key, ""));
    }

    private static string ReadBlackboardString(GodotObject blackboard, StringName key, string fallback)
    {
        if (blackboard == null || string.IsNullOrWhiteSpace(key.ToString()))
        {
            return fallback ?? "";
        }

        Variant value = blackboard.Call("get_var", key, fallback ?? "");
        if (value.VariantType == Variant.Type.Nil)
        {
            return fallback ?? "";
        }

        return value.ToString();
    }
}
