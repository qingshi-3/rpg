using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class BattleSkillInterruptPolicyResource : Resource
{
    [Export] public bool CanInterruptBasicAttackWindup { get; set; }
    [Export] public bool CanCancelBasicAttackRecovery { get; set; }
    [Export] public bool ReleasesWithoutOccupyingCaster { get; set; }
    [Export] public bool CanInterruptActiveChannel { get; set; }
}
