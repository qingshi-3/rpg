using System.Collections.Generic;

namespace Rpg.Application.Emotion;

public sealed class EmotionLoyaltyResult
{
    public EmotionLoyaltyResult(
        string actorId,
        string factionId,
        int riskScore,
        EmotionLoyaltyRiskLevel riskLevel,
        int desertionChanceModifierPercent,
        IEnumerable<EmotionScoreFactor> factors)
    {
        ActorId = actorId ?? "";
        FactionId = factionId ?? "";
        RiskScore = riskScore;
        RiskLevel = riskLevel;
        DesertionChanceModifierPercent = desertionChanceModifierPercent;
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
    }

    public string ActorId { get; }
    public string FactionId { get; }
    public int RiskScore { get; }
    public EmotionLoyaltyRiskLevel RiskLevel { get; }
    public int DesertionChanceModifierPercent { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
}
