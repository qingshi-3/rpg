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
    public int ApCost { get; set; } = 1;

    [Export]
    public int Range { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<AbilityTargetRule> TargetRules { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AbilityEffect> Effects { get; set; } = new();
}
