using System.Collections.Generic;
using Rpg.Definitions.Emotion;

namespace Rpg.Domain.Emotion;

public sealed class EmotionEvent
{
    public EmotionEvent(
        string eventId,
        EmotionEventKind kind,
        string subjectActorId,
        string sourceId,
        IEnumerable<EmotionTraitDelta> traitDeltas,
        IEnumerable<EmotionRelationshipDelta> relationshipDeltas,
        EmotionMemoryState memory = null,
        IEnumerable<EmotionMemoryDelta> memoryDeltas = null)
    {
        EventId = eventId ?? "";
        Kind = kind;
        SubjectActorId = subjectActorId ?? "";
        SourceId = sourceId ?? "";
        TraitDeltas = new List<EmotionTraitDelta>(traitDeltas ?? System.Array.Empty<EmotionTraitDelta>());
        RelationshipDeltas = new List<EmotionRelationshipDelta>(relationshipDeltas ?? System.Array.Empty<EmotionRelationshipDelta>());
        Memory = memory;

        List<EmotionMemoryDelta> allMemoryDeltas = new(memoryDeltas ?? System.Array.Empty<EmotionMemoryDelta>());
        if (memory != null)
        {
            allMemoryDeltas.Insert(0, new EmotionMemoryDelta(SubjectActorId, memory));
        }

        MemoryDeltas = allMemoryDeltas;
    }

    public string EventId { get; }
    public EmotionEventKind Kind { get; }
    public string SubjectActorId { get; }
    public string SourceId { get; }
    public IReadOnlyList<EmotionTraitDelta> TraitDeltas { get; }
    public IReadOnlyList<EmotionRelationshipDelta> RelationshipDeltas { get; }
    public EmotionMemoryState Memory { get; }
    public IReadOnlyList<EmotionMemoryDelta> MemoryDeltas { get; }
}
