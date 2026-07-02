using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class BattleSkillTimingResource : Resource
{
    [Export] public double CastSeconds { get; set; }
    [Export] public double ImpactDelaySeconds { get; set; }
    [Export] public double RecoverySeconds { get; set; }
}
