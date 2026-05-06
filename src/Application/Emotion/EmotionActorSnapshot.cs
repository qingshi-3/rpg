using System.Collections.Generic;
using Rpg.Definitions.Emotion;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionActorSnapshot
{
    public EmotionActorSnapshot(EmotionActorState state)
    {
        ActorId = state?.ActorId ?? "";
        DisplayName = state?.DisplayName ?? "";
        RaceId = state?.RaceId ?? "";
        IsSpecial = state?.IsSpecial ?? false;
        Traits = state == null
            ? new Dictionary<EmotionAxis, int>()
            : new Dictionary<EmotionAxis, int>(state.Traits);

        Dictionary<string, EmotionRelationshipSnapshot> relationships = new();
        if (state != null)
        {
            foreach ((string targetId, EmotionRelationshipState relationship) in state.Relationships)
            {
                relationships[targetId] = new EmotionRelationshipSnapshot(relationship);
            }
        }

        Relationships = relationships;
        List<EmotionMemoryState> memories = new();
        if (state != null)
        {
            foreach (EmotionMemoryState memory in state.Memories)
            {
                memories.Add(memory.Clone());
            }
        }

        Memories = memories;
    }

    public string ActorId { get; }
    public string DisplayName { get; }
    public string RaceId { get; }
    public bool IsSpecial { get; }
    public IReadOnlyDictionary<EmotionAxis, int> Traits { get; }
    public IReadOnlyDictionary<string, EmotionRelationshipSnapshot> Relationships { get; }
    public IReadOnlyList<EmotionMemoryState> Memories { get; }

    public int GetTrait(EmotionAxis axis)
    {
        return Traits.TryGetValue(axis, out int value) ? value : 0;
    }
}
