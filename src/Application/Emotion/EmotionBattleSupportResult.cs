using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionBattleSupportResult
{
    public EmotionBattleSupportResult(
        string actorId,
        string supportedFactionId,
        bool canSupport,
        int score,
        int supportChanceModifierPercent,
        int moraleModifierPercent,
        string blockReasonCode,
        IEnumerable<EmotionScoreFactor> factors)
    {
        ActorId = actorId ?? "";
        SupportedFactionId = supportedFactionId ?? "";
        CanSupport = canSupport;
        Score = score;
        SupportChanceModifierPercent = supportChanceModifierPercent;
        MoraleModifierPercent = moraleModifierPercent;
        BlockReasonCode = blockReasonCode ?? "";
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
    }

    public string ActorId { get; }
    public string SupportedFactionId { get; }
    public bool CanSupport { get; }
    public int Score { get; }
    public int SupportChanceModifierPercent { get; }
    public int MoraleModifierPercent { get; }
    public string BlockReasonCode { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
}
