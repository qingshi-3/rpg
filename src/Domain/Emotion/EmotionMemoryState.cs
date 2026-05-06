using System.Collections.Generic;

namespace Rpg.Domain.Emotion;

public sealed class EmotionMemoryState
{
    public EmotionMemoryState(string id, string sourceEventId, string description, int weight, IEnumerable<string> tags)
    {
        Id = id ?? "";
        SourceEventId = sourceEventId ?? "";
        Description = description ?? "";
        Weight = Clamp(weight);
        Tags = new List<string>(tags ?? System.Array.Empty<string>());
    }

    public string Id { get; }
    public string SourceEventId { get; }
    public string Description { get; }
    public int Weight { get; }
    public IReadOnlyList<string> Tags { get; }

    public EmotionMemoryState Clone()
    {
        return new EmotionMemoryState(Id, SourceEventId, Description, Weight, Tags);
    }

    private static int Clamp(int value)
    {
        return System.Math.Clamp(value, -100, 100);
    }
}
