using System;
using System.Collections.Generic;
using Rpg.Definitions.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionEventReactionQuery
{
    public EmotionEventReactionQuery(
        string actorId,
        string targetId,
        EmotionEventKind eventKind,
        int baseScore = 0,
        int risk = 0,
        int rewardValue = 0,
        IEnumerable<EmotionAxisWeight> traitWeights = null,
        IEnumerable<EmotionRelationshipMetricWeight> relationshipWeights = null)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        EventKind = eventKind;
        BaseScore = baseScore;
        Risk = risk;
        RewardValue = rewardValue;
        TraitWeights = new List<EmotionAxisWeight>(traitWeights ?? Array.Empty<EmotionAxisWeight>());
        RelationshipWeights = new List<EmotionRelationshipMetricWeight>(relationshipWeights ?? Array.Empty<EmotionRelationshipMetricWeight>());
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public EmotionEventKind EventKind { get; }
    public int BaseScore { get; }
    public int Risk { get; }
    public int RewardValue { get; }
    public IReadOnlyList<EmotionAxisWeight> TraitWeights { get; }
    public IReadOnlyList<EmotionRelationshipMetricWeight> RelationshipWeights { get; }
}
