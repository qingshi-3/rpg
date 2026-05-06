using System.Collections.Generic;
using Rpg.Definitions.Emotion;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionRelationshipSnapshot
{
    public EmotionRelationshipSnapshot(EmotionRelationshipState state)
    {
        TargetId = state?.TargetId ?? "";
        Metrics = state == null
            ? new Dictionary<EmotionRelationshipMetric, int>()
            : new Dictionary<EmotionRelationshipMetric, int>(state.Metrics);
    }

    public string TargetId { get; }
    public IReadOnlyDictionary<EmotionRelationshipMetric, int> Metrics { get; }

    public int Get(EmotionRelationshipMetric metric)
    {
        return Metrics.TryGetValue(metric, out int value) ? value : 0;
    }
}
