using System;
using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionRecruitmentQuery
{
    public EmotionRecruitmentQuery(
        string actorId,
        string recruiterFactionId,
        int difficulty = 0,
        int offerValue = 0,
        IEnumerable<string> requiredMemoryTags = null,
        IEnumerable<string> blockingMemoryTags = null)
    {
        ActorId = actorId ?? "";
        RecruiterFactionId = recruiterFactionId ?? "";
        Difficulty = difficulty;
        OfferValue = offerValue;
        RequiredMemoryTags = new List<string>(requiredMemoryTags ?? Array.Empty<string>());
        BlockingMemoryTags = new List<string>(blockingMemoryTags ?? Array.Empty<string>());
    }

    public string ActorId { get; }
    public string RecruiterFactionId { get; }
    public int Difficulty { get; }
    public int OfferValue { get; }
    public IReadOnlyList<string> RequiredMemoryTags { get; }
    public IReadOnlyList<string> BlockingMemoryTags { get; }
}
