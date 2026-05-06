using System.Collections.Generic;
using Rpg.Definitions.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionEventReactionResult
{
    public EmotionEventReactionResult(
        string actorId,
        string targetId,
        EmotionEventKind eventKind,
        int score,
        EmotionEventReactionTone tone,
        int relationshipDeltaPreview,
        IEnumerable<EmotionScoreFactor> factors)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        EventKind = eventKind;
        Score = score;
        Tone = tone;
        RelationshipDeltaPreview = relationshipDeltaPreview;
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public EmotionEventKind EventKind { get; }
    public int Score { get; }
    public EmotionEventReactionTone Tone { get; }
    public int RelationshipDeltaPreview { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
}
