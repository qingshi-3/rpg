using System.Collections.Generic;
using Rpg.Definitions.Emotion;

namespace Rpg.Domain.Emotion;

public sealed class EmotionRelationshipState
{
    private readonly Dictionary<EmotionRelationshipMetric, int> _metrics = new();

    public EmotionRelationshipState(string targetId)
    {
        TargetId = targetId ?? "";
    }

    public string TargetId { get; }

    public IReadOnlyDictionary<EmotionRelationshipMetric, int> Metrics => _metrics;

    public int Get(EmotionRelationshipMetric metric)
    {
        return _metrics.TryGetValue(metric, out int value) ? value : 0;
    }

    public void Set(EmotionRelationshipMetric metric, int value)
    {
        _metrics[metric] = Clamp(value);
    }

    public void Add(EmotionRelationshipMetric metric, int amount)
    {
        Set(metric, Get(metric) + amount);
    }

    public EmotionRelationshipState Clone()
    {
        EmotionRelationshipState clone = new(TargetId);
        foreach ((EmotionRelationshipMetric metric, int value) in _metrics)
        {
            clone.Set(metric, value);
        }

        return clone;
    }

    private static int Clamp(int value)
    {
        return System.Math.Clamp(value, -100, 100);
    }
}
