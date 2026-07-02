using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class CreateMarkSkillEffectResource : BattleSkillEffectResource
{
    [Export] public BattleSkillMarkKindDefinition MarkKind { get; set; } = BattleSkillMarkKindDefinition.ThunderMark;
    [Export] public double LifetimeSeconds { get; set; }
    [Export] public bool AttachToActorWhenTargeted { get; set; }
    [Export] public bool ReplaceExistingOwnedMark { get; set; } = true;
}
