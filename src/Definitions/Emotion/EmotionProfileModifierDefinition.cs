using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionProfileModifierDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "情感修正";

    [Export]
    public EmotionProfileModifierKind Kind { get; set; } = EmotionProfileModifierKind.Archetype;

    [Export]
    public Godot.Collections.Array<EmotionTraitDefinition> TraitModifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EmotionRelationshipModifierDefinition> RelationshipModifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> MemoryTags { get; set; } = new();

    [Export(PropertyHint.Range, "0,40,1")]
    public int VarianceBonus { get; set; }
}
