using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionConditionContext
{
    public EmotionConditionContext(
        string subjectActorId = "",
        string targetId = "",
        string sourceId = "",
        string eventId = "",
        IEnumerable<string> tags = null)
    {
        SubjectActorId = subjectActorId ?? "";
        TargetId = targetId ?? "";
        SourceId = sourceId ?? "";
        EventId = eventId ?? "";
        Tags = new List<string>(tags ?? System.Array.Empty<string>());
    }

    public string SubjectActorId { get; }
    public string TargetId { get; }
    public string SourceId { get; }
    public string EventId { get; }
    public IReadOnlyList<string> Tags { get; }
}
