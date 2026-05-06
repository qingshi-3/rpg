using Godot;

namespace Rpg.Definitions.Characters;

[GlobalClass]
public partial class CharacterDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "角色";

    [Export]
    public CharacterRaceDefinition Race { get; set; }

    [Export]
    public string CultureId { get; set; } = "";

    [Export]
    public string FactionId { get; set; } = "";

    [Export]
    public string ProfessionId { get; set; } = "";

    [Export]
    public Godot.Collections.Array<CharacterAttributeValue> AttributeModifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> EmotionModifierIds { get; set; } = new();

    [Export]
    public bool IsSpecial { get; set; }
}
