using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionConditionDefinition : Resource
{
    [Export]
    public EmotionConditionKind Kind { get; set; } = EmotionConditionKind.TraitThreshold;

    [Export]
    public string ActorId { get; set; } = "";

    [Export]
    public string TargetId { get; set; } = "";

    [Export]
    public EmotionAxis Axis { get; set; } = EmotionAxis.Trust;

    [Export]
    public EmotionRelationshipMetric RelationshipMetric { get; set; } = EmotionRelationshipMetric.Trust;

    [Export]
    public EmotionComparisonOperator Comparison { get; set; } = EmotionComparisonOperator.AtLeast;

    [Export(PropertyHint.Range, "-100,100,1")]
    public int Threshold { get; set; }

    [Export]
    public EmotionDispositionMinimum Disposition { get; set; } = EmotionDispositionMinimum.Neutral;

    [Export]
    public string MemoryTag { get; set; } = "";
}
