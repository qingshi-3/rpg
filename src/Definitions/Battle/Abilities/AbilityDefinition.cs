using Godot;

namespace Rpg.Definitions.Battle.Abilities;

[GlobalClass]
public partial class AbilityDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "能力";

    [Export]
    public string IconText { get; set; } = "";

    [Export]
    public int Range { get; set; } = 1;

    [Export]
    public AbilityTargetMode TargetMode { get; set; } = AbilityTargetMode.UnitTarget;

    [Export]
    public AbilityDirectionMode DirectionMode { get; set; } = AbilityDirectionMode.EightWay;

    [Export]
    public AbilityAreaShape AreaShape { get; set; } = AbilityAreaShape.SingleActor;

    [Export]
    public Godot.Collections.Array<AbilityTargetRule> TargetRules { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AbilityEffect> Effects { get; set; } = new();
}
