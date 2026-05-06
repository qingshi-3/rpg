using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class RaceEmotionProfileDefinition : Resource
{
    [Export]
    public string RaceId { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string BaselineDescription { get; set; } = "";

    [Export]
    public Godot.Collections.Array<EmotionTraitDefinition> BaselineTraits { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EmotionRelationshipModifierDefinition> InitialRelationshipModifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> MemoryTags { get; set; } = new();

    [Export(PropertyHint.Range, "0,40,1")]
    public int IndividualVariance { get; set; } = 12;
}
