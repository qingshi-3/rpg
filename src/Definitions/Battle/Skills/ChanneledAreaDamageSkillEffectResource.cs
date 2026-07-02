using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class ChanneledAreaDamageSkillEffectResource : BattleSkillEffectResource
{
    [Export] public int BaseDamage { get; set; }
    [Export] public BattleSkillDamageTypeDefinition DamageType { get; set; } = BattleSkillDamageTypeDefinition.Lightning;
    [Export] public double DurationSeconds { get; set; }
    [Export] public double TickIntervalSeconds { get; set; }
    [Export] public BattleSkillAreaShapeDefinition AreaShape { get; set; } = BattleSkillAreaShapeDefinition.GridRadius;
    [Export] public int Radius { get; set; }
    [Export] public bool FollowsCaster { get; set; }
    [Export] public bool UsesTargetOffset { get; set; }
}
