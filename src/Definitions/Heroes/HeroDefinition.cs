using Godot;

namespace Rpg.Definitions.Heroes;

[GlobalClass]
public partial class HeroDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "英雄";
    [Export] public string ProfessionId { get; set; } = "";
    [Export] public HeroAttributeBlock BaseAttributes { get; set; }
    [Export] public Godot.Collections.Array<string> StartingSkillIds { get; set; } = new();
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
}
