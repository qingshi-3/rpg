using System;
using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionRelationshipGateQuery
{
    public EmotionRelationshipGateQuery(
        string actorId,
        string targetId,
        EmotionRelationshipGateKind kind,
        int? requiredScoreOverride = null,
        IEnumerable<string> requiredMemoryTags = null,
        IEnumerable<string> blockingMemoryTags = null)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        Kind = kind;
        RequiredScoreOverride = requiredScoreOverride;
        RequiredMemoryTags = new List<string>(requiredMemoryTags ?? Array.Empty<string>());
        BlockingMemoryTags = new List<string>(blockingMemoryTags ?? Array.Empty<string>());
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public EmotionRelationshipGateKind Kind { get; }
    public int? RequiredScoreOverride { get; }
    public IReadOnlyList<string> RequiredMemoryTags { get; }
    public IReadOnlyList<string> BlockingMemoryTags { get; }
}
