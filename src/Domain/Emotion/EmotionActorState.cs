using System.Collections.Generic;
using Rpg.Definitions.Emotion;

namespace Rpg.Domain.Emotion;

public sealed class EmotionActorState
{
    private readonly Dictionary<EmotionAxis, int> _traits = new();
    private readonly Dictionary<string, EmotionRelationshipState> _relationships = new();
    private readonly List<EmotionMemoryState> _memories = new();

    public EmotionActorState(string actorId, string displayName, string raceId, bool isSpecial)
    {
        ActorId = actorId ?? "";
        DisplayName = displayName ?? "";
        RaceId = raceId ?? "";
        IsSpecial = isSpecial;
    }

    public string ActorId { get; }
    public string DisplayName { get; }
    public string RaceId { get; }
    public bool IsSpecial { get; }
    public IReadOnlyDictionary<EmotionAxis, int> Traits => _traits;
    public IReadOnlyDictionary<string, EmotionRelationshipState> Relationships => _relationships;
    public IReadOnlyList<EmotionMemoryState> Memories => _memories;

    public int GetTrait(EmotionAxis axis)
    {
        return _traits.TryGetValue(axis, out int value) ? value : 0;
    }

    public void SetTrait(EmotionAxis axis, int value)
    {
        _traits[axis] = Clamp(value);
    }

    public void AddTrait(EmotionAxis axis, int amount)
    {
        SetTrait(axis, GetTrait(axis) + amount);
    }

    public EmotionRelationshipState GetOrCreateRelationship(string targetId)
    {
        targetId ??= "";
        if (!_relationships.TryGetValue(targetId, out EmotionRelationshipState relationship))
        {
            relationship = new EmotionRelationshipState(targetId);
            _relationships[targetId] = relationship;
        }

        return relationship;
    }

    public EmotionRelationshipState GetRelationship(string targetId)
    {
        targetId ??= "";
        return _relationships.TryGetValue(targetId, out EmotionRelationshipState relationship)
            ? relationship
            : null;
    }

    public void AddMemory(EmotionMemoryState memory)
    {
        if (memory == null || string.IsNullOrWhiteSpace(memory.Id))
        {
            return;
        }

        _memories.Add(memory);
    }

    public bool HasMemoryTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        foreach (EmotionMemoryState memory in _memories)
        {
            foreach (string memoryTag in memory.Tags)
            {
                if (memoryTag == tag)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public EmotionActorState Clone()
    {
        EmotionActorState clone = new(ActorId, DisplayName, RaceId, IsSpecial);
        foreach ((EmotionAxis axis, int value) in _traits)
        {
            clone.SetTrait(axis, value);
        }

        foreach ((string targetId, EmotionRelationshipState relationship) in _relationships)
        {
            clone._relationships[targetId] = relationship.Clone();
        }

        foreach (EmotionMemoryState memory in _memories)
        {
            clone.AddMemory(memory.Clone());
        }

        return clone;
    }

    private static int Clamp(int value)
    {
        return System.Math.Clamp(value, -100, 100);
    }
}
