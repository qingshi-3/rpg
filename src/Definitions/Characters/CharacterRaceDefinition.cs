using Godot;

namespace Rpg.Definitions.Characters;

[GlobalClass]
public partial class CharacterRaceDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "种族";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";

    [Export]
    public Godot.Collections.Array<CharacterRaceTag> RaceTags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CharacterAttributeValue> BaselineAttributes { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> DefaultEmotionModifierIds { get; set; } = new();
}
