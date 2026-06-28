using Godot;

namespace Rpg.Definitions.Battle.Abilities;

[GlobalClass]
public partial class DamageAbilityEffect : AbilityEffect
{
    [Export]
    public int Damage { get; set; } = 1;
}
