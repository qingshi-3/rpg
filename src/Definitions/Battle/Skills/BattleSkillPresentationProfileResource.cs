using Godot;

namespace Rpg.Definitions.Battle.Skills;

[GlobalClass]
public partial class BattleSkillPresentationProfileResource : Resource
{
    [Export] public string ProfileId { get; set; } = "";
    [Export] public string CastFxProfileId { get; set; } = "";
    [Export] public string ImpactFxProfileId { get; set; } = "";
    [Export] public string MarkFxProfileId { get; set; } = "";
    [Export] public string AreaFxProfileId { get; set; } = "";
    [Export] public bool SuppressActorCastFx { get; set; }
    [Export] public bool HoldCastAnimationDuringAction { get; set; }
}
