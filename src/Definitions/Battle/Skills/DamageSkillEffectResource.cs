using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class DamageSkillEffectResource : BattleSkillEffectResource
{
    [Export] public int BaseDamage { get; set; }
    [Export] public BattleSkillDamageTypeDefinition DamageType { get; set; } = BattleSkillDamageTypeDefinition.Physical;
    [Export] public bool CanHitActors { get; set; } = true;
    [Export] public bool CanHitWorldObjects { get; set; }
}
