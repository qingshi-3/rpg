using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionEffectDefinition : Resource
{
    [Export]
    public EmotionEffectKind Kind { get; set; } = EmotionEffectKind.TraitDelta;

    [Export]
    public string ActorId { get; set; } = "";

    [Export]
    public string TargetId { get; set; } = "";

    [Export]
    public EmotionAxis Axis { get; set; } = EmotionAxis.Trust;

    [Export]
    public EmotionRelationshipMetric RelationshipMetric { get; set; } = EmotionRelationshipMetric.Trust;

    [Export(PropertyHint.Range, "-100,100,1")]
    public int Amount { get; set; }

    [Export]
    public string MemoryId { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string MemoryDescription { get; set; } = "";

    [Export(PropertyHint.Range, "-100,100,1")]
    public int MemoryWeight { get; set; }

    [Export]
    public Godot.Collections.Array<string> MemoryTags { get; set; } = new();
}
