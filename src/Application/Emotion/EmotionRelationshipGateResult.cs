using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionRelationshipGateResult
{
    public EmotionRelationshipGateResult(
        string actorId,
        string targetId,
        EmotionRelationshipGateKind kind,
        bool passed,
        int score,
        int requiredScore,
        string blockReasonCode,
        IEnumerable<EmotionScoreFactor> factors,
        IEnumerable<string> missingRequiredMemoryTags,
        IEnumerable<string> activeBlockingMemoryTags)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        Kind = kind;
        Passed = passed;
        Score = score;
        RequiredScore = requiredScore;
        BlockReasonCode = blockReasonCode ?? "";
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
        MissingRequiredMemoryTags = new List<string>(missingRequiredMemoryTags ?? System.Array.Empty<string>());
        ActiveBlockingMemoryTags = new List<string>(activeBlockingMemoryTags ?? System.Array.Empty<string>());
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public EmotionRelationshipGateKind Kind { get; }
    public bool Passed { get; }
    public int Score { get; }
    public int RequiredScore { get; }
    public string BlockReasonCode { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
    public IReadOnlyList<string> MissingRequiredMemoryTags { get; }
    public IReadOnlyList<string> ActiveBlockingMemoryTags { get; }
}
