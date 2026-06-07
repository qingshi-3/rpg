using System.Collections.Generic;

namespace Rpg.Definitions.Battle.Skills;

public sealed class BattleSkillDefinition
{
    public string SkillId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public BattleSkillTargetingMode TargetingMode { get; init; } = BattleSkillTargetingMode.None;
    public int Range { get; init; }
    public BattleSkillActionTimingDefinition Timing { get; init; } = new();
    public BattleSkillInterruptPolicyDefinition InterruptPolicy { get; init; } = new();
    public List<BattleSkillEffectDefinition> Effects { get; init; } = new();
}
