using System.Collections.Generic;
using Rpg.Definitions.World;

namespace Rpg.Application.Emotion;

public sealed class EmotionTaskAssignmentResult
{
    public EmotionTaskAssignmentResult(
        string actorId,
        string targetId,
        WorldTaskKind taskKind,
        bool canAssign,
        int score,
        int efficiencyModifierPercent,
        int loyaltyRiskDelta,
        string blockReasonCode,
        IEnumerable<EmotionScoreFactor> factors)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        TaskKind = taskKind;
        CanAssign = canAssign;
        Score = score;
        EfficiencyModifierPercent = efficiencyModifierPercent;
        LoyaltyRiskDelta = loyaltyRiskDelta;
        BlockReasonCode = blockReasonCode ?? "";
        Factors = new List<EmotionScoreFactor>(factors ?? System.Array.Empty<EmotionScoreFactor>());
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public WorldTaskKind TaskKind { get; }
    public bool CanAssign { get; }
    public int Score { get; }
    public int EfficiencyModifierPercent { get; }
    public int LoyaltyRiskDelta { get; }
    public string BlockReasonCode { get; }
    public IReadOnlyList<EmotionScoreFactor> Factors { get; }
}
