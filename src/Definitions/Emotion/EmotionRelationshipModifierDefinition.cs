using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionRelationshipModifierDefinition : Resource
{
    [Export]
    public string TargetId { get; set; } = "player_faction";

    [Export]
    public EmotionRelationshipMetric Metric { get; set; } = EmotionRelationshipMetric.Trust;

    [Export(PropertyHint.Range, "-100,100,1")]
    public int Amount { get; set; }
}
