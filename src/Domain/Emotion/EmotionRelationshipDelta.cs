using Rpg.Definitions.Emotion;

namespace Rpg.Domain.Emotion;

public sealed class EmotionRelationshipDelta
{
    public EmotionRelationshipDelta(string sourceActorId, string targetId, EmotionRelationshipMetric metric, int amount)
    {
        SourceActorId = sourceActorId ?? "";
        TargetId = targetId ?? "";
        Metric = metric;
        Amount = amount;
    }

    public string SourceActorId { get; }
    public string TargetId { get; }
    public EmotionRelationshipMetric Metric { get; }
    public int Amount { get; }
}
