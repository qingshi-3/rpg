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
        return true;
    }

    public bool can_strike_battle_target(StringName targetVar, StringName abilityVar, GodotObject blackboard)
    {
        return CanUseFacts() && _facts.HasPrimaryAbility && _facts.CanStrikeNow && blackboard != null;
    }

    public bool emit_battle_intent(StringName templateId, StringName powerVar, string reason, GodotObject blackboard)
    {
        int power = ReadBlackboardInt(blackboard, powerVar, _facts.PrimaryAbilityPower);
        LastDecision = new BattleAiDecisionResult(BattleAiFacadeCore.ResolveTemplate(templateId.ToString()), power, reason);
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
}
