using System.Collections.Generic;

namespace Rpg.Domain.Emotion;

public sealed class EmotionEventResult
{
    public EmotionEventResult(string eventId, IEnumerable<string> changedActorIds, IEnumerable<string> warnings)
    {
        EventId = eventId ?? "";
        ChangedActorIds = new List<string>(changedActorIds ?? System.Array.Empty<string>());
        Warnings = new List<string>(warnings ?? System.Array.Empty<string>());
    }

    public string EventId { get; }
    public IReadOnlyList<string> ChangedActorIds { get; }
    public IReadOnlyList<string> Warnings { get; }
}
