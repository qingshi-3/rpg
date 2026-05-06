using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionRecruitmentResult
{
    public EmotionRecruitmentResult(
        string actorId,
        string recruiterFactionId,
        bool canRecruit,
        int score,
        int recruitmentChanceModifierPercent,
        string blockReasonCode,
        EmotionDispositionResult disposition,
        IEnumerable<EmotionScoreFactor> factors,
        IEnumerable<string> missingRequiredMemoryTags,
        IEnumerable<string> activeBlockingMemoryTags)
    {
        ActorId = actorId ?? "";
        RecruiterFactionId = recruiterFactionId ?? "";
        CanRecruit = canRecruit;
        Score = score;
        RecruitmentChanceModifierPercent = recruitmentChanceModifierPercent;
        BlockReasonCode = blockReasonCode ?? "";
        Disposition = disposition;
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
        MissingRequiredMemoryTags = new List<string>(missingRequiredMemoryTags ?? System.Array.Empty<string>());
        ActiveBlockingMemoryTags = new List<string>(activeBlockingMemoryTags ?? System.Array.Empty<string>());
    }

    public string ActorId { get; }
    public string RecruiterFactionId { get; }
    public bool CanRecruit { get; }
    public int Score { get; }
    public int RecruitmentChanceModifierPercent { get; }
    public string BlockReasonCode { get; }
    public EmotionDispositionResult Disposition { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
    public IReadOnlyList<string> MissingRequiredMemoryTags { get; }
    public IReadOnlyList<string> ActiveBlockingMemoryTags { get; }
}
