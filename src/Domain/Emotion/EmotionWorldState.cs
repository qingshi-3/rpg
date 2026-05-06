using System.Collections.Generic;

namespace Rpg.Domain.Emotion;

public sealed class EmotionWorldState
{
    private readonly Dictionary<string, EmotionActorState> _actors = new();

    public IReadOnlyDictionary<string, EmotionActorState> Actors => _actors;

    public bool ContainsActor(string actorId)
    {
        return !string.IsNullOrWhiteSpace(actorId) && _actors.ContainsKey(actorId);
    }

    public EmotionActorState GetActor(string actorId)
    {
        actorId ??= "";
        return _actors.TryGetValue(actorId, out EmotionActorState actor) ? actor : null;
    }

    public void SetActor(EmotionActorState actor)
    {
        if (actor == null || string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return;
        }

        _actors[actor.ActorId] = actor;
    }

    public EmotionEventResult ApplyEvent(EmotionEvent emotionEvent)
    {
        HashSet<string> changedActors = new();
        List<string> warnings = new();

        if (emotionEvent == null)
        {
            warnings.Add("Emotion event is null.");
            return new EmotionEventResult("", changedActors, warnings);
        }

        foreach (EmotionTraitDelta delta in emotionEvent.TraitDeltas)
        {
            if (delta == null)
            {
                continue;
            }

            string actorId = string.IsNullOrWhiteSpace(delta.ActorId) ? emotionEvent.SubjectActorId : delta.ActorId;
            EmotionActorState actor = GetActor(actorId);
            if (actor == null)
            {
                warnings.Add($"Trait delta skipped; actor '{actorId}' was not found.");
                continue;
            }

            actor.AddTrait(delta.Axis, delta.Amount);
            changedActors.Add(actor.ActorId);
        }

        foreach (EmotionRelationshipDelta delta in emotionEvent.RelationshipDeltas)
        {
            if (delta == null)
            {
                continue;
            }

            string sourceActorId = string.IsNullOrWhiteSpace(delta.SourceActorId)
                ? emotionEvent.SubjectActorId
                : delta.SourceActorId;
            EmotionActorState actor = GetActor(sourceActorId);
            if (actor == null)
            {
                warnings.Add($"Relationship delta skipped; actor '{sourceActorId}' was not found.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(delta.TargetId))
            {
                warnings.Add($"Relationship delta skipped; target id is empty for actor '{sourceActorId}'.");
                continue;
            }

            actor.GetOrCreateRelationship(delta.TargetId).Add(delta.Metric, delta.Amount);
            changedActors.Add(actor.ActorId);
        }

        foreach (EmotionMemoryDelta delta in emotionEvent.MemoryDeltas)
        {
            if (delta?.Memory == null)
            {
                continue;
            }

            string actorId = string.IsNullOrWhiteSpace(delta.ActorId) ? emotionEvent.SubjectActorId : delta.ActorId;
            EmotionActorState actor = GetActor(actorId);
            if (actor == null)
            {
                warnings.Add($"Memory skipped; actor '{actorId}' was not found.");
            }
            else
            {
                actor.AddMemory(delta.Memory);
                changedActors.Add(actor.ActorId);
            }
        }

        return new EmotionEventResult(emotionEvent.EventId, changedActors, warnings);
    }

    public EmotionWorldState Clone()
    {
        EmotionWorldState clone = new();
        foreach (EmotionActorState actor in _actors.Values)
        {
            clone.SetActor(actor.Clone());
        }

        return clone;
    }
}
