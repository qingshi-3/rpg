using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionTraitDefinition : Resource
{
    [Export]
    public EmotionAxis Axis { get; set; } = EmotionAxis.Trust;

    [Export(PropertyHint.Range, "-100,100,1")]
    public int Value { get; set; }
}
