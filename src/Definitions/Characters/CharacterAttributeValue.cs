using Godot;

namespace Rpg.Definitions.Characters;

[GlobalClass]
public partial class CharacterAttributeValue : Resource
{
    [Export]
    public CharacterAttribute Attribute { get; set; } = CharacterAttribute.Strength;

    [Export(PropertyHint.Range, "-100,100,1")]
    public int Value { get; set; }
}
