using Rpg.Definitions.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionRelationshipMetricWeight
{
    public EmotionRelationshipMetricWeight(EmotionRelationshipMetric metric, int weight)
    {
        Metric = metric;
        Weight = weight;
    }

    public EmotionRelationshipMetric Metric { get; }
    public int Weight { get; }
}
