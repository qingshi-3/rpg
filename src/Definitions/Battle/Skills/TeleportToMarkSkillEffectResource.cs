using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class TeleportToMarkSkillEffectResource : BattleSkillEffectResource
{
    [Export] public BattleSkillMarkKindDefinition RequiredMarkKind { get; set; } = BattleSkillMarkKindDefinition.ThunderMark;
    [Export] public int LandingRadius { get; set; }
    [Export] public bool ConsumesMark { get; set; }
}
