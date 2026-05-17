using Godot;

namespace Rpg.Definitions.Heroes;

[GlobalClass]
public partial class HeroAttributeBlock : Resource
{
    [Export] public int Martial { get; set; }
    [Export] public int Vitality { get; set; }
    [Export] public int Technique { get; set; }
    [Export] public int Tactics { get; set; }
    [Export] public int Willpower { get; set; }
    [Export] public int Charisma { get; set; }
    [Export] public int Craft { get; set; }
    [Export] public int Mystic { get; set; }
}
