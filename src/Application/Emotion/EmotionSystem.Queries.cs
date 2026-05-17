using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Definitions.World;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed partial class EmotionSystem
{
    public EmotionOperationResult<EmotionActorSnapshot> QueryActor(string actorId)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor == null
            ? EmotionOperationResult<EmotionActorSnapshot>.Fail($"Actor '{actorId}' was not found.")
            : EmotionOperationResult<EmotionActorSnapshot>.Ok(new EmotionActorSnapshot(actor));
    }

    public EmotionOperationResult<int> QueryTrait(string actorId, EmotionAxis axis)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor == null
            ? EmotionOperationResult<int>.Fail($"Actor '{actorId}' was not found.")
            : EmotionOperationResult<int>.Ok(actor.GetTrait(axis));
    }

    public EmotionOperationResult<EmotionRelationshipSnapshot> QueryRelationship(string sourceActorId, string targetId)
    {
        EmotionActorState actor = _state.GetActor(sourceActorId);
        if (actor == null)
        {
            return EmotionOperationResult<EmotionRelationshipSnapshot>.Fail($"Actor '{sourceActorId}' was not found.");
        }

        EmotionRelationshipState relationship = actor.GetRelationship(targetId) ?? new EmotionRelationshipState(targetId);
        return EmotionOperationResult<EmotionRelationshipSnapshot>.Ok(new EmotionRelationshipSnapshot(relationship));
    }

    public EmotionOperationResult<EmotionDispositionResult> EvaluateDisposition(string actorId, string targetId)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        if (actor == null)
        {
            return EmotionOperationResult<EmotionDispositionResult>.Fail($"Actor '{actorId}' was not found.");
        }

        EmotionRelationshipState relationship = actor.GetRelationship(targetId) ?? new EmotionRelationshipState(targetId);
        int score =
            relationship.Get(EmotionRelationshipMetric.Trust) +
            relationship.Get(EmotionRelationshipMetric.Affinity) +
            relationship.Get(EmotionRelationshipMetric.Respect) / 2 -
            relationship.Get(EmotionRelationshipMetric.Fear) / 2 -
            relationship.Get(EmotionRelationshipMetric.Grievance) +
            actor.GetTrait(EmotionAxis.Empathy) / 3 +
            actor.GetTrait(EmotionAxis.Helpfulness) / 3 -
            actor.GetTrait(EmotionAxis.Aggression) / 4;

        score = Clamp(score);
        EmotionDisposition disposition = score switch
        {
            <= -50 => EmotionDisposition.Hostile,
            <= -15 => EmotionDisposition.Wary,
            < 25 => EmotionDisposition.Neutral,
            < 65 => EmotionDisposition.Friendly,
            _ => EmotionDisposition.Devoted
        };

        return EmotionOperationResult<EmotionDispositionResult>.Ok(new EmotionDispositionResult(actorId, targetId, score, disposition));
    }

    public IReadOnlyList<EmotionActorSnapshot> QueryActors()
    {
        return _state.Actors.Values
            .Select(actor => new EmotionActorSnapshot(actor))
            .ToArray();
    }
}
