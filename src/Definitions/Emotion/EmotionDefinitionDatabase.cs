using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionDefinitionDatabase : Resource
{
    [Export]
    public Godot.Collections.Array<RaceEmotionProfileDefinition> RaceProfiles { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EmotionProfileModifierDefinition> ProfileModifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EmotionEventDefinition> EventDefinitions { get; set; } = new();
}
